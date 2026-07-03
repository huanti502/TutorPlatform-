using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;

namespace TutorPlatform.Web.Controllers;

public class BlogController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IWebHostEnvironment _env; // Cần thiết để lưu file
    private readonly CloudinaryService _cloudinary;
    private readonly ModerationService _moderation;

    public BlogController(AppDbContext db, UserManager<AppUser> userManager, IWebHostEnvironment env, CloudinaryService cloudinary, ModerationService moderation)
    {
        _db = db;
        _userManager = userManager;
        _env = env; // Đừng quên gán biến này nhé!
        _cloudinary = cloudinary;
        _moderation = moderation;
    }

    // Xem danh sách bài viết (Đã sửa - thêm search, sort và bài nổi bật)
    public async Task<IActionResult> Index(string? search, string sort = "newest")
    {
        var query = _db.Posts
            .Include(p => p.Author)
            .Include(p => p.Comments)
            .Include(p => p.Likes)
            .Where(p => p.IsPublished)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.Title.Contains(search) || p.Content.Contains(search));

        // Sort
        query = sort switch
        {
            "popular" => query.OrderByDescending(p => p.Likes.Count + p.Comments.Count * 2),
            "mostliked" => query.OrderByDescending(p => p.Likes.Count),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        // Bài nổi bật (top 3 nhiều like + comment nhất)
        var featured = await _db.Posts
            .Include(p => p.Author)
            .Include(p => p.Comments)
            .Include(p => p.Likes)
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.Likes.Count + p.Comments.Count * 2)
            .Take(3)
            .ToListAsync();

        ViewBag.Featured = featured;
        ViewBag.Sort = sort;
        ViewBag.Search = search;
        ViewBag.CurrentUserId = _userManager.GetUserId(User);

        return View(await query.Take(20).ToListAsync());
    }

    // Xem chi tiết bài viết
    public async Task<IActionResult> Detail(int id)
    {
        var post = await _db.Posts
            .Include(p => p.Author)
            .Include(p => p.Comments).ThenInclude(c => c.Author)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (post == null) return NotFound();

        // Tăng lượt xem
        post.Views++;
        await _db.SaveChangesAsync();

        return View(post);
    }

    // Form tạo bài viết (Chỉ người đã đăng nhập)
    [Authorize]
    [HttpGet]
    public IActionResult Create() => View();

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(string title, string content, IFormFile? imageFile)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Vui lòng nhập đầy đủ tiêu đề và nội dung.";
            return View();
        }

        var user = await _userManager.GetUserAsync(User);
        var post = new Post
        {
            Title = title,
            Content = content,
            AuthorId = user!.Id,
            CreatedAt = DateTime.UtcNow,
            IsPublished = true // Đảm bảo bài viết được hiển thị theo điều kiện Where(p => p.IsPublished)
        };

        // XỬ LÝ LƯU ẢNH (NẾU CÓ) — lưu lên Cloudinary để không mất khi redeploy
        if (imageFile != null && imageFile.Length > 0)
        {
            var imageUrl = await _cloudinary.UploadImageAsync(imageFile, "posts");
            if (!string.IsNullOrEmpty(imageUrl))
            {
                post.ImageUrl = imageUrl;
            }
        }

        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đăng bài thành công!";
        return RedirectToAction("Index");
    }

    // Thêm bình luận
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> AddComment(int postId, string content)
    {
        // #18: kiểm duyệt bình luận blog (regex + AI)
        var mod = await _moderation.DeepCheckAsync(content);
        if (!mod.Ok)
        {
            TempData["Error"] = mod.Reason;
            return RedirectToAction("Detail", new { id = postId });
        }

        if (string.IsNullOrWhiteSpace(content)) return RedirectToAction("Detail", new { id = postId });

        var user = await _userManager.GetUserAsync(User);
        var comment = new Comment
        {
            PostId = postId,
            AuthorId = user!.Id,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();
        return RedirectToAction("Detail", new { id = postId });
    }

    // Action Like/Unlike (Toggle)
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ToggleLike(int postId)
    {
        var userId = _userManager.GetUserId(User);
        var existing = await _db.PostLikes
            .FirstOrDefaultAsync(pl => pl.PostId == postId && pl.UserId == userId);

        if (existing != null)
            _db.PostLikes.Remove(existing);      // Unlike
        else
            _db.PostLikes.Add(new PostLike
            {     // Like
                PostId = postId,
                UserId = userId!,
                CreatedAt = DateTime.UtcNow
            });

        await _db.SaveChangesAsync();

        var likeCount = await _db.PostLikes.CountAsync(pl => pl.PostId == postId);
        var isLiked = existing == null; // Sau toggle: nếu vừa xóa = false, thêm = true

        return Json(new { likeCount, isLiked });
    }

    // Xóa bài viết
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await _db.Posts.FindAsync(id);

        if (post == null)
        {
            TempData["Error"] = "Không tìm thấy bài viết.";
            return RedirectToAction(nameof(Index));
        }

        var currentUserId = _userManager.GetUserId(User);

        // Kiểm tra quyền: Chỉ tác giả hoặc Admin mới được xóa
        if (post.AuthorId != currentUserId && !User.IsInRole("Admin"))
        {
            TempData["Error"] = "Bạn không có quyền xóa bài viết này.";
            return RedirectToAction("Detail", new { id = id });
        }

        try
        {
            _db.Posts.Remove(post);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Đã xóa bài viết thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            // Trong trường hợp có lỗi (ví dụ: lỗi khóa ngoại do DB chưa cascade delete)
            TempData["Error"] = "Đã xảy ra lỗi khi xóa bài viết. Vui lòng thử lại.";
            return RedirectToAction("Detail", new { id = id });
        }
    }
}