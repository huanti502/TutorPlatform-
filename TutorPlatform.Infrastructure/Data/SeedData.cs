using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;

namespace TutorPlatform.Web.Data;

public static class SeedData
{
    public static async Task SeedAllAsync(
        AppDbContext db,
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        if (await db.TutorProfiles.CountAsync() >= 5) return;

        // ── ROLES ─────────────────────────────────────────
        foreach (var role in new[] { "Admin", "Tutor", "Student" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        // ── ADMIN ACCOUNT ─────────────────────────────────
        if (await userManager.FindByEmailAsync("admin@tutor.com") == null)
        {
            var admin = new AppUser
            {
                UserName = "admin@tutor.com",
                Email = "admin@tutor.com",
                FullName = "Quản trị viên",
                Role = "Admin",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };
            await userManager.CreateAsync(admin, "Admin@123");
            await userManager.AddToRoleAsync(admin, "Admin");
        }
        // ── SUBJECTS ──────────────────────────────────────
        // FIX LỖI 1: Không dùng AnyAsync() nữa, thay bằng upsert chỉ môn còn thiếu
        // Lý do: HasData migration chỉ seed 5 môn (1-5), gia sư dùng môn 6-10
        // → TutorSubject FK vào môn 6-10 không tồn tại → lỗi FOREIGN KEY
        var existingSubjectIds = await db.Subjects.Select(s => s.Id).ToListAsync();
        var allSubjectDefs = new[]
        {
            new Subject { Id = 1,  Name = "Toán học",     Level = "THPT",       IsActive = true },
            new Subject { Id = 2,  Name = "Tiếng Anh",    Level = "THPT",       IsActive = true },
            new Subject { Id = 3,  Name = "Vật lý",       Level = "THPT",       IsActive = true },
            new Subject { Id = 4,  Name = "Hóa học",      Level = "THPT",       IsActive = true },
            new Subject { Id = 5,  Name = "Lập trình",    Level = "Đại học",    IsActive = true },
            new Subject { Id = 6,  Name = "Ngữ văn",      Level = "THPT",       IsActive = true },
            new Subject { Id = 7,  Name = "Lịch sử",      Level = "THPT",       IsActive = true },
            new Subject { Id = 8,  Name = "Tiếng Nhật",   Level = "Đại học",    IsActive = true },
            new Subject { Id = 9,  Name = "Toán cao cấp", Level = "Đại học",    IsActive = true },
            new Subject { Id = 10, Name = "IELTS",         Level = "Chứng chỉ", IsActive = true }
        };
        var missingSubjects = allSubjectDefs.Where(s => !existingSubjectIds.Contains(s.Id)).ToList();
        if (missingSubjects.Any())
        {
            db.Subjects.AddRange(missingSubjects);
            await db.SaveChangesAsync();
        }

        // ── BADGES ────────────────────────────────────────
        if (!await db.Badges.AnyAsync())
        {
            db.Badges.AddRange(
                new Badge { Id = 1, Name = "Khởi đầu", Description = "Hoàn thành buổi học đầu tiên", Icon = "🌱", Color = "#4CAF50", Type = BadgeType.Sessions, RequiredCount = 1 },
                new Badge { Id = 2, Name = "Đang lên", Description = "Hoàn thành 10 buổi học", Icon = "⚡", Color = "#2196F3", Type = BadgeType.Sessions, RequiredCount = 10 },
                new Badge { Id = 3, Name = "Chuyên nghiệp", Description = "Hoàn thành 50 buổi học", Icon = "🎯", Color = "#9C27B0", Type = BadgeType.Sessions, RequiredCount = 50 },
                new Badge { Id = 4, Name = "Huyền thoại", Description = "Hoàn thành 100 buổi học", Icon = "🏆", Color = "#FFD700", Type = BadgeType.Sessions, RequiredCount = 100 },
                new Badge { Id = 5, Name = "Được yêu thích", Description = "Nhận được 5 lượt đánh giá", Icon = "⭐", Color = "#FF9800", Type = BadgeType.Reviews, RequiredCount = 5 },
                new Badge { Id = 6, Name = "Top Rated", Description = "Nhận được 20 lượt đánh giá", Icon = "🌟", Color = "#FF5722", Type = BadgeType.Reviews, RequiredCount = 20 },
                new Badge { Id = 7, Name = "Xuất sắc", Description = "Điểm TB ≥ 4.5 sao", Icon = "💎", Color = "#00BCD4", Type = BadgeType.Rating, RequiredCount = 45 },
                new Badge { Id = 8, Name = "Hoàn hảo", Description = "Điểm TB ≥ 4.8 sao", Icon = "👑", Color = "#E91E63", Type = BadgeType.Rating, RequiredCount = 48 },
                new Badge { Id = 9, Name = "Đa năng", Description = "Dạy từ 3 môn học trở lên", Icon = "📚", Color = "#607D8B", Type = BadgeType.Subjects, RequiredCount = 3 },
                new Badge { Id = 10, Name = "Triệu phú", Description = "Tích lũy doanh thu 1,000,000 VNĐ", Icon = "💰", Color = "#795548", Type = BadgeType.Revenue, RequiredCount = 1000 }
            );
            await db.SaveChangesAsync();
        }
      

   
        // ════════════════════════════════════════════════════
        //  TẠO GIA SƯ
        // ════════════════════════════════════════════════════
        var tutorData = new[]
        {
            new {
                Email="nguyenvanminh@tutor.com", Pass="Tutor@123",
                FullName="Nguyễn Văn Minh", Phone="0901234567",
                Address="Cầu Giấy, Hà Nội",
                Education="Thạc sĩ Toán học - ĐH Sư phạm Hà Nội",
                Exp=8, Area="Cầu Giấy, Đống Đa, Ba Đình - Hà Nội",
                Rate=200000m, Mode="Both",
                Bio="Thầy Minh có 8 năm kinh nghiệm luyện thi Toán THPT và Đại học. Phương pháp dạy rõ ràng, logic, giúp học sinh hiểu bản chất thay vì học thuộc lòng. Đã có hơn 150 học sinh đậu đại học các trường top.",
                Subjects=new[]{1,3,9}, Approved=true
            },
            new {
                Email="tranthihuong@tutor.com", Pass="Tutor@123",
                FullName="Trần Thị Hương", Phone="0912345678",
                Address="Bình Thạnh, TP.HCM",
                Education="Cử nhân Ngôn ngữ Anh - ĐH Ngoại ngữ Hà Nội, IELTS 8.0",
                Exp=6, Area="Bình Thạnh, Gò Vấp, Phú Nhuận - TP.HCM",
                Rate=250000m, Mode="Online",
                Bio="Cô Hương chuyên dạy Tiếng Anh giao tiếp và luyện thi IELTS. Đã đạt IELTS 8.0 và có kinh nghiệm 6 năm giúp học viên cải thiện điểm từ 5.0 lên 7.0+.",
                Subjects=new[]{2,10}, Approved=true
            },
            new {
                Email="lephantrung@tutor.com", Pass="Tutor@123",
                FullName="Lê Phan Trung", Phone="0923456789",
                Address="Thủ Đức, TP.HCM",
                Education="Kỹ sư CNTT - ĐH Bách Khoa TP.HCM, 5 năm kinh nghiệm Dev",
                Exp=5, Area="TP.HCM (Online toàn quốc)",
                Rate=300000m, Mode="Online",
                Bio="Anh Trung là Senior Developer với 5 năm thực chiến. Chuyên dạy lập trình Web (C#, .NET, React, NodeJS). Học xong có thể làm việc ngay.",
                Subjects=new[]{5,9}, Approved=true
            },
            new {
                Email="phamthilan@tutor.com", Pass="Tutor@123",
                FullName="Phạm Thị Lan", Phone="0934567890",
                Address="Thanh Xuân, Hà Nội",
                Education="Tiến sĩ Hóa học - ĐH Khoa học Tự nhiên Hà Nội",
                Exp=12, Area="Thanh Xuân, Hoàng Mai, Hà Đông - Hà Nội",
                Rate=180000m, Mode="Offline",
                Bio="Cô Lan là Tiến sĩ Hóa học với 12 năm giảng dạy. Chuyên luyện thi Hóa THPTQG, thi học sinh giỏi. Nhiều học sinh đoạt giải Olympic Hóa quốc gia.",
                Subjects=new[]{4,3}, Approved=true
            },
            new {
                Email="vuthanhlong@tutor.com", Pass="Tutor@123",
                FullName="Vũ Thành Long", Phone="0945678901",
                Address="Hải Châu, Đà Nẵng",
                Education="Cử nhân Sư phạm Ngữ văn - ĐH Đà Nẵng",
                Exp=7, Area="Hải Châu, Thanh Khê, Sơn Trà - Đà Nẵng",
                Rate=150000m, Mode="Both",
                Bio="Thầy Long chuyên dạy Ngữ văn THPT và luyện thi THPTQG. Nhiều học sinh đạt 8-9 điểm Văn kỳ thi THPTQG.",
                Subjects=new[]{6,7}, Approved=true
            },
            new {
                Email="hoangminhtu@tutor.com", Pass="Tutor@123",
                FullName="Hoàng Minh Tú", Phone="0956789012",
                Address="Cần Thơ",
                Education="Thạc sĩ Vật lý - ĐH Cần Thơ",
                Exp=9, Area="Ninh Kiều, Bình Thủy - Cần Thơ & Online",
                Rate=160000m, Mode="Both",
                Bio="Thầy Tú có 9 năm kinh nghiệm dạy Vật lý. Biết cách truyền đạt các khái niệm phức tạp một cách đơn giản. Tỷ lệ học sinh vào ngành kỹ thuật top cao.",
                Subjects=new[]{3,1}, Approved=true
            },
            new {
                Email="nguyenthimai@tutor.com", Pass="Tutor@123",
                FullName="Nguyễn Thị Mai", Phone="0967890123",
                Address="Đống Đa, Hà Nội",
                Education="Cử nhân Tiếng Nhật - ĐH Hà Nội, N1 JLPT",
                Exp=4, Area="Hà Nội & Online toàn quốc",
                Rate=220000m, Mode="Online",
                Bio="Cô Mai đạt chứng chỉ N1 JLPT, từng làm việc 2 năm tại Nhật. Dạy tiếng Nhật từ mất gốc đến N1. Nhiều học viên đã đi du học và làm việc tại Nhật.",
                Subjects=new[]{8,2}, Approved=true
            },
            new {
                Email="tranvanduc@tutor.com", Pass="Tutor@123",
                FullName="Trần Văn Đức", Phone="0978901234",
                Address="Long Biên, Hà Nội",
                Education="Kỹ sư Toán - Tin ĐH Bách Khoa Hà Nội",
                Exp=3, Area="Long Biên, Gia Lâm - Hà Nội & Online",
                Rate=140000m, Mode="Both",
                Bio="Anh Đức tốt nghiệp loại giỏi ĐH Bách Khoa. Nhiệt tình, kiên nhẫn với học sinh yếu. Giá hợp lý, phù hợp học sinh cần học bổ sung.",
                Subjects=new[]{1,5}, Approved=true
            },
        };

        var tutorProfiles = new List<TutorProfile>();
        var tutorUsers = new List<AppUser>();

        foreach (var td in tutorData)
        {
            if (await userManager.FindByEmailAsync(td.Email) != null) continue;

            var user = new AppUser
            {
                UserName = td.Email,
                Email = td.Email,
                FullName = td.FullName,
                PhoneNumber = td.Phone,   // FIX LỖI 2: gán số điện thoại
                Address = td.Address,
                Role = "Tutor",
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(30, 365))
            };
            await userManager.CreateAsync(user, td.Pass);
            await userManager.AddToRoleAsync(user, "Tutor");

            // FIX LỖI 2: Gán đầy đủ TeachingMode, ExperienceYears, TeachingArea
            var profile = new TutorProfile
            {
                UserId = user.Id,
                Education = td.Education,
                Bio = td.Bio,
                HourlyRate = td.Rate,
                IsApproved = td.Approved,
                TeachingMode = td.Mode,   // ← thiếu ở bản cũ
                ExperienceYears = td.Exp,    // ← thiếu ở bản cũ
                TeachingArea = td.Area    // ← thiếu ở bản cũ
            };

            db.TutorProfiles.Add(profile);
            await db.SaveChangesAsync(); // lưu trước để có profile.Id

            foreach (var sid in td.Subjects)
                db.TutorSubjects.Add(new TutorSubject { TutorProfileId = profile.Id, SubjectId = sid });

            db.TutorAvailabilities.Add(new TutorAvailability
            {
                TutorProfileId = profile.Id,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeSpan(18, 0, 0),
                EndTime = new TimeSpan(20, 0, 0)
            });

            await db.SaveChangesAsync();

            tutorProfiles.Add(profile);
            tutorUsers.Add(user);
        }

        // ════════════════════════════════════════════════════
        //  TẠO HỌC VIÊN
        // ════════════════════════════════════════════════════
        var studentData = new[]
        {
            new { Email="hocvien1@gmail.com",  Pass="Student@123", FullName="Nguyễn Thị Bảo Châu",  Phone="0321234567", Address="Cầu Giấy, Hà Nội"        },
            new { Email="hocvien2@gmail.com",  Pass="Student@123", FullName="Trần Minh Khoa",        Phone="0332345678", Address="Bình Thạnh, TP.HCM"       },
            new { Email="hocvien3@gmail.com",  Pass="Student@123", FullName="Lê Thị Thu Hà",         Phone="0343456789", Address="Đống Đa, Hà Nội"          },
            new { Email="hocvien4@gmail.com",  Pass="Student@123", FullName="Phạm Quốc Bảo",         Phone="0354567890", Address="Thủ Đức, TP.HCM"          },
            new { Email="hocvien5@gmail.com",  Pass="Student@123", FullName="Hoàng Thị Yến Nhi",     Phone="0365678901", Address="Hải Châu, Đà Nẵng"        },
            new { Email="hocvien6@gmail.com",  Pass="Student@123", FullName="Vũ Đình Anh Tuấn",      Phone="0376789012", Address="Long Biên, Hà Nội"         },
            new { Email="hocvien7@gmail.com",  Pass="Student@123", FullName="Đặng Thị Mỹ Linh",      Phone="0387890123", Address="Ninh Kiều, Cần Thơ"        },
            new { Email="hocvien8@gmail.com",  Pass="Student@123", FullName="Bùi Thanh Hải",          Phone="0398901234", Address="Thanh Xuân, Hà Nội"       },
            new { Email="hocvien9@gmail.com",  Pass="Student@123", FullName="Ngô Thị Lan Anh",        Phone="0309012345", Address="Gò Vấp, TP.HCM"           },
            new { Email="hocvien10@gmail.com", Pass="Student@123", FullName="Đinh Văn Mạnh",          Phone="0310123456", Address="Sơn Trà, Đà Nẵng"         },
        };

        var studentUsers = new List<AppUser>();
        foreach (var sd in studentData)
        {
            if (await userManager.FindByEmailAsync(sd.Email) != null) continue;
            var user = new AppUser
            {
                UserName = sd.Email,
                Email = sd.Email,
                FullName = sd.FullName,
                PhoneNumber = sd.Phone,
                Address = sd.Address,
                Role = "Student",
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(10, 200)),
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, sd.Pass);
            await userManager.AddToRoleAsync(user, "Student");
            studentUsers.Add(user);
        }

        if (!tutorProfiles.Any() || !studentUsers.Any())
        {
            tutorProfiles = await db.TutorProfiles.Include(t => t.TutorSubjects).Take(8).ToListAsync();
            studentUsers = await db.Users.Where(u => u.Role == "Student").Take(10).ToListAsync();
            tutorUsers = new List<AppUser>();
            foreach (var p in tutorProfiles)
            {
                var u = await userManager.FindByIdAsync(p.UserId);
                if (u != null) tutorUsers.Add(u);
            }
        }

        // ════════════════════════════════════════════════════
        //  TẠO BOOKING + REVIEW
        // ════════════════════════════════════════════════════
        var rng = Random.Shared;
        var comments = new[]
        {
            "Thầy/Cô dạy rất dễ hiểu, tôi tiến bộ rõ rệt sau vài buổi học!",
            "Phương pháp giảng dạy rất hay, bài tập phong phú và sát đề thi.",
            "Giải thích rõ ràng từng bước, kiên nhẫn với học sinh. Rất hài lòng!",
            "Nội dung học được chuẩn bị kỹ, đúng trọng tâm cần ôn thi.",
            "Thầy/Cô nhiệt tình, luôn giải đáp thắc mắc kể cả ngoài giờ học.",
            "Học với Thầy/Cô tiến bộ nhanh hơn hẳn tự học. Rất recommend!",
            "Giá cả hợp lý, chất lượng dạy tốt. Sẽ tiếp tục học dài hạn.",
            "Buổi học đầu tiên đã thấy rõ sự khác biệt so với học thêm ở trường.",
            "Thầy/Cô có nhiều mẹo hay giúp tôi nhớ công thức nhanh hơn.",
            "Phong cách dạy sinh động, không nhàm chán, học rất vào."
        };

        int bookingIdCounter = 1;

        for (int ti = 0; ti < Math.Min(tutorProfiles.Count, 8); ti++)
        {
            var tutor = tutorProfiles[ti];
            int bookingCount = rng.Next(8, 16);
            var subId = tutor.TutorSubjects.FirstOrDefault()?.SubjectId ?? 1;

            for (int bi = 0; bi < bookingCount; bi++)
            {
                var student = studentUsers[rng.Next(studentUsers.Count)];
                int daysAgo = rng.Next(-7, 90);
                var startTime = DateTime.Now.AddDays(-daysAgo).Date
                    .AddHours(rng.Next(17, 20)).AddMinutes(rng.Next(0, 2) * 30);
                var endTime = startTime.AddHours(rng.Next(1, 3));

                string status;
                string? meetingRoomId = null;
                if (daysAgo > 14)
                    status = rng.Next(10) < 8 ? "Completed" : "Cancelled";
                else if (daysAgo > 2)
                {
                    status = rng.Next(10) < 7 ? "Confirmed" : "Completed";
                    meetingRoomId = $"TutorPlatform-{bookingIdCounter}-{Guid.NewGuid().ToString("N")[..8]}";
                }
                else if (daysAgo < 0)
                    status = "Pending";
                else
                    status = rng.Next(2) == 0 ? "Confirmed" : "Pending";

                if (status == "Confirmed")
                    meetingRoomId = $"TutorPlatform-{bookingIdCounter}-{Guid.NewGuid().ToString("N")[..8]}";

                var modes = new[] { "Online", "Offline" };
                var notes = new[] {
                    "Cần ôn tập phần đạo hàm và tích phân",
                    "Muốn luyện Speaking và Writing IELTS",
                    "Học lập trình Web từ cơ bản",
                    "Cần giải bài tập Hóa hữu cơ",
                    "Ôn thi THPTQG phần Vật lý sóng",
                    "Luyện viết văn nghị luận xã hội",
                    null
                };

                var booking = new Booking
                {
                    StudentId = student.Id,
                    TutorProfileId = tutor.Id,
                    SubjectId = subId,
                    StartTime = startTime,
                    EndTime = endTime,
                    Status = status,
                    TeachingMode = tutor.TeachingMode == "Both"
                        ? modes[rng.Next(modes.Length)]
                        : (tutor.TeachingMode == "Online" ? "Online" : "Offline"),
                    Note = notes[rng.Next(notes.Length)],
                    MeetingRoomId = meetingRoomId,
                    CreatedAt = startTime.AddDays(-rng.Next(1, 7))
                };
                db.Bookings.Add(booking);
                await db.SaveChangesAsync();
                bookingIdCounter++;

                if (status == "Completed" && rng.Next(10) < 8)
                {
                    int rating = rng.Next(10) < 7 ? rng.Next(4, 6) : rng.Next(3, 5);
                    db.Reviews.Add(new Review
                    {
                        StudentId = student.Id,
                        TutorProfileId = tutor.Id,
                        BookingId = booking.Id,
                        Rating = rating,
                        Comment = comments[rng.Next(comments.Length)],
                        CreatedAt = endTime.AddHours(rng.Next(1, 48))
                    });
                }
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════
        //  TẠO BADGES CHO GIA SƯ
        // ════════════════════════════════════════════════════
        var allBadges = await db.Badges.ToListAsync();
        var allProfiles = await db.TutorProfiles
            .Include(t => t.Bookings)
            .Include(t => t.ReceivedReviews)
            .Include(t => t.TutorSubjects)
            .ToListAsync();

        foreach (var profile in allProfiles)
        {
            var completedCount = profile.Bookings.Count(b => b.Status == "Completed");
            var reviewCount = profile.ReceivedReviews.Count;
            var avgRating = reviewCount > 0 ? profile.ReceivedReviews.Average(r => r.Rating) : 0;
            var subjectCount = profile.TutorSubjects.Count;
            var revenue = profile.Bookings
                .Where(b => b.Status == "Completed")
                .Sum(b => (decimal)(b.EndTime - b.StartTime).TotalHours * profile.HourlyRate);

            foreach (var badge in allBadges)
            {
                bool qualified = badge.Type switch
                {
                    BadgeType.Sessions => completedCount >= badge.RequiredCount,
                    BadgeType.Reviews => reviewCount >= badge.RequiredCount,
                    BadgeType.Subjects => subjectCount >= badge.RequiredCount,
                    BadgeType.Revenue => revenue >= badge.RequiredCount * 1000,
                    BadgeType.Rating => reviewCount >= 3 && avgRating * 10 >= badge.RequiredCount,
                    _ => false
                };
                if (!qualified) continue;

                var exists = await db.TutorBadges.AnyAsync(tb =>
                    tb.TutorProfileId == profile.Id && tb.BadgeId == badge.Id);
                if (!exists)
                    db.TutorBadges.Add(new TutorBadge
                    {
                        TutorProfileId = profile.Id,
                        BadgeId = badge.Id,
                        EarnedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 30))
                    });
            }
        }
        await db.SaveChangesAsync();

        // ════════════════════════════════════════════════════
        //  TẠO BLOG POSTS
        // ════════════════════════════════════════════════════
        if (tutorUsers.Count >= 7 && !await db.Posts.AnyAsync())
        {
            var posts = new[]
            {
                new Post {
                    AuthorId    = tutorUsers[0].Id,
                    Title       = "5 phương pháp học Toán hiệu quả cho kỳ thi THPTQG",
                    Summary     = "Chia sẻ từ gia sư 8 năm kinh nghiệm: Cách học Toán hiệu quả, tránh học vẹt và tăng điểm nhanh.",
                    Content     = "Nhiều học sinh học Toán theo kiểu thuộc lòng công thức mà không hiểu bản chất...\n\n**1. Hiểu gốc rễ trước khi giải bài**\nThay vì vội vào bài tập, hãy dành 20% thời gian hiểu lý thuyết thật sâu...\n\n**2. Phân loại dạng bài**\nMỗi chương có 4-6 dạng bài chuẩn. Hãy tổng hợp và thuộc phương pháp giải từng dạng...\n\n**3. Luyện đề theo thời gian thực**\nLàm đề thi thử trong đúng 90 phút, không tra cứu...\n\n**4. Review sai lầm hàng ngày**\nMỗi tối dành 10 phút xem lại các bài làm sai trong ngày...\n\n**5. Học theo nhóm nhỏ**\nGiải thích cho bạn bè là cách học hiệu quả nhất.",
                    IsPublished = true,
                    CreatedAt   = DateTime.Now.AddDays(-45)
                },
                new Post {
                    AuthorId    = tutorUsers[1].Id,
                    Title       = "Roadmap học IELTS từ 0 lên 7.0 trong 6 tháng",
                    Summary     = "Lộ trình học IELTS chi tiết theo từng tháng, kèm tài liệu và app học miễn phí.",
                    Content     = "IELTS 7.0 không phải mục tiêu xa vời nếu bạn có lộ trình đúng...\n\n**Tháng 1-2: Xây nền tảng**\n- Từ vựng: Học 10 từ/ngày với Anki\n- Ngữ pháp: Cambridge Grammar in Use\n\n**Tháng 3-4: Luyện kỹ năng**\n- Reading: Cambridge IELTS 14, 15, 16\n- Writing: Practice Task 1 và Task 2 mỗi ngày\n\n**Tháng 5-6: Mock Test**\n- Làm full test mỗi tuần\n- Đăng ký thi thật",
                    IsPublished = true,
                    CreatedAt   = DateTime.Now.AddDays(-32)
                },
                new Post {
                    AuthorId    = tutorUsers[2].Id,
                    Title       = "Học lập trình Web năm 2025: Nên bắt đầu từ đâu?",
                    Summary     = "Hướng dẫn toàn diện cho người mới muốn học lập trình Web, từ HTML đến Full-stack.",
                    Content     = "Lập trình Web là ngành hot nhất hiện tại với mức lương hấp dẫn...\n\n**Bước 1: HTML & CSS (2-4 tuần)**\n**Bước 2: JavaScript (4-8 tuần)**\n**Bước 3: Chọn hướng**\n- Frontend: React, Vue\n- Backend: NodeJS, C# .NET\n**Bước 4: Làm Project thực tế**",
                    IsPublished = true,
                    CreatedAt   = DateTime.Now.AddDays(-20)
                },
                new Post {
                    AuthorId    = tutorUsers[3].Id,
                    Title       = "Bí quyết học Hóa hữu cơ không bao giờ quên",
                    Summary     = "TS Hóa học chia sẻ cách học Hóa hữu cơ một lần nhớ mãi, không cần học thuộc.",
                    Content     = "Hóa hữu cơ khiến nhiều học sinh sợ vì quá nhiều phản ứng cần nhớ...\n\n**Nguyên tắc 1: Hiểu cơ chế phản ứng**\n**Nguyên tắc 2: Vẽ sơ đồ tư duy**\n**Nguyên tắc 3: Học từ ví dụ thực tế**\n**Nguyên tắc 4: Luyện bài tập nhận biết**",
                    IsPublished = true,
                    CreatedAt   = DateTime.Now.AddDays(-15)
                },
                new Post {
                    AuthorId    = tutorUsers[4].Id,
                    Title       = "Cách viết mở bài - kết bài Văn nghị luận gây ấn tượng",
                    Summary     = "Bí quyết viết mở bài sáng tạo và kết bài đọng lại cảm xúc.",
                    Content     = "Mở bài và kết bài chiếm 15-20% điểm bài văn...\n\n**3 kiểu mở bài hiệu quả:**\n1. Mở bài bằng câu hỏi tu từ\n2. Mở bài bằng trích dẫn\n3. Mở bài bằng tình huống giả định\n\n**Kết bài gây đọng lại:**\nMở ra hướng suy nghĩ mới cho người đọc.",
                    IsPublished = true,
                    CreatedAt   = DateTime.Now.AddDays(-10)
                },
                new Post {
                    AuthorId    = tutorUsers[1].Id,
                    Title       = "Top 10 app học tiếng Anh miễn phí tốt nhất 2025",
                    Summary     = "Tổng hợp các app học tiếng Anh hiệu quả nhất, từ người mới đến nâng cao.",
                    Content     = "Học tiếng Anh không nhất thiết phải tốn nhiều tiền...\n\n1. Duolingo\n2. Anki\n3. BBC Learning English\n4. Elsa Speak\n5. Cake\n6. HelloTalk\n7. Coursera\n8. TED\n9. Grammarly\n10. DeepL",
                    IsPublished = true,
                    CreatedAt   = DateTime.Now.AddDays(-5)
                },
                new Post {
                    AuthorId    = tutorUsers[6].Id,
                    Title       = "Tại sao nên học tiếng Nhật và cơ hội việc làm năm 2025",
                    Summary     = "Thị trường lao động Nhật Bản đang mở rộng, cơ hội rất lớn cho người biết tiếng Nhật.",
                    Content     = "Nhật Bản đang thiếu lao động và đang tìm kiếm nhân lực từ Việt Nam...\n\n**Lý do học tiếng Nhật:**\n- Lương kỹ sư IT tại Nhật: 80-150 triệu/tháng\n- Chi phí du học hợp lý hơn Mỹ/Úc\n\n**Lộ trình:** N5 → N4 → N3 → N2 → N1",
                    IsPublished = true,
                    CreatedAt   = DateTime.Now.AddDays(-3)
                },
            };

            foreach (var post in posts)
                db.Posts.Add(post);
            await db.SaveChangesAsync();

            var allPosts = await db.Posts.ToListAsync();
            var commentTexts = new[]
            {
                "Bài viết rất hữu ích! Cảm ơn thầy/cô đã chia sẻ.",
                "Tôi đang áp dụng phương pháp này và thấy hiệu quả hơn hẳn.",
                "Cho tôi hỏi thêm về tài liệu để luyện tập được không ạ?",
                "Bài này nên pin lại để đọc lại nhiều lần!",
                "Chia sẻ rất thực tế và chi tiết. Cảm ơn nhiều!",
                "Tôi đã thử theo phương pháp này được 2 tuần, điểm tăng rõ rệt.",
                "Thông tin rất bổ ích cho kỳ thi sắp tới của tôi.",
                "Mong thầy/cô ra thêm nhiều bài viết như thế này!"
            };

            foreach (var post in allPosts)
            {
                int numComments = rng.Next(2, 6);
                for (int c = 0; c < numComments; c++)
                {
                    var commenter = studentUsers[rng.Next(studentUsers.Count)];
                    db.Comments.Add(new Comment
                    {
                        PostId = post.Id,
                        AuthorId = commenter.Id,
                        Content = commentTexts[rng.Next(commentTexts.Length)],
                        CreatedAt = post.CreatedAt.AddDays(rng.Next(1, 10))
                    });
                }

                var likedBy = studentUsers.OrderBy(_ => rng.Next()).Take(rng.Next(3, 9));
                foreach (var liker in likedBy)
                {
                    var existsLike = await db.PostLikes.AnyAsync(pl =>
                        pl.PostId == post.Id && pl.UserId == liker.Id);
                    if (!existsLike)
                        db.PostLikes.Add(new PostLike
                        {
                            PostId = post.Id,
                            UserId = liker.Id,
                            CreatedAt = post.CreatedAt.AddDays(rng.Next(1, 5))
                        });
                }
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════
        //  TẠO TIN NHẮN
        // ════════════════════════════════════════════════════
        if (!await db.Messages.AnyAsync())
        {
            var msgTexts = new[,]
            {
                { "Xin chào thầy/cô, tôi muốn đặt lịch học Toán ạ!", "" },
                { "", "Chào em! Thầy/Cô đang có lịch trống vào tối thứ 2 và thứ 4. Em muốn học thời gian nào?" },
                { "Dạ em muốn học tối thứ 4 lúc 19h ạ. Học phí một buổi bao nhiêu ạ?", "" },
                { "", "Em học 2 tiếng là 400,000đ nhé. Thầy/Cô sẽ chuẩn bị bài tập theo đề cương của em." },
                { "Dạ vâng ạ. Em đã đặt lịch rồi ạ, thầy/cô xác nhận giúp em nhé!", "" },
                { "", "Thầy/Cô xác nhận rồi em nhé. Hẹn gặp em tối thứ 4!" },
            };

            for (int ti = 0; ti < Math.Min(tutorUsers.Count, 4); ti++)
            {
                var tUser2 = tutorUsers[ti];
                for (int si = 0; si < Math.Min(3, studentUsers.Count); si++)
                {
                    var sUser = studentUsers[(ti + si) % studentUsers.Count];
                    for (int mi = 0; mi < msgTexts.GetLength(0); mi++)
                    {
                        var studentMsg = msgTexts[mi, 0];
                        var tutorMsg = msgTexts[mi, 1];
                        var baseTime = DateTime.UtcNow.AddDays(-rng.Next(1, 14)).AddHours(-rng.Next(1, 48));

                        if (!string.IsNullOrEmpty(studentMsg))
                            db.Messages.Add(new Message { SenderId = sUser.Id, ReceiverId = tUser2.Id, Content = studentMsg, SentAt = baseTime.AddMinutes(mi * 5), IsRead = true });

                        if (!string.IsNullOrEmpty(tutorMsg))
                            db.Messages.Add(new Message { SenderId = tUser2.Id, ReceiverId = sUser.Id, Content = tutorMsg, SentAt = baseTime.AddMinutes(mi * 5 + 2), IsRead = mi < msgTexts.GetLength(0) - 1 });
                    }
                }
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════
        //  TẠO THÔNG BÁO
        // ════════════════════════════════════════════════════
        if (!await db.Notifications.AnyAsync())
        {
            var notifData = new List<(string UserId, string Title, string Content, string Link)>();

            foreach (var student in studentUsers.Take(5))
            {
                notifData.Add((student.Id, "✅ Lịch học được xác nhận!", "Gia sư đã xác nhận lịch học của bạn. Chuẩn bị đồ dùng và đúng giờ nhé!", "/Booking/MyBookings"));
                notifData.Add((student.Id, "⏰ Nhắc lịch học ngày mai", "Bạn có buổi học lúc 19:00 ngày mai. Đừng quên chuẩn bị bài nhé!", "/Booking/MyBookings"));
                notifData.Add((student.Id, "💡 AI gợi ý gia sư mới cho bạn", "Dựa trên lịch sử học tập, chúng tôi tìm được 3 gia sư phù hợp hơn cho bạn!", "/TutorSearch/Search"));
            }

            foreach (var tUser3 in tutorUsers.Take(4))
            {
                notifData.Add((tUser3.Id, "📅 Yêu cầu đặt lịch mới!", "Một học viên vừa gửi yêu cầu đặt lịch học. Hãy xem và xác nhận sớm!", "/Booking/TutorRequests"));
                notifData.Add((tUser3.Id, "⭐ Bạn vừa nhận đánh giá mới!", "Học viên đã để lại đánh giá 5 sao cho buổi học. Xem ngay!", "/Tutor/Dashboard"));
                notifData.Add((tUser3.Id, "🏆 Huy hiệu mới: Gia sư xuất sắc!", "Chúc mừng! Bạn vừa đạt huy hiệu 'Được yêu thích' với 5 lượt đánh giá.", "/Tutor/Dashboard"));
            }

            foreach (var (uid, title, content, link) in notifData)
                db.Notifications.Add(new Notification
                {
                    UserId = uid,
                    Title = title,
                    Content = content,
                    Link = link,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(1, 72))
                });

            await db.SaveChangesAsync();
        }

        Console.WriteLine("✅ Seed data hoàn thành!");
    }
}