namespace TutorPlatform.Web.ViewModels;

public class CheckoutViewModel
{
    public int BookingId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public double Hours { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal BaseAmount { get; set; }   // tiền gốc trước giảm
}
