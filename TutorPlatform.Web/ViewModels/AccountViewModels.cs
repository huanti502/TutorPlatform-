using System.ComponentModel.DataAnnotations;

namespace TutorPlatform.Web.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    public string Password { get; set; } = string.Empty;

    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Student";
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    public string NewPassword { get; set; } = string.Empty;

    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
public class TutorProfileFormViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập trình độ học vấn")]
    public string Education { get; set; } = string.Empty;

    [Range(0, 50)]
    public int ExperienceYears { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập khu vực dạy")]
    public string TeachingArea { get; set; } = string.Empty;

    [Range(0, 10000000)]
    public decimal HourlyRate { get; set; }

    public string TeachingMode { get; set; } = "Both";
    public string? Bio { get; set; }
    public List<int> SelectedSubjectIds { get; set; } = new();

    // Avatar
    public string? AvatarUrl { get; set; }
    public IFormFile? AvatarFile { get; set; }

    // Vị trí dạy trên bản đồ
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
public class TwoFactorLoginViewModel
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập mã")]
    [System.ComponentModel.DataAnnotations.Display(Name = "Mã xác thực")]
    public string Code { get; set; } = string.Empty;

    public bool RememberMachine { get; set; }
}

public class EnableAuthenticatorViewModel
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập mã")]
    [System.ComponentModel.DataAnnotations.Display(Name = "Mã xác thực")]
    public string Code { get; set; } = string.Empty;

    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
}
