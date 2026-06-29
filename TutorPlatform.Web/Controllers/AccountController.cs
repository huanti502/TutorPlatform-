using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
    private readonly Cloudinary _cloudinary;

    public AccountController(UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        AppDbContext db,
        IWebHostEnvironment env,
        IConfiguration config)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _env = env;

        var account = new Account(
            config["Cloudinary:CloudName"],
            config["Cloudinary:ApiKey"],
            config["Cloudinary:ApiSecret"]
        );
        _cloudinary = new Cloudinary(account);
    }

    // ── Helper: Upload file lên Cloudinary ──────────────────────────
    private async Task<string?> UploadToCloudinaryAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0) return null;
        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = $"tutorplatform/{folder}",
            Transformation = new Transformation().Width(400).Height(400).Crop("fill").Gravity("face")
        };
        var result = await _cloudinary.UploadAsync(uploadParams);
        return result.SecureUrl?.ToString();
    }

    private async Task<string?> UploadFileToCloudinaryAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0) return null;
        using var stream = file.OpenReadStream();
        var ext = Path.GetExtension(file.FileName).ToLower();

        if (ext == ".pdf")
        {
            var rawParams = new RawUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = $"tutorplatform/{folder}"
            };
            var result = await _cloudinary.UploadAsync(rawParams);
            return result.SecureUrl?.ToString();
        }
        else
        {
            var imgParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = $"tutorplatform/{folder}"
            };
            var result = await _cloudinary.UploadAsync(imgParams);
            return result.SecureUrl?.ToString();
        }
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

        // Upload avatar lên Cloudinary (cả Student và Tutor)
        if (avatarFile != null && avatarFile.Length > 0)
        {
            var avatarUrl = await UploadToCloudinaryAsync(avatarFile, "avatars");
            if (avatarUrl != null)
            {
                user.AvatarUrl = avatarUrl;
                await _userManager.UpdateAsync(user);
            }
        }

        if (model.Role == "Tutor")
        {
            try
            {
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

                // Upload ảnh xác minh danh tính
                if (facePhotoFile != null && facePhotoFile.Length > 0)
                {
                    var faceUrl = await UploadToCloudinaryAsync(facePhotoFile, "verifications");
                    if (faceUrl != null)
                        _db.Certificates.Add(new Certificate
                        {
                            TutorProfileId = profile.Id,
                            Title = "Ảnh xác minh danh tính",
                            FilePath = faceUrl,
                            FileType = "image",
                            Type = CertificateType.FacePhoto
                        });
                }

                // Upload bằng cấp/chứng chỉ
                for (int i = 0; i < certFiles.Count; i++)
                {
                    var file = certFiles[i];
                    if (file == null || file.Length == 0) continue;
                    var title = (i < certTitles.Count) ? certTitles[i] : string.Empty;
                    var ext = Path.GetExtension(file.FileName).ToLower();
                    var url = await UploadFileToCloudinaryAsync(file, "certificates");
                    if (url != null)
                        _db.Certificates.Add(new Certificate
                        {
                            TutorProfileId = profile.Id,
                            Title = string.IsNullOrWhiteSpace(title) ? $"Bằng cấp {i + 1}" : title,
                            FilePath = url,
                            FileType = ext == ".pdf" ? "pdf" : "image",
                            Type = CertificateType.Degree
                        });
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

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null && user.IsLocked)
        {
            ModelState.AddModelError("", "Tài khoản của bạn đã bị khoá. Vui lòng liên hệ Admin.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
            return RedirectToAction("Index", "Home");

        ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
        return View(model);
    }

    // ══════════════════════════════════════════════════════
    //  ĐĂNG NHẬP BẰNG GOOGLE
    // ══════════════════════════════════════════════════════

    // Bấm nút "Đăng nhập với Google" → chuyển hướng sang Google
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    // Google gọi lại sau khi người dùng đồng ý
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (remoteError != null)
        {
            TempData["Error"] = "Lỗi từ dịch vụ đăng nhập: " + remoteError;
            return RedirectToAction("Login");
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            TempData["Error"] = "Không lấy được thông tin đăng nhập từ Google.";
            return RedirectToAction("Login");
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);

        // 1) Đã từng đăng nhập Google trước đó → đăng nhập luôn
        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            if (email != null)
            {
                var existing = await _userManager.FindByEmailAsync(email);
                if (existing != null && existing.IsLocked)
                {
                    await _signInManager.SignOutAsync();
                    TempData["Error"] = "Tài khoản của bạn đã bị khoá. Vui lòng liên hệ Admin.";
                    return RedirectToAction("Login");
                }
            }
            return RedirectToLocal(returnUrl);
        }

        // 2) Chưa liên kết → cần email để xử lý tiếp
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Tài khoản Google không cung cấp email nên không thể đăng nhập.";
            return RedirectToAction("Login");
        }

        // 3) Tìm theo email; nếu chưa có thì tạo mới (mặc định vai trò Học viên)
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            var fullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;
            user = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                Role = "Student",
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                TempData["Error"] = "Không tạo được tài khoản: " +
                    string.Join(", ", createResult.Errors.Select(e => e.Description));
                return RedirectToAction("Login");
            }

            await _userManager.AddToRoleAsync(user, "Student");
        }

        if (user.IsLocked)
        {
            TempData["Error"] = "Tài khoản của bạn đã bị khoá. Vui lòng liên hệ Admin.";
            return RedirectToAction("Login");
        }

        // Liên kết đăng nhập Google với tài khoản rồi đăng nhập
        await _userManager.AddLoginAsync(user, info);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToLocal(returnUrl);
    }

    // Helper: chỉ chuyển hướng tới URL nội bộ (chống open-redirect)
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    // Trang báo không đủ quyền (tránh 404 khi bị từ chối truy cập)
    [HttpGet]
    public IActionResult AccessDenied() => View();

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

    // ── Upload avatar cho user đã đăng nhập ─────────────────────────
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAvatar(IFormFile avatarFile)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        if (avatarFile == null || avatarFile.Length == 0)
        {
            TempData["Error"] = "Vui lòng chọn ảnh.";
            return RedirectBack();
        }

        var url = await UploadToCloudinaryAsync(avatarFile, "avatars");
        if (url != null)
        {
            user.AvatarUrl = url;
            await _userManager.UpdateAsync(user);
            TempData["Success"] = "Cập nhật ảnh đại diện thành công!";
        }
        else
        {
            TempData["Error"] = "Upload ảnh thất bại. Vui lòng thử lại.";
        }

        return RedirectBack();
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

    private IActionResult RedirectBack()
    {
        var referer = Request.Headers["Referer"].ToString();
        return string.IsNullOrEmpty(referer)
            ? RedirectToAction("Index", "Home")
            : Redirect(referer);
    }
}