using System.ComponentModel.DataAnnotations;

namespace TutorPlatform.Web.ViewModels;

public class AvailabilityViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn thứ trong tuần")]
    public DayOfWeek DayOfWeek { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ bắt đầu")]
    public TimeSpan StartTime { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giờ kết thúc")]
    public TimeSpan EndTime { get; set; }
}