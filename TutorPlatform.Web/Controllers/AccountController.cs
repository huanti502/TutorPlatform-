using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.ViewModels;

namespace TutorPlatform.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public AccountController(UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        AppDbContext db,
        IWebHostEnvironment env)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _env = env;
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        var avatarFile = Request.Form.Files["avatarFile"];
        var facePhotoFile = Request.Form.Files["facePhotoFile"];
        var certFiles = Request.Form.Files.GetFiles("certFiles").ToList();
        var certTitles = Request.Form["certTitles"].ToList();

        if (!ModelState.IsValid) return View(model);

        if (model.Role == "Tutor")
        {
            if (avatarFile == null || avatarFile.Length == 0)
                ModelState.AddModelError("", "Vui lòng upload ảnh đại diện.");
            if (facePhotoFile == null || facePhotoFile.Length == 0)
                ModelState.AddModelError("", "Vui lòng upload ảnh xác minh danh tính.");
            if (!certFiles.Any(f => f.Length > 0))
                ModelState.AddModelError("", "Vui lòng upload ít nhất 1 bằng cấp hoặc chứng chỉ.");
            if (!ModelState.IsValid) return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("", "Email này đã được đăng ký. Vui lòng dùng email khác.");
            return View(model);
        }

        var user = new AppUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            Role = model.Role,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, model.Role);

        if (model.Role == "Tutor")
        {
            try
            {
                var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");

                string? avatarUrl = null;
                if (avatarFile != null && avatarFile.Length > 0)
                {
                    var avatarFolder = Path.Combine(uploadsPath, "avatars");
                    Directory.CreateDirectory(avatarFolder);
                    var ext = Path.GetExtension(avatarFile.FileName);
                    var fileName = $"avatar_{user.Id}{ext}";
                    using (var stream = new FileStream(Path.Combine(avatarFolder, fileName), FileMode.Create))
                        await avatarFile.CopyToAsync(stream);
                    avatarUrl = $"/uploads/avatars/{fileName}";
                    user.AvatarUrl = avatarUrl;
                    await _userManager.UpdateAsync(user);
                }

                var profile = new TutorProfile
                {
                    UserId = user.Id,
                    IsApproved = false,
                    Education = string.Empty,
                    TeachingArea = string.Empty,
                    HourlyRate = 0,
                    TeachingMode = "Both"
                };
                _db.TutorProfiles.Add(profile);
                await _db.SaveChangesAsync();

                if (facePhotoFile != null && facePhotoFile.Length > 0)
                {
                    var faceFolder = Path.Combine(uploadsPath, "verifications");
                    Directory.CreateDirectory(faceFolder);
                    var ext = Path.GetExtension(facePhotoFile.FileName);
                    var name = $"face_{profile.Id}_{Guid.NewGuid()}{ext}";
                    using (var stream = new FileStream(Path.Combine(faceFolder, name), FileMode.Create))
                        await facePhotoFile.CopyToAsync(stream);
                    _db.Certificates.Add(new Certificate
                    {
                        TutorProfileId = profile.Id,
                        Title = "Ảnh xác minh danh tính",
                        FilePath = $"/uploads/verifications/{name}",
                        FileType = "image",
                        Type = CertificateType.FacePhoto
                    });
                }

                if (certFiles.Any())
                {
                    var certFolder = Path.Combine(uploadsPath, "certificates");
                    Directory.CreateDirectory(certFolder);
                    for (int i = 0; i < certFiles.Count; i++)
                    {
                        var file = certFiles[i];
                        if (file == null || file.Length == 0) continue;
                        var title = (i < certTitles.Count) ? certTitles[i] : string.Empty;
                        var ext = Path.GetExtension(file.FileName);
                        var name = $"cert_{profile.Id}_{Guid.NewGuid()}{ext}";
                        using (var stream = new FileStream(Path.Combine(certFolder, name), FileMode.Create))
                            await file.CopyToAsync(stream);
                        _db.Certificates.Add(new Certificate
                        {
                            TutorProfileId = profile.Id,
                            Title = string.IsNullOrWhiteSpace(title) ? $"Bằng cấp {i + 1}" : title,
                            FilePath = $"/uploads/certificates/{name}",
                            FileType = ext.ToLower() == ".pdf" ? "pdf" : "image",
                            Type = CertificateType.Degree
                        });
                    }
                }

                await _db.SaveChangesAsync();
                await _signInManager.SignInAsync(user, isPersistent: false);
                TempData["Success"] = "Đăng ký thành công! Hồ sơ đang chờ Admin xét duyệt.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                await _userManager.DeleteAsync(user);
                ModelState.AddModelError("", $"Lỗi khi tạo hồ sơ: {ex.Message}. Vui lòng thử lại.");
                return View(model);
            }
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // ── Kiểm tra tài khoản có bị khoá không TRƯỚC khi đăng nhập ──
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null && user.IsLocked)
        {
            ModelState.AddModelError("", "Tài khoản của bạn đã bị khoá. Vui lòng liên hệ Admin để được hỗ trợ.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
            return RedirectToAction("Index", "Home");

        ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            TempData["Error"] = "Email không tồn tại trong hệ thống.";
            return View();
        }
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetLink = Url.Action("ResetPassword", "Account", new { token, email }, Request.Scheme);
        TempData["ResetLink"] = resetLink;
        TempData["Success"] = "Đã tạo link đặt lại mật khẩu!";
        return View();
    }

    [HttpGet]
    public IActionResult ResetPassword(string token, string email)
    {
        ViewBag.Token = token;
        ViewBag.Email = email;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(string token, string email,
       string newPassword, string confirmPassword)
    {
        if (string.IsNullOrEmpty(email)) email = Request.Form["email"].ToString();
        if (string.IsNullOrEmpty(token)) token = Request.Form["token"].ToString();
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            TempData["Error"] = "Link đặt lại mật khẩu không hợp lệ.";
            return RedirectToAction("ForgotPassword");
        }
        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "Mật khẩu xác nhận không khớp.";
            ViewBag.Token = token; ViewBag.Email = email;
            return View();
        }
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            TempData["Error"] = "Không tìm thấy tài khoản.";
            ViewBag.Token = token; ViewBag.Email = email;
            return View();
        }
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (result.Succeeded)
        {
            TempData["Success"] = "Đặt lại mật khẩu thành công! Hãy đăng nhập.";
            return RedirectToAction("Login");
        }
        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);
        ViewBag.Token = token; ViewBag.Email = email;
        return View();
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        var user = await _userManager.GetUserAsync(User);
        var unread = await _db.Notifications
            .Where(n => n.UserId == user!.Id && !n.IsRead).ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã đánh dấu tất cả là đã đọc.";
        return RedirectToAction("Notifications");
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View();

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");
        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (result.Succeeded)
        {
            TempData["Success"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Index", "Home");
        }
        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);
        return View(model);
    }

    [Authorize]
    public async Task<IActionResult> Notifications()
    {
        var user = await _userManager.GetUserAsync(User);
        var notifications = await _db.Notifications
            .Where(n => n.UserId == user!.Id)
            .OrderByDescending(n => n.CreatedAt).ToListAsync();
        foreach (var n in notifications.Where(n => !n.IsRead)) n.IsRead = true;
        await _db.SaveChangesAsync();
        return View(notifications);
    }
}