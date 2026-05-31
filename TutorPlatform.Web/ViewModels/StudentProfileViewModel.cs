using System.ComponentModel.DataAnnotations;

namespace TutorPlatform.Web.ViewModels;



public class StudentProfileViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? PhoneNumber { get; set; }

    public string? Address { get; set; }

    // Email chỉ dùng để hiển thị, không cho phép sửa ở trang này
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public IFormFile? AvatarFile { get; set; }
}
