using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Services;

/// <summary>
/// Tổng hợp số liệu thống kê hệ thống và xuất ra file Excel (.xlsx) cho Admin.
/// </summary>
public class ReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Gom toàn bộ số liệu thống kê trong khoảng [from, to] để hiển thị preview hoặc xuất Excel.
    /// </summary>
    public async Task<ReportData> BuildReportDataAsync(DateTime from, DateTime to)
    {
        // Chuẩn hoá: tính cả ngày "to"
        var fromUtc = from.Date;
        var toUtc = to.Date.AddDays(1).AddTicks(-1);

        // ── KPI tổng quan ──────────────────────────────────────────────
        var totalUsers = await _db.Users.CountAsync();
        var totalStudents = await _db.Users.CountAsync(u => u.Role == "Student");
        var totalTutors = await _db.TutorProfiles.CountAsync();
        var pendingTutors = await _db.TutorProfiles.CountAsync(t => !t.IsApproved);
        var totalReviews = await _db.Reviews.CountAsync();

        // Booking trong khoảng thời gian
        var bookingsInRange = await _db.Bookings
            .Include(b => b.TutorProfile)
            .Include(b => b.Subject)
            .Include(b => b.Student)
            .Where(b => b.CreatedAt >= fromUtc && b.CreatedAt <= toUtc)
            .ToListAsync();

        var completed = bookingsInRange.Where(b => b.Status == "Completed").ToList();

        decimal RevenueOf(Core.Models.Booking b) =>
            (decimal)(b.EndTime - b.StartTime).TotalHours * (b.TutorProfile?.HourlyRate ?? 0);

        var totalRevenue = completed.Sum(RevenueOf);

        // ── Doanh thu theo tháng ───────────────────────────────────────
        var monthly = completed
            .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
            .Select(g => new MonthlyRow
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Count = g.Count(),
                Revenue = g.Sum(RevenueOf)
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();

        // ── Lịch đặt theo trạng thái ───────────────────────────────────
        var byStatus = bookingsInRange
            .GroupBy(b => b.Status)
            .Select(g => new StatusRow { Status = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        // ── Top gia sư (theo rating, dựa trên booking trong kỳ) ─────────
        var tutorList = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.ReceivedReviews)
            .Where(t => t.IsApproved)
            .ToListAsync();

        var completedByTutor = completed
            .GroupBy(b => b.TutorProfileId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Revenue: g.Sum(RevenueOf)));

        var topTutors = tutorList
            .Select(t =>
            {
                completedByTutor.TryGetValue(t.Id, out var stat);
                return new TutorRow
                {
                    Name = t.User?.FullName ?? "(không tên)",
                    Email = t.User?.Email ?? "",
                    Area = t.TeachingArea,
                    Sessions = stat.Count,
                    Revenue = stat.Revenue,
                    AvgRating = t.ReceivedReviews.Any()
                        ? Math.Round(t.ReceivedReviews.Average(r => r.Rating), 2)
                        : 0
                };
            })
            .OrderByDescending(t => t.AvgRating)
            .ThenByDescending(t => t.Sessions)
            .Take(10)
            .ToList();

        // ── Mức độ phổ biến môn học (số gia sư dạy) ─────────────────────
        var tutorSubjects = await _db.TutorSubjects
            .Include(ts => ts.Subject)
            .ToListAsync();

        var subjects = tutorSubjects
            .GroupBy(ts => ts.Subject?.Name ?? "Khác")
            .Select(g => new SubjectRow { Subject = g.Key, TutorCount = g.Count() })
            .OrderByDescending(x => x.TutorCount)
            .ToList();

        return new ReportData
        {
            From = from.Date,
            To = to.Date,
            TotalUsers = totalUsers,
            TotalStudents = totalStudents,
            TotalTutors = totalTutors,
            PendingTutors = pendingTutors,
            TotalReviews = totalReviews,
            TotalBookings = bookingsInRange.Count,
            CompletedBookings = completed.Count,
            TotalRevenue = totalRevenue,
            Monthly = monthly,
            ByStatus = byStatus,
            TopTutors = topTutors,
            Subjects = subjects
        };
    }

    /// <summary>
    /// Sinh file Excel (.xlsx) dạng byte[] từ dữ liệu thống kê.
    /// </summary>
    public async Task<byte[]> ExportExcelAsync(DateTime from, DateTime to)
    {
        var data = await BuildReportDataAsync(from, to);

        using var wb = new XLWorkbook();

        var accent = XLColor.FromHtml("#4F46E5"); // tím chủ đạo
        var headerBg = XLColor.FromHtml("#EEF2FF");

        // Hàm tạo tiêu đề sheet
        void Title(IXLWorksheet ws, string text, int span)
        {
            var range = ws.Range(1, 1, 1, span).Merge();
            range.Value = text;
            range.Style.Font.Bold = true;
            range.Style.Font.FontSize = 15;
            range.Style.Font.FontColor = accent;
            ws.Row(1).Height = 24;

            var sub = ws.Range(2, 1, 2, span).Merge();
            sub.Value = $"Kỳ báo cáo: {data.From:dd/MM/yyyy} – {data.To:dd/MM/yyyy}  ·  Xuất lúc {DateTime.Now:dd/MM/yyyy HH:mm}";
            sub.Style.Font.FontSize = 9;
            sub.Style.Font.FontColor = XLColor.Gray;
        }

        void StyleHeaderRow(IXLWorksheet ws, int row, int firstCol, int lastCol)
        {
            var hr = ws.Range(row, firstCol, row, lastCol);
            hr.Style.Font.Bold = true;
            hr.Style.Fill.BackgroundColor = headerBg;
            hr.Style.Font.FontColor = accent;
            hr.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            hr.Style.Border.BottomBorderColor = accent;
        }

        // ════════════ Sheet 1: Tổng quan ════════════
        var s1 = wb.Worksheets.Add("Tổng quan");
        Title(s1, "BÁO CÁO THỐNG KÊ HỆ THỐNG GIA SƯ", 2);
        int r = 4;
        StyleHeaderRow(s1, r, 1, 2);
        s1.Cell(r, 1).Value = "Chỉ số";
        s1.Cell(r, 2).Value = "Giá trị";
        r++;

        void Kpi(string label, object value, string? numberFormat = null)
        {
            s1.Cell(r, 1).Value = label;
            s1.Cell(r, 2).Value = XLCellValue.FromObject(value);
            if (numberFormat != null) s1.Cell(r, 2).Style.NumberFormat.Format = numberFormat;
            r++;
        }

        Kpi("Tổng người dùng", data.TotalUsers);
        Kpi("Học viên", data.TotalStudents);
        Kpi("Gia sư", data.TotalTutors);
        Kpi("Gia sư chờ duyệt", data.PendingTutors);
        Kpi("Tổng lịch đặt (trong kỳ)", data.TotalBookings);
        Kpi("Buổi học hoàn thành (trong kỳ)", data.CompletedBookings);
        Kpi("Tổng đánh giá", data.TotalReviews);
        Kpi("Doanh thu ước tính (VNĐ)", data.TotalRevenue, "#,##0");
        s1.Columns().AdjustToContents();
        s1.Column(1).Width = Math.Max(s1.Column(1).Width, 34);
        s1.Column(2).Width = Math.Max(s1.Column(2).Width, 20);

        // ════════════ Sheet 2: Doanh thu theo tháng ════════════
        var s2 = wb.Worksheets.Add("Doanh thu theo tháng");
        Title(s2, "DOANH THU THEO THÁNG", 3);
        r = 4;
        StyleHeaderRow(s2, r, 1, 3);
        s2.Cell(r, 1).Value = "Tháng";
        s2.Cell(r, 2).Value = "Số buổi hoàn thành";
        s2.Cell(r, 3).Value = "Doanh thu (VNĐ)";
        r++;
        foreach (var m in data.Monthly)
        {
            s2.Cell(r, 1).Value = $"{m.Month:00}/{m.Year}";
            s2.Cell(r, 2).Value = m.Count;
            s2.Cell(r, 3).Value = m.Revenue;
            s2.Cell(r, 3).Style.NumberFormat.Format = "#,##0";
            r++;
        }
        if (data.Monthly.Count > 0)
        {
            s2.Cell(r, 1).Value = "TỔNG";
            s2.Cell(r, 2).Value = data.Monthly.Sum(m => m.Count);
            s2.Cell(r, 3).Value = data.Monthly.Sum(m => m.Revenue);
            s2.Cell(r, 3).Style.NumberFormat.Format = "#,##0";
            s2.Range(r, 1, r, 3).Style.Font.Bold = true;
            s2.Range(r, 1, r, 3).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        }
        s2.Columns().AdjustToContents();

        // ════════════ Sheet 3: Lịch đặt theo trạng thái ════════════
        var s3 = wb.Worksheets.Add("Lịch đặt theo trạng thái");
        Title(s3, "LỊCH ĐẶT THEO TRẠNG THÁI", 2);
        r = 4;
        StyleHeaderRow(s3, r, 1, 2);
        s3.Cell(r, 1).Value = "Trạng thái";
        s3.Cell(r, 2).Value = "Số lượng";
        r++;
        foreach (var st in data.ByStatus)
        {
            s3.Cell(r, 1).Value = VietnameseStatus(st.Status);
            s3.Cell(r, 2).Value = st.Count;
            r++;
        }
        s3.Columns().AdjustToContents();

        // ════════════ Sheet 4: Top gia sư ════════════
        var s4 = wb.Worksheets.Add("Top gia sư");
        Title(s4, "TOP GIA SƯ", 6);
        r = 4;
        StyleHeaderRow(s4, r, 1, 6);
        s4.Cell(r, 1).Value = "#";
        s4.Cell(r, 2).Value = "Họ tên";
        s4.Cell(r, 3).Value = "Email";
        s4.Cell(r, 4).Value = "Khu vực";
        s4.Cell(r, 5).Value = "Số buổi (kỳ)";
        s4.Cell(r, 6).Value = "Rating TB";
        r++;
        int rank = 1;
        foreach (var t in data.TopTutors)
        {
            s4.Cell(r, 1).Value = rank++;
            s4.Cell(r, 2).Value = t.Name;
            s4.Cell(r, 3).Value = t.Email;
            s4.Cell(r, 4).Value = t.Area;
            s4.Cell(r, 5).Value = t.Sessions;
            s4.Cell(r, 6).Value = t.AvgRating;
            r++;
        }
        s4.Columns().AdjustToContents();

        // ════════════ Sheet 5: Môn học phổ biến ════════════
        var s5 = wb.Worksheets.Add("Môn học phổ biến");
        Title(s5, "MỨC ĐỘ PHỔ BIẾN MÔN HỌC", 2);
        r = 4;
        StyleHeaderRow(s5, r, 1, 2);
        s5.Cell(r, 1).Value = "Môn học";
        s5.Cell(r, 2).Value = "Số gia sư dạy";
        r++;
        foreach (var sub in data.Subjects)
        {
            s5.Cell(r, 1).Value = sub.Subject;
            s5.Cell(r, 2).Value = sub.TutorCount;
            r++;
        }
        s5.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string VietnameseStatus(string status) => status switch
    {
        "Pending" => "Chờ duyệt",
        "Confirmed" => "Đã xác nhận",
        "Completed" => "Hoàn thành",
        "Cancelled" => "Đã huỷ",
        "Rejected" => "Bị từ chối",
        _ => status
    };
}

// ── DTO ────────────────────────────────────────────────────────────────
public class ReportData
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TotalUsers { get; set; }
    public int TotalStudents { get; set; }
    public int TotalTutors { get; set; }
    public int PendingTutors { get; set; }
    public int TotalReviews { get; set; }
    public int TotalBookings { get; set; }
    public int CompletedBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public List<MonthlyRow> Monthly { get; set; } = new();
    public List<StatusRow> ByStatus { get; set; } = new();
    public List<TutorRow> TopTutors { get; set; } = new();
    public List<SubjectRow> Subjects { get; set; } = new();
}

public class MonthlyRow { public int Year; public int Month; public int Count; public decimal Revenue; }
public class StatusRow { public string Status = ""; public int Count; }
public class TutorRow { public string Name = ""; public string Email = ""; public string Area = ""; public int Sessions; public decimal Revenue; public double AvgRating; }
public class SubjectRow { public string Subject = ""; public int TutorCount; }