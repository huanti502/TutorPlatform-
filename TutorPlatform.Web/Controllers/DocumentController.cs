using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Services;
using UglyToad.PdfPig;

namespace TutorPlatform.Web.Controllers;

[Authorize]
public class DocumentController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly AIService _ai; // Bổ sung AIService

    // Cập nhật Constructor để nhận thêm AIService
    public DocumentController(
        AppDbContext db,
        UserManager<AppUser> userManager,
        IWebHostEnvironment env,
        AIService ai)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
        _ai = ai;
    }

    // Danh sách tài liệu (public + của mình)
    public async Task<IActionResult> Index(string? search)
    {
        var user = await _userManager.GetUserAsync(User);

        var query = _db.Documents
            .Include(d => d.Uploader)
            .Where(d => d.IsPublic || d.UploaderId == user!.Id)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(d =>
                d.Title.Contains(search) || d.Description.Contains(search));

        ViewBag.Search = search;
        return View(await query.OrderByDescending(d => d.CreatedAt).ToListAsync());
    }

    // Form upload
    [HttpGet]
    public IActionResult Upload() => View();

    // Xử lý upload
    [HttpPost]
    public async Task<IActionResult> Upload(
        string title, string description, bool isPublic,
        int? bookingId, IFormFile file)
    {
        // Validate
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Vui lòng chọn file.";
            return View();
        }

        // Chỉ cho phép các định dạng an toàn
        var allowed = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx",
                              ".xls", ".xlsx", ".txt", ".jpg", ".jpeg", ".png" };
        var ext = Path.GetExtension(file.FileName).ToLower();
        if (!allowed.Contains(ext))
        {
            TempData["Error"] = "Định dạng không hỗ trợ. Cho phép: PDF, Word, PowerPoint, Excel, ảnh.";
            return View();
        }

        // Giới hạn 20MB
        if (file.Length > 20 * 1024 * 1024)
        {
            TempData["Error"] = "File quá lớn (tối đa 20MB).";
            return View();
        }

        // Lưu file
        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "documents");
        Directory.CreateDirectory(uploadsFolder);

        var uniqueName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadsFolder, uniqueName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var user = await _userManager.GetUserAsync(User);

        // Xác định loại file
        var fileType = ext switch
        {
            ".pdf" => "pdf",
            ".doc" or ".docx" => "word",
            ".ppt" or ".pptx" => "powerpoint",
            ".xls" or ".xlsx" => "excel",
            ".jpg" or ".jpeg" or ".png" => "image",
            _ => "other"
        };

        _db.Documents.Add(new Document
        {
            Title = title,
            Description = description,
            FilePath = $"/uploads/documents/{uniqueName}",
            FileName = file.FileName,
            FileType = fileType,
            FileSize = file.Length,
            UploaderId = user!.Id,
            BookingId = bookingId,
            IsPublic = isPublic,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = "Tải lên thành công!";
        return RedirectToAction("Index");
    }

    // Download + đếm lượt tải
    public async Task<IActionResult> Download(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var doc = await _db.Documents.FindAsync(id);

        if (doc == null) return NotFound();
        if (!doc.IsPublic && doc.UploaderId != user!.Id) return Forbid();

        var fullPath = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/'));
               if (!System.IO.File.Exists(fullPath))
        {
            TempData["Error"] = "File không còn tồn tại trên server (server đã restart). Vui lòng yêu cầu người upload tải lại.";
            return RedirectToAction("Index");
        }

        doc.DownloadCount++;
        await _db.SaveChangesAsync();

        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        return File(bytes, "application/octet-stream", doc.FileName);
    }

    // Xóa (chỉ người upload)
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var doc = await _db.Documents.FindAsync(id);

        if (doc == null) return NotFound();
        if (doc.UploaderId != user!.Id) return Forbid();

        // Xóa file vật lý
        var fullPath = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/'));
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);

        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã xóa tài liệu.";
        return RedirectToAction("Index");
    }

    // ===== BỔ SUNG: XỬ LÝ TÓM TẮT TÀI LIỆU BẰNG AI =====
    [HttpPost]
    public async Task<IActionResult> Summarize(int id)
    {
        var doc = await _db.Documents.FindAsync(id);
        if (doc == null) return NotFound();

        var fullPath = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/'));
        if (!System.IO.File.Exists(fullPath))
        {
            TempData["Error"] = "Không tìm thấy file!";
            return RedirectToAction("Index");
        }

        string fileContent;

        // Đọc nội dung file tuỳ theo loại
        if (doc.FileType == "pdf")
        {
            try
            {
                // Đọc text từ PDF (yêu cầu cài đặt gói NuGet: PdfPig)
                using var pdfDoc = UglyToad.PdfPig.PdfDocument.Open(fullPath);
                var pages = pdfDoc.GetPages().Take(10); // Giới hạn đọc tối đa 10 trang đầu
                fileContent = string.Join("\n", pages.Select(p => p.Text));
            }
            catch
            {
                TempData["Error"] = "Không thể đọc nội dung từ file PDF này.";
                return RedirectToAction("Index");
            }
        }
        else if (doc.FileType == "word")
        {
            // Đọc text thuần từ docx
            fileContent = $"[Tài liệu Word: {doc.FileName}] — " +
                          "Nội dung file Word. Vui lòng chuyển sang PDF để AI đọc tốt hơn.";
        }
        else if (doc.FileType == "image")
        {
            TempData["Error"] = "AI chưa hỗ trợ tóm tắt ảnh, chỉ hỗ trợ PDF và Word.";
            return RedirectToAction("Index");
        }
        else
        {
            fileContent = await System.IO.File.ReadAllTextAsync(fullPath);
        }

        // Giới hạn 4000 ký tự để tránh vượt ngưỡng Token giới hạn của Model AI
        if (fileContent.Length > 4000)
            fileContent = fileContent[..4000] + "\n...[nội dung còn lại đã bị cắt]";

        var systemPrompt = """
            Bạn là trợ lý AI giáo dục chuyên phân tích tài liệu học tập.
            Hãy đọc nội dung tài liệu và tạo ra:
            1. 📋 TÓM TẮT: 3-5 câu ngắn gọn về nội dung chính
            2. 🎯 ĐIỂM CHÍNH: Liệt kê 5-7 ý quan trọng nhất (dạng bullet)
            3. ❓ CÂU HỎI ÔN TẬP: 3 câu hỏi giúp học viên kiểm tra hiểu biết
            4. 💡 GỢI Ý HỌC: Một lời khuyên ngắn gọn về cách học tài liệu này
            Trả lời bằng tiếng Việt, rõ ràng, phù hợp với học sinh/sinh viên.
            Dùng emoji để dễ đọc. Nếu nội dung không phải tài liệu học tập,
            hãy tóm tắt nội dung thông thường.
            """;

        try
        {
            var summary = await _ai.ChatAsync(
                systemPrompt,
                $"Tài liệu: {doc.Title}\n\nNội dung:\n{fileContent}",
                maxTokens: 1200
            );

            doc.AISummary = summary;
            doc.SummarizedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["Success"] = "AI đã tóm tắt tài liệu thành công!";
        }
        catch
        {
            TempData["Error"] = "AI đang bận, vui lòng thử lại sau!";
        }

        return RedirectToAction("Detail", new { id });
    }

    // ===== BỔ SUNG: TRANG XEM CHI TIẾT TÀI LIỆU VÀ AI SUMMARY =====
    public async Task<IActionResult> Detail(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var doc = await _db.Documents
            .Include(d => d.Uploader)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc == null) return NotFound();
        if (!doc.IsPublic && doc.UploaderId != user?.Id) return Forbid();

        return View(doc);
    }
}