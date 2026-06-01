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

        var rng = Random.Shared;

        // ════════════════════════════════════════════════════
        //  ROLES
        // ════════════════════════════════════════════════════
        foreach (var role in new[] { "Admin", "Tutor", "Student" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // ════════════════════════════════════════════════════
        //  ADMIN
        // ════════════════════════════════════════════════════
        if (await userManager.FindByEmailAsync("admin@tutor.com") == null)
        {
            var admin = new AppUser
            {
                UserName = "admin@tutor.com", Email = "admin@tutor.com",
                FullName = "Quản trị viên", Role = "Admin",
                EmailConfirmed = true, CreatedAt = DateTime.UtcNow
            };
            await userManager.CreateAsync(admin, "Admin@123");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // ════════════════════════════════════════════════════
        //  SUBJECTS
        // ════════════════════════════════════════════════════
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
            new Subject { Id = 10, Name = "IELTS",        Level = "Chứng chỉ",  IsActive = true }
        };
        var missing = allSubjectDefs.Where(s => !existingSubjectIds.Contains(s.Id)).ToList();
        if (missing.Any()) { db.Subjects.AddRange(missing); await db.SaveChangesAsync(); }

        // ════════════════════════════════════════════════════
        //  BADGES
        // ════════════════════════════════════════════════════
        if (!await db.Badges.AnyAsync())
        {
            db.Badges.AddRange(
                new Badge { Id = 1,  Name = "Khởi đầu",       Description = "Hoàn thành buổi học đầu tiên",          Icon = "🌱", Color = "#4CAF50", Type = BadgeType.Sessions, RequiredCount = 1    },
                new Badge { Id = 2,  Name = "Đang lên",        Description = "Hoàn thành 10 buổi học",                Icon = "⚡", Color = "#2196F3", Type = BadgeType.Sessions, RequiredCount = 10   },
                new Badge { Id = 3,  Name = "Chuyên nghiệp",   Description = "Hoàn thành 50 buổi học",                Icon = "🎯", Color = "#9C27B0", Type = BadgeType.Sessions, RequiredCount = 50   },
                new Badge { Id = 4,  Name = "Huyền thoại",     Description = "Hoàn thành 100 buổi học",               Icon = "🏆", Color = "#FFD700", Type = BadgeType.Sessions, RequiredCount = 100  },
                new Badge { Id = 5,  Name = "Được yêu thích",  Description = "Nhận được 5 lượt đánh giá",             Icon = "⭐", Color = "#FF9800", Type = BadgeType.Reviews,  RequiredCount = 5    },
                new Badge { Id = 6,  Name = "Top Rated",       Description = "Nhận được 20 lượt đánh giá",            Icon = "🌟", Color = "#FF5722", Type = BadgeType.Reviews,  RequiredCount = 20   },
                new Badge { Id = 7,  Name = "Gia sư xuất sắc", Description = "Điểm TB >= 4.5 sao (it nhat 5 danh gia)", Icon = "💎", Color = "#00BCD4", Type = BadgeType.Rating, RequiredCount = 45   },
                new Badge { Id = 8,  Name = "Hoàn hảo",        Description = "Điểm TB >= 4.8 sao (it nhat 5 danh gia)", Icon = "👑", Color = "#E91E63", Type = BadgeType.Rating, RequiredCount = 48   },
                new Badge { Id = 9,  Name = "Đa năng",         Description = "Dạy từ 3 môn học trở lên",             Icon = "📚", Color = "#607D8B", Type = BadgeType.Subjects, RequiredCount = 3    },
                new Badge { Id = 10, Name = "Triệu phú",       Description = "Tích lũy doanh thu 1,000,000 VND",      Icon = "💰", Color = "#795548", Type = BadgeType.Revenue,  RequiredCount = 1000 }
            );
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════
        //  TẠO 12 GIA SƯ
        // ════════════════════════════════════════════════════
        var tutorData = new[]
        {
            new { Email="nguyenvanminh@tutor.com",  Pass="Tutor@123", FullName="Nguyen Van Minh",   Phone="0901234567", Address="Cau Giay, Ha Noi",       Education="Thac si Toan hoc - DH Su pham Ha Noi",                    Exp=8,  Area="Cau Giay, Dong Da, Ba Dinh - Ha Noi",   Rate=200000m, Mode="Both",    Bio="Thay Minh co 8 nam kinh nghiem luyen thi Toan THPT va Dai hoc. Phuong phap day ro rang, logic, giup hoc sinh hieu ban chat thay vi hoc thuoc long. Da co hon 150 hoc sinh dau dai hoc cac truong top.", Subjects=new[]{1,3,9} },
            new { Email="tranthihuong@tutor.com",   Pass="Tutor@123", FullName="Tran Thi Huong",    Phone="0912345678", Address="Binh Thanh, TP.HCM",      Education="Cu nhan Ngon ngu Anh - DH Ngoai ngu, IELTS 8.0",          Exp=6,  Area="Binh Thanh, Go Vap, Phu Nhuan - TP.HCM", Rate=250000m, Mode="Online",  Bio="Co Huong chuyen day Tieng Anh giao tiep va luyen thi IELTS. Da dat IELTS 8.0 va co kinh nghiem 6 nam giup hoc vien cai thien diem tu 5.0 len 7.0+.", Subjects=new[]{2,10} },
            new { Email="lephantrung@tutor.com",    Pass="Tutor@123", FullName="Le Phan Trung",     Phone="0923456789", Address="Thu Duc, TP.HCM",          Education="Ky su CNTT - DH Bach Khoa TP.HCM, 5 nam Dev",             Exp=5,  Area="TP.HCM - Online toan quoc",              Rate=300000m, Mode="Online",  Bio="Anh Trung la Senior Developer voi 5 nam thuc chien. Chuyen day lap trinh Web (C#, .NET, React, NodeJS). Hoc xong co the lam viec ngay.", Subjects=new[]{5,9} },
            new { Email="phamthilan@tutor.com",     Pass="Tutor@123", FullName="Pham Thi Lan",      Phone="0934567890", Address="Thanh Xuan, Ha Noi",       Education="Tien si Hoa hoc - DH Khoa hoc Tu nhien Ha Noi",            Exp=12, Area="Thanh Xuan, Hoang Mai, Ha Dong - Ha Noi", Rate=180000m, Mode="Offline", Bio="Co Lan la Tien si Hoa hoc voi 12 nam giang day. Chuyen luyen thi Hoa THPTQG, thi hoc sinh gioi. Nhieu hoc sinh doat giai Olympic Hoa quoc gia.", Subjects=new[]{4,3} },
            new { Email="vuthanhlong@tutor.com",    Pass="Tutor@123", FullName="Vu Thanh Long",     Phone="0945678901", Address="Hai Chau, Da Nang",        Education="Cu nhan Su pham Ngu van - DH Da Nang",                    Exp=7,  Area="Hai Chau, Thanh Khe, Son Tra - Da Nang", Rate=150000m, Mode="Both",    Bio="Thay Long chuyen day Ngu van THPT va luyen thi THPTQG. Nhieu hoc sinh dat 8-9 diem Van ky thi THPTQG.", Subjects=new[]{6,7} },
            new { Email="hoangminhtu@tutor.com",    Pass="Tutor@123", FullName="Hoang Minh Tu",     Phone="0956789012", Address="Ninh Kieu, Can Tho",       Education="Thac si Vat ly - DH Can Tho",                             Exp=9,  Area="Ninh Kieu, Binh Thuy - Can Tho & Online", Rate=160000m, Mode="Both",    Bio="Thay Tu co 9 nam kinh nghiem day Vat ly. Biet cach truyen dat cac khai niem phuc tap mot cach don gian. Ty le hoc sinh vao nganh ky thuat top cao.", Subjects=new[]{3,1} },
            new { Email="nguyenthimai@tutor.com",   Pass="Tutor@123", FullName="Nguyen Thi Mai",    Phone="0967890123", Address="Dong Da, Ha Noi",          Education="Cu nhan Tieng Nhat - DH Ha Noi, N1 JLPT",                 Exp=4,  Area="Ha Noi & Online toan quoc",              Rate=220000m, Mode="Online",  Bio="Co Mai dat chung chi N1 JLPT, tung lam viec 2 nam tai Nhat. Day tieng Nhat tu mat goc den N1. Nhieu hoc vien da di du hoc va lam viec tai Nhat.", Subjects=new[]{8,2} },
            new { Email="tranvanduc@tutor.com",     Pass="Tutor@123", FullName="Tran Van Duc",      Phone="0978901234", Address="Long Bien, Ha Noi",         Education="Ky su Toan - Tin DH Bach Khoa Ha Noi",                    Exp=3,  Area="Long Bien, Gia Lam - Ha Noi & Online",   Rate=140000m, Mode="Both",    Bio="Anh Duc tot nghiep loai gioi DH Bach Khoa. Nhiet tinh, kien nhan voi hoc sinh yeu. Gia hop ly, phu hop hoc sinh can hoc bo sung.", Subjects=new[]{1,5} },
            new { Email="buithioanh@tutor.com",     Pass="Tutor@123", FullName="Bui Thi Oanh",      Phone="0911223344", Address="Nam Tu Liem, Ha Noi",       Education="Thac si Sinh hoc - DH Su pham Ha Noi",                    Exp=10, Area="Nam Tu Liem, Bac Tu Liem, Cau Giay - HN", Rate=170000m, Mode="Both",    Bio="Co Oanh day Sinh hoc va Hoa hoc THPT 10 nam. Phuong phap day truc quan, nhieu hinh anh minh hoa sinh dong. Hoc sinh yeu thich mon hoc ngay tu buoi dau.", Subjects=new[]{4,3} },
            new { Email="dovanquang@tutor.com",     Pass="Tutor@123", FullName="Do Van Quang",      Phone="0922334455", Address="Ngu Hanh Son, Da Nang",    Education="Thac si Toan ung dung - DH Bach Khoa Da Nang",            Exp=6,  Area="Da Nang & Online",                       Rate=165000m, Mode="Both",    Bio="Thay Quang chuyen luyen thi Toan dai hoc. Phuong phap tu duy logic, giai nhanh trac nghiem. Ty le hoc sinh dat 8+ diem Toan THPTQG rat cao.", Subjects=new[]{1,9} },
            new { Email="lehoangyen@tutor.com",     Pass="Tutor@123", FullName="Le Hoang Yen",      Phone="0933445566", Address="Binh Duong",                Education="Cu nhan Ngon ngu Anh - DH Quoc te, TOEIC 950",            Exp=5,  Area="Binh Duong, Thu Dau Mot & Online",        Rate=190000m, Mode="Online",  Bio="Co Yen chuyen luyen thi TOEIC va Tieng Anh doanh nghiep. Da giup hon 200 hoc vien dat TOEIC 700+ trong 3 thang. Phong cach day nang dong, thuc te.", Subjects=new[]{2,10} },
            new { Email="nguyenducmanh@tutor.com",  Pass="Tutor@123", FullName="Nguyen Duc Manh",   Phone="0944556677", Address="Hai Phong",                 Education="Thac si Vat ly - DH Hai Phong, 7 nam kinh nghiem",        Exp=7,  Area="Hai Phong & Online toan quoc",            Rate=155000m, Mode="Both",    Bio="Thay Manh day Vat ly va Toan THPT tai Hai Phong. Tung doat giai HSG Vat ly quoc gia. Hoc sinh hoc voi Thay thuong cai thien diem ro ret sau 1 thang.", Subjects=new[]{3,1} },
        };

        var tutorProfiles = new List<TutorProfile>();
        var tutorUsers    = new List<AppUser>();
        var availDays     = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

        foreach (var td in tutorData)
        {
            if (await userManager.FindByEmailAsync(td.Email) != null) continue;
            var user = new AppUser
            {
                UserName = td.Email, Email = td.Email, FullName = td.FullName,
                PhoneNumber = td.Phone, Address = td.Address, Role = "Tutor",
                EmailConfirmed = true, CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(60, 500)),
                XpPoints = rng.Next(200, 2000), XpLevel = "Gia su tich cuc"
            };
            await userManager.CreateAsync(user, td.Pass);
            await userManager.AddToRoleAsync(user, "Tutor");

            var profile = new TutorProfile
            {
                UserId = user.Id, Education = td.Education, Bio = td.Bio,
                HourlyRate = td.Rate, IsApproved = true,
                TeachingMode = td.Mode, ExperienceYears = td.Exp, TeachingArea = td.Area
            };
            db.TutorProfiles.Add(profile);
            await db.SaveChangesAsync();

            foreach (var sid in td.Subjects)
                db.TutorSubjects.Add(new TutorSubject { TutorProfileId = profile.Id, SubjectId = sid });

            foreach (var day in availDays.OrderBy(_ => rng.Next()).Take(rng.Next(3, 6)))
            {
                db.TutorAvailabilities.Add(new TutorAvailability { TutorProfileId = profile.Id, DayOfWeek = day, StartTime = new TimeSpan(8, 0, 0),  EndTime = new TimeSpan(11, 0, 0) });
                db.TutorAvailabilities.Add(new TutorAvailability { TutorProfileId = profile.Id, DayOfWeek = day, StartTime = new TimeSpan(18, 0, 0), EndTime = new TimeSpan(21, 0, 0) });
            }
            await db.SaveChangesAsync();
            tutorProfiles.Add(profile);
            tutorUsers.Add(user);
        }

        // ════════════════════════════════════════════════════
        //  TẠO 15 HỌC VIÊN
        // ════════════════════════════════════════════════════
        var studentData = new[]
        {
            new { Email="hocvien1@gmail.com",  Pass="Student@123", FullName="Nguyen Thi Bao Chau",  Phone="0321234567", Address="Cau Giay, Ha Noi",    Xp=350  },
            new { Email="hocvien2@gmail.com",  Pass="Student@123", FullName="Tran Minh Khoa",        Phone="0332345678", Address="Binh Thanh, TP.HCM",  Xp=520  },
            new { Email="hocvien3@gmail.com",  Pass="Student@123", FullName="Le Thi Thu Ha",         Phone="0343456789", Address="Dong Da, Ha Noi",     Xp=180  },
            new { Email="hocvien4@gmail.com",  Pass="Student@123", FullName="Pham Quoc Bao",         Phone="0354567890", Address="Thu Duc, TP.HCM",     Xp=740  },
            new { Email="hocvien5@gmail.com",  Pass="Student@123", FullName="Hoang Thi Yen Nhi",     Phone="0365678901", Address="Hai Chau, Da Nang",   Xp=290  },
            new { Email="hocvien6@gmail.com",  Pass="Student@123", FullName="Vu Dinh Anh Tuan",      Phone="0376789012", Address="Long Bien, Ha Noi",   Xp=610  },
            new { Email="hocvien7@gmail.com",  Pass="Student@123", FullName="Dang Thi My Linh",      Phone="0387890123", Address="Ninh Kieu, Can Tho",  Xp=430  },
            new { Email="hocvien8@gmail.com",  Pass="Student@123", FullName="Bui Thanh Hai",         Phone="0398901234", Address="Thanh Xuan, Ha Noi",  Xp=820  },
            new { Email="hocvien9@gmail.com",  Pass="Student@123", FullName="Ngo Thi Lan Anh",       Phone="0309012345", Address="Go Vap, TP.HCM",      Xp=160  },
            new { Email="hocvien10@gmail.com", Pass="Student@123", FullName="Dinh Van Manh",         Phone="0310123456", Address="Son Tra, Da Nang",    Xp=950  },
            new { Email="hocvien11@gmail.com", Pass="Student@123", FullName="Cao Thi Minh Nguyet",   Phone="0311234567", Address="Hai Phong",           Xp=275  },
            new { Email="hocvien12@gmail.com", Pass="Student@123", FullName="Trinh Van Hung",        Phone="0312345678", Address="Nha Trang, Khanh Hoa", Xp=490 },
            new { Email="hocvien13@gmail.com", Pass="Student@123", FullName="Phan Thi Thanh Thao",   Phone="0313456789", Address="Bien Hoa, Dong Nai",  Xp=330  },
            new { Email="hocvien14@gmail.com", Pass="Student@123", FullName="Ly Minh Tuan",          Phone="0314567890", Address="Quan 7, TP.HCM",      Xp=680  },
            new { Email="hocvien15@gmail.com", Pass="Student@123", FullName="Vo Thi Ngoc Ha",        Phone="0315678901", Address="Hue, Thua Thien Hue", Xp=210  },
        };

        var studentUsers = new List<AppUser>();
        foreach (var sd in studentData)
        {
            if (await userManager.FindByEmailAsync(sd.Email) != null) continue;
            var user = new AppUser
            {
                UserName = sd.Email, Email = sd.Email, FullName = sd.FullName,
                PhoneNumber = sd.Phone, Address = sd.Address, Role = "Student",
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(10, 300)), EmailConfirmed = true,
                XpPoints = sd.Xp, XpLevel = sd.Xp > 500 ? "Hoc vien cham chi" : "Hoc vien moi"
            };
            await userManager.CreateAsync(user, sd.Pass);
            await userManager.AddToRoleAsync(user, "Student");
            studentUsers.Add(user);
        }

        if (!tutorProfiles.Any() || !studentUsers.Any())
        {
            tutorProfiles = await db.TutorProfiles.Include(t => t.TutorSubjects).ToListAsync();
            studentUsers  = await db.Users.Where(u => u.Role == "Student").ToListAsync();
            tutorUsers    = new List<AppUser>();
            foreach (var p in tutorProfiles)
            {
                var u = await userManager.FindByIdAsync(p.UserId);
                if (u != null) tutorUsers.Add(u);
            }
        }

        // ════════════════════════════════════════════════════
        //  BOOKINGS + REVIEWS + REVIEW REPLIES
        // ════════════════════════════════════════════════════
        var reviewComments = new[]
        {
            "Thay/Co day rat de hieu, toi tien bo ro ret sau vai buoi hoc!",
            "Phuong phap giang day rat hay, bai tap phong phu va sat de thi.",
            "Giai thich ro rang tung buoc, kien nhan voi hoc sinh. Rat hai long!",
            "Noi dung hoc duoc chuan bi ky, dung trong tam can on thi.",
            "Thay/Co nhiet tinh, luon giai dap thac mac ke ca ngoai gio hoc.",
            "Hoc voi Thay/Co tien bo nhanh hon han tu hoc. Rat recommend!",
            "Gia ca hop ly, chat luong day tot. Se tiep tuc hoc dai han.",
            "Buoi hoc dau tien da thay ro su khac biet so voi hoc them o truong.",
            "Thay/Co co nhieu meo hay giup toi nho cong thuc nhanh hon.",
            "Phong cach day sinh dong, khong nham chan, hoc rat vao.",
            "Cam on Thay/Co rat nhieu, diem thi cua toi tang tu 5 len 8!",
            "Bai giang co cau truc ro rang, de theo doi va ghi chep.",
            "Thay/Co luon chia se tai lieu bo ich sau moi buoi hoc.",
            "Rat hai long voi cach Thay/Co to chuc buoi hoc. Khoa hoc va hieu qua.",
            "Hoc phi hop ly ma chat luong rat tot. Toi da gioi thieu cho ban be."
        };

        var replyTexts = new[]
        {
            "Cam on em da tin tuong va de lai danh gia! Chuc em hoc tot nhe!",
            "Thay/Co rat vui khi em co tien bo. Co gang len em nhe!",
            "Cam on em! Neu co gi can ho tro them em cu nhan tin cho Thay/Co nhe.",
            "That vui khi duoc dong hanh cung em. Chuc em dat ket qua tot trong ky thi!",
            "Cam on em rat nhieu! Thay/Co se tiep tuc chuan bi bai tot hon cho em.",
            "Em co gang on tap them o nha nhe. Thay/Co luon san sang ho tro em!",
            "Rat vui duoc day em. Neu can luyen them phan nao em cu bao Thay/Co nhe!"
        };

        var modes       = new[] { "Online", "Offline" };
        var noteOptions = new[]
        {
            "Can on tap phan dao ham va tich phan",
            "Muon luyen Speaking va Writing IELTS",
            "Hoc lap trinh Web tu co ban",
            "Can giai bai tap Hoa huu co",
            "On thi THPTQG phan Vat ly song",
            "Luyen viet van nghi luan xa hoi",
            "Hoc Toan cao cap tu dau",
            "Can luyen nghe tieng Nhat N3",
            "On tap Lich su the gioi can dai",
            "Luyen TOEIC Reading va Listening",
            null
        };

        var savedReviews = new List<(Review review, string tutorUserId)>();

        for (int ti = 0; ti < tutorProfiles.Count; ti++)
        {
            var tutor = tutorProfiles[ti];
            var tUser = tutorUsers.Count > ti ? tutorUsers[ti] : null;
            var subId = tutor.TutorSubjects.FirstOrDefault()?.SubjectId ?? 1;
            int bCount = rng.Next(20, 35);

            for (int bi = 0; bi < bCount; bi++)
            {
                var student = studentUsers[rng.Next(studentUsers.Count)];
                int daysAgo = rng.Next(-10, 150);
                var start   = DateTime.Now.AddDays(-daysAgo).Date.AddHours(rng.Next(7, 20)).AddMinutes(rng.Next(0, 2) * 30);
                var end     = start.AddHours(rng.Next(1, 3));

                string status; string? roomId = null;
                if      (daysAgo > 20) status = rng.Next(10) < 8 ? "Completed" : "Cancelled";
                else if (daysAgo > 3)  { status = rng.Next(10) < 6 ? "Confirmed" : "Completed"; roomId = $"Room-{ti}-{bi}-{Guid.NewGuid().ToString("N")[..6]}"; }
                else if (daysAgo < 0)  status = "Pending";
                else                   status = rng.Next(2) == 0 ? "Confirmed" : "Pending";
                if (status == "Confirmed") roomId = $"Room-{ti}-{bi}-{Guid.NewGuid().ToString("N")[..6]}";

                var booking = new Booking
                {
                    StudentId = student.Id, TutorProfileId = tutor.Id, SubjectId = subId,
                    StartTime = start, EndTime = end, Status = status,
                    TeachingMode  = tutor.TeachingMode == "Both" ? modes[rng.Next(2)] : (tutor.TeachingMode == "Online" ? "Online" : "Offline"),
                    Note          = noteOptions[rng.Next(noteOptions.Length)],
                    MeetingRoomId = roomId,
                    CreatedAt     = start.AddDays(-rng.Next(1, 10)),
                    IsPaid        = status == "Completed"
                };
                db.Bookings.Add(booking);
                await db.SaveChangesAsync();

                if (status == "Completed" && rng.Next(100) < 92)
                {
                    int rating = rng.Next(100) < 75 ? rng.Next(4, 6) : rng.Next(3, 5);
                    var review = new Review
                    {
                        StudentId = student.Id, TutorProfileId = tutor.Id, BookingId = booking.Id,
                        Rating = rating, Comment = reviewComments[rng.Next(reviewComments.Length)],
                        CreatedAt = end.AddHours(rng.Next(1, 72))
                    };
                    db.Reviews.Add(review);
                    await db.SaveChangesAsync();
                    if (tUser != null) savedReviews.Add((review, tUser.Id));
                }
            }
            await db.SaveChangesAsync();
        }

        // ReviewReply: 65% review duoc tra loi
        foreach (var (review, tutorUserId) in savedReviews.OrderBy(_ => rng.Next()).Take(savedReviews.Count * 65 / 100))
        {
            if (!await db.ReviewReplies.AnyAsync(r => r.ReviewId == review.Id))
                db.ReviewReplies.Add(new ReviewReply
                {
                    ReviewId = review.Id, AuthorId = tutorUserId,
                    Content  = replyTexts[rng.Next(replyTexts.Length)],
                    CreatedAt = review.CreatedAt.AddHours(rng.Next(1, 96))
                });
        }
        await db.SaveChangesAsync();

        // ════════════════════════════════════════════════════
        //  CERTIFICATES (2-3 chung chi moi gia su)
        // ════════════════════════════════════════════════════
        if (!await db.Certificates.AnyAsync())
        {
            var certMap = new (string title, CertificateType type)[][]
            {
                new[] { ("Bang Thac si Toan hoc - DH Su pham Ha Noi", CertificateType.Degree), ("Chung chi Su pham quoc te Cambridge", CertificateType.Certificate) },
                new[] { ("Bang Cu nhan Ngon ngu Anh", CertificateType.Degree), ("Chung chi IELTS 8.0", CertificateType.Certificate), ("Chung chi TESOL", CertificateType.Certificate) },
                new[] { ("Bang Ky su CNTT - DH Bach Khoa", CertificateType.Degree), ("Chung chi AWS Developer", CertificateType.Certificate), ("Chung chi Microsoft Azure", CertificateType.Certificate) },
                new[] { ("Bang Tien si Hoa hoc", CertificateType.Degree), ("Chung chi Nghien cuu Hoa hoc quoc te", CertificateType.Certificate) },
                new[] { ("Bang Cu nhan Su pham Ngu van", CertificateType.Degree), ("Chung chi Huong dan vien du lich", CertificateType.Certificate) },
                new[] { ("Bang Thac si Vat ly - DH Can Tho", CertificateType.Degree), ("Chung chi Giang vien Vat ly quoc gia", CertificateType.Certificate) },
                new[] { ("Bang Cu nhan Tieng Nhat", CertificateType.Degree), ("Chung chi JLPT N1", CertificateType.Certificate), ("Chung chi Kinh doanh tieng Nhat BJT", CertificateType.Certificate) },
                new[] { ("Bang Ky su Toan - Tin DH Bach Khoa", CertificateType.Degree), ("Chung chi Lap trinh Python", CertificateType.Certificate) },
                new[] { ("Bang Thac si Sinh hoc", CertificateType.Degree), ("Chung chi Su pham", CertificateType.Certificate) },
                new[] { ("Bang Thac si Toan ung dung", CertificateType.Degree), ("Chung chi Giang vien Toan", CertificateType.Certificate) },
                new[] { ("Bang Cu nhan Ngon ngu Anh", CertificateType.Degree), ("Chung chi TOEIC 950", CertificateType.Certificate), ("Chung chi TESOL quoc te", CertificateType.Certificate) },
                new[] { ("Bang Thac si Vat ly - DH Hai Phong", CertificateType.Degree), ("Huy chuong HSG Vat ly Quoc gia", CertificateType.Certificate) },
            };

            for (int i = 0; i < Math.Min(tutorProfiles.Count, certMap.Length); i++)
                foreach (var (title, certType) in certMap[i])
                    db.Certificates.Add(new Certificate
                    {
                        TutorProfileId = tutorProfiles[i].Id, Title = title,
                        FilePath = $"/uploads/certificates/cert_{i + 1}_{Math.Abs(title.GetHashCode()) % 99999:D5}.jpg",
                        FileType = "image", Type = certType,
                        IsVerified = rng.Next(10) < 8,
                        UploadedAt = DateTime.UtcNow.AddDays(-rng.Next(10, 180))
                    });
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════
        //  DOCUMENTS
        // ════════════════════════════════════════════════════
        if (!await db.Documents.AnyAsync())
        {
            var docDefs = new[]
            {
                new { Title="Tong hop cong thuc Toan THPT",          Desc="Toan bo cong thuc Toan lop 10-12.", Type="pdf", Size=1024000L, TutorIdx=0  },
                new { Title="100 de Toan thi thu THPTQG 2024",       Desc="Bo 100 de thi thu co dap an.",      Type="pdf", Size=5120000L, TutorIdx=0  },
                new { Title="IELTS Writing Task 2 - 50 mau hay",     Desc="50 bai mau Writing Task 2 band 7+", Type="pdf", Size=2048000L, TutorIdx=1  },
                new { Title="Tu vung IELTS theo chu de",              Desc="1500 tu vung IELTS theo 20 chu de", Type="pdf", Size=768000L,  TutorIdx=1  },
                new { Title="Lo trinh hoc C# .NET tu 0",             Desc="Huong dan hoc lap trinh C# tu co ban", Type="pdf", Size=3072000L, TutorIdx=2 },
                new { Title="Source code du an Web ASP.NET MVC",     Desc="Code mau du an quan ly sinh vien",   Type="pdf", Size=4096000L, TutorIdx=2  },
                new { Title="Hoa huu co - So do tu duy day du",      Desc="Toan bo Hoa huu co lop 11-12",       Type="pdf", Size=6144000L, TutorIdx=3  },
                new { Title="200 bai tap Hoa huu co co loi giai",    Desc="200 bai tap tu co ban den nang cao", Type="pdf", Size=2560000L, TutorIdx=3  },
                new { Title="Van mau nghi luan xa hoi lop 12",       Desc="50 bai van mau dat 8-9 diem",        Type="pdf", Size=1536000L, TutorIdx=4  },
                new { Title="Vat ly - Cong thuc va phuong phap giai",Desc="He thong cong thuc Vat ly THPT",     Type="pdf", Size=2048000L, TutorIdx=5  },
                new { Title="Tieng Nhat N4 - Giao trinh Minna",      Desc="Tom tat ngu phap Minna no Nihongo",  Type="pdf", Size=3584000L, TutorIdx=6  },
                new { Title="TOEIC 900 - Chien luoc lam bai",        Desc="Bi quyet dat 900 TOEIC",             Type="pdf", Size=1792000L, TutorIdx=10 },
            };

            foreach (var d in docDefs)
                if (d.TutorIdx < tutorUsers.Count)
                    db.Documents.Add(new Document
                    {
                        Title = d.Title, Description = d.Desc,
                        FilePath = $"/uploads/documents/{Math.Abs(d.Title.GetHashCode()) % 99999:D5}.{d.Type}",
                        FileName = $"{d.Title}.{d.Type}", FileType = d.Type, FileSize = d.Size,
                        UploaderId = tutorUsers[d.TutorIdx].Id, IsPublic = true,
                        DownloadCount = rng.Next(10, 300),
                        CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(5, 120))
                    });
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════
        //  QUIZ ATTEMPTS
        // ════════════════════════════════════════════════════
        if (!await db.QuizAttempts.AnyAsync())
        {
            var levels     = new[] { "Co ban", "Trung binh", "Nang cao" };
            var subjectIds = new[] { 1, 2, 3, 4, 5, 6, 8, 9 };
            foreach (var student in studentUsers)
            {
                int attempts = rng.Next(5, 15);
                for (int a = 0; a < attempts; a++)
                    db.QuizAttempts.Add(new QuizAttempt
                    {
                        UserId = student.Id, SubjectId = subjectIds[rng.Next(subjectIds.Length)],
                        Level = levels[rng.Next(levels.Length)], Score = rng.Next(4, 11),
                        TotalQuestions = 10, CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 90))
                    });
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════
        //  BADGES CHO GIA SU
        // ════════════════════════════════════════════════════
        var allBadges   = await db.Badges.ToListAsync();
        var allProfiles = await db.TutorProfiles
            .Include(t => t.Bookings).Include(t => t.ReceivedReviews).Include(t => t.TutorSubjects)
            .ToListAsync();

        foreach (var profile in allProfiles)
        {
            var completed    = profile.Bookings.Count(b => b.Status == "Completed");
            var reviewCount  = profile.ReceivedReviews.Count;
            var avgRating    = reviewCount > 0 ? profile.ReceivedReviews.Average(r => r.Rating) : 0;
            var subjectCount = profile.TutorSubjects.Count;
            var revenue      = profile.Bookings.Where(b => b.Status == "Completed")
                                .Sum(b => (decimal)(b.EndTime - b.StartTime).TotalHours * profile.HourlyRate);

            foreach (var badge in allBadges)
            {
                bool qualified = badge.Type switch
                {
                    BadgeType.Sessions => completed    >= badge.RequiredCount,
                    BadgeType.Reviews  => reviewCount  >= badge.RequiredCount,
                    BadgeType.Subjects => subjectCount >= badge.RequiredCount,
                    BadgeType.Revenue  => revenue      >= badge.RequiredCount * 1000,
                    BadgeType.Rating   => reviewCount >= 3 && avgRating * 10 >= badge.RequiredCount,
                    _                  => false
                };
                if (!qualified) continue;
                if (!await db.TutorBadges.AnyAsync(tb => tb.TutorProfileId == profile.Id && tb.BadgeId == badge.Id))
                    db.TutorBadges.Add(new TutorBadge { TutorProfileId = profile.Id, BadgeId = badge.Id, EarnedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 60)) });
            }
        }
        await db.SaveChangesAsync();

        // ════════════════════════════════════════════════════
        //  BLOG POSTS + COMMENTS + LIKES
        // ════════════════════════════════════════════════════
        if (tutorUsers.Count >= 7 && !await db.Posts.AnyAsync())
        {
            var posts = new List<Post>();
            if (tutorUsers.Count > 0)  posts.Add(new Post { AuthorId = tutorUsers[0].Id,  Title = "5 phuong phap hoc Toan hieu qua cho ky thi THPTQG",        Summary = "Chia se tu gia su 8 nam kinh nghiem: Cach hoc Toan hieu qua.",        Content = "Nhieu hoc sinh hoc Toan theo kieu thuoc long...\n\n1. Hieu goc re truoc khi giai bai\n2. Phan loai dang bai\n3. Luyen de theo thoi gian thuc\n4. Review sai lam hang ngay\n5. Hoc theo nhom nho", IsPublished = true, Views = rng.Next(800,2000),  CreatedAt = DateTime.Now.AddDays(-45) });
            if (tutorUsers.Count > 1)  posts.Add(new Post { AuthorId = tutorUsers[1].Id,  Title = "Roadmap hoc IELTS tu 0 len 7.0 trong 6 thang",             Summary = "Lo trinh hoc IELTS chi tiet theo tung thang, kem tai lieu mien phi.", Content = "IELTS 7.0 khong phai muc tieu xa voi...\n\nThang 1-2: Xay nen tang\nThang 3-4: Luyen ky nang\nThang 5-6: Mock test & dang ky thi",             IsPublished = true, Views = rng.Next(1200,3000), CreatedAt = DateTime.Now.AddDays(-32) });
            if (tutorUsers.Count > 2)  posts.Add(new Post { AuthorId = tutorUsers[2].Id,  Title = "Hoc lap trinh Web nam 2025: Nen bat dau tu dau?",           Summary = "Huong dan toan dien cho nguoi moi muon hoc lap trinh Web.",           Content = "Lap trinh Web la nganh hot nhat...\n\nBuoc 1: HTML & CSS\nBuoc 2: JavaScript\nBuoc 3: Chon Frontend hoac Backend\nBuoc 4: Lam Project thuc te",    IsPublished = true, Views = rng.Next(900,2500),  CreatedAt = DateTime.Now.AddDays(-20) });
            if (tutorUsers.Count > 3)  posts.Add(new Post { AuthorId = tutorUsers[3].Id,  Title = "Bi quyet hoc Hoa huu co khong bao gio quen",                Summary = "TS Hoa hoc chia se cach hoc Hoa huu co mot lan nho mai.",             Content = "Hoa huu co khien nhieu hoc sinh so...\n\n1. Hieu co che phan ung\n2. Ve so do tu duy\n3. Hoc tu vi du thuc te\n4. Luyen bai tap nhan biet",          IsPublished = true, Views = rng.Next(600,1800),  CreatedAt = DateTime.Now.AddDays(-15) });
            if (tutorUsers.Count > 4)  posts.Add(new Post { AuthorId = tutorUsers[4].Id,  Title = "Cach viet mo bai - ket bai Van nghi luan gay an tuong",     Summary = "Bi quyet viet mo bai sang tao va ket bai dong lai cam xuc.",          Content = "Mo bai va ket bai chiem 15-20% diem...\n\n3 kieu mo bai hieu qua:\n1. Cau hoi tu tu\n2. Trich dan\n3. Tinh huong gia dinh",                         IsPublished = true, Views = rng.Next(500,1500),  CreatedAt = DateTime.Now.AddDays(-10) });
            if (tutorUsers.Count > 1)  posts.Add(new Post { AuthorId = tutorUsers[1].Id,  Title = "Top 10 app hoc tieng Anh mien phi tot nhat 2025",           Summary = "Tong hop cac app hoc tieng Anh hieu qua nhat, tu nguoi moi.",         Content = "Hoc tieng Anh khong nhat thiet ton tien...\n\n1. Duolingo\n2. Anki\n3. BBC Learning English\n4. Elsa Speak\n5. Cake\n6. HelloTalk\n7. Coursera\n8. TED\n9. Grammarly\n10. DeepL", IsPublished = true, Views = rng.Next(700,2200), CreatedAt = DateTime.Now.AddDays(-5) });
            if (tutorUsers.Count > 6)  posts.Add(new Post { AuthorId = tutorUsers[6].Id,  Title = "Tai sao nen hoc tieng Nhat va co hoi viec lam nam 2025",    Summary = "Thi truong lao dong Nhat Ban dang mo rong, co hoi rat lon.",          Content = "Nhat Ban dang thieu lao dong...\n\nLy do hoc tieng Nhat:\n- Luong IT tai Nhat: 80-150 trieu/thang\n- Du hoc chi phi hop ly\n\nLo trinh: N5->N4->N3->N2->N1", IsPublished = true, Views = rng.Next(400,1200), CreatedAt = DateTime.Now.AddDays(-3) });
            if (tutorUsers.Count > 5)  posts.Add(new Post { AuthorId = tutorUsers[5].Id,  Title = "Tai sao Vat ly khong kho nhu ban nghi",                     Summary = "Thay Tu chia se cach tiep can Vat ly bang hinh anh, vi du thuc te.",  Content = "Vat ly khong kho nhu ban nghi...\n\nMeo 1: Lien he cong thuc voi thuc te\nMeo 2: Ve hinh minh hoa truoc khi giai\nMeo 3: Kiem tra don vi sau moi phep tinh", IsPublished = true, Views = rng.Next(550,1600), CreatedAt = DateTime.Now.AddDays(-8) });
            if (tutorUsers.Count > 9)  posts.Add(new Post { AuthorId = tutorUsers[9].Id,  Title = "Chien thuat lam bai Toan trac nghiem dat 9-10 diem",        Summary = "Bi quyet lam bai Toan trac nghiem nhanh va chinh xac.",              Content = "Toan trac nghiem can toc do va do chinh xac...\n\nChien thuat thoi gian:\n- Cau de: <=1 phut\n- Cau trung binh: 1-2 phut\n- Cau kho: bo qua, quay lai sau", IsPublished = true, Views = rng.Next(650,1900), CreatedAt = DateTime.Now.AddDays(-12) });
            if (tutorUsers.Count > 10) posts.Add(new Post { AuthorId = tutorUsers[10].Id, Title = "TOEIC 900+ khong kho neu ban biet cach hoc dung",            Summary = "Chien luoc hoc TOEIC dat 900+ trong 3 thang tu co Yen - TOEIC 950.", Content = "TOEIC 900+ trong 3 thang la hoan toan kha thi...\n\nListening (495 diem):\n- Part 1-2: Thuan thuc trong 2 tuan\n- Part 3-4: Luyen du doan truoc khi nghe\n\nReading (495 diem):\n- Part 5-6: On ngu phap trong diem", IsPublished = true, Views = rng.Next(750,2100), CreatedAt = DateTime.Now.AddDays(-18) });

            foreach (var post in posts) db.Posts.Add(post);
            await db.SaveChangesAsync();

            var commentTexts = new[]
            {
                "Bai viet rat huu ich! Cam on thay/co da chia se.",
                "Toi dang ap dung phuong phap nay va thay hieu qua hon han.",
                "Cho toi hoi them ve tai lieu luyen tap duoc khong a?",
                "Bai nay nen pin lai de doc lai nhieu lan!",
                "Chia se rat thuc te va chi tiet. Cam on nhieu!",
                "Toi da thu theo phuong phap nay duoc 2 tuan, diem tang ro ret.",
                "Thong tin rat bo ich cho ky thi sap toi.",
                "Mong thay/co ra them nhieu bai viet nhu the nay!",
                "Hay qua! Chia se them ve phan luyen de duoc khong a?",
                "Em dang chuan bi thi nen bai nay rat dung luc.",
                "Ap dung duoc ngay, khong can ton tien mua tai lieu ngoai.",
                "Thay/co co day truc tiep khong? Em muon dang ky hoc."
            };

            var allSavedPosts = await db.Posts.ToListAsync();
            foreach (var post in allSavedPosts)
            {
                int numCmts = rng.Next(4, 11);
                for (int c = 0; c < numCmts; c++)
                    db.Comments.Add(new Comment { PostId = post.Id, AuthorId = studentUsers[rng.Next(studentUsers.Count)].Id, Content = commentTexts[rng.Next(commentTexts.Length)], CreatedAt = post.CreatedAt.AddDays(rng.Next(1, 15)) });

                foreach (var liker in studentUsers.OrderBy(_ => rng.Next()).Take(rng.Next(6, 14)))
                    if (!await db.PostLikes.AnyAsync(pl => pl.PostId == post.Id && pl.UserId == liker.Id))
                        db.PostLikes.Add(new PostLike { PostId = post.Id, UserId = liker.Id, CreatedAt = post.CreatedAt.AddDays(rng.Next(1, 10)) });
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════
        //  TIN NHAN
        // ════════════════════════════════════════════════════
        if (!await db.Messages.AnyAsync())
        {
            var convs = new[]
            {
                new[] { ("s","Xin chao thay/co! Em muon hoi ve lich hoc a."), ("t","Chao em! Thay/Co co lich trong toi thu 2, 4, 6. Em muon hoc ngay nao?"), ("s","Da em muon hoc toi thu 4 luc 19h a."), ("t","Ok em nhe! Thay/Co se chuan bi bai tap theo de cuong cua em."), ("s","Da cam on thay/co! Em da dat lich roi a."), ("t","Thay/Co xac nhan roi. Hen gap em toi thu 4!") },
                new[] { ("s","Thay/co oi, em dang gap kho khan voi bai tap chuong nay a."), ("t","Em gap kho o phan nao? Thay/Co se giai thich them cho em nhe."), ("s","Da em chua hieu cach ap dung cong thuc vao bai tap a."), ("t","Thay/Co se cho em bai tap tu co ban den nang cao de luyen dan nhe."), ("s","Da em cam on thay/co nhieu a!"), ("t","Co gang len em! Buoi toi minh se on ky phan nay.") },
                new[] { ("s","Thay/co cho em hoi hoc phi mot thang la bao nhieu a?"), ("t","Em hoc 2 buoi/tuan thi mot thang khoang 8 buoi, tong khoang 1.6 trieu nhe."), ("s","Da hop ly a. Em muon dang ky hoc thu 1 buoi truoc duoc khong a?"), ("t","Duoc chu em! Thay/Co se cho em hoc thu mien phi buoi dau tien."), ("s","Wow, tuyet voi qua a! Em dang ky ngay."), ("t","Thay/Co cho em dat lich nhe. Hen gap em som!") },
            };

            for (int ti = 0; ti < Math.Min(tutorUsers.Count, 8); ti++)
            {
                var tUser = tutorUsers[ti];
                for (int si = 0; si < Math.Min(4, studentUsers.Count); si++)
                {
                    var sUser = studentUsers[(ti * 2 + si) % studentUsers.Count];
                    var conv  = convs[rng.Next(convs.Length)];
                    var baseT = DateTime.UtcNow.AddDays(-rng.Next(1, 20)).AddHours(-rng.Next(1, 48));
                    for (int mi = 0; mi < conv.Length; mi++)
                    {
                        bool isStudent = conv[mi].Item1 == "s";
                        db.Messages.Add(new Message
                        {
                            SenderId   = isStudent ? sUser.Id : tUser.Id,
                            ReceiverId = isStudent ? tUser.Id : sUser.Id,
                            Content    = conv[mi].Item2,
                            SentAt     = baseT.AddMinutes(mi * rng.Next(3, 15)),
                            IsRead     = mi < conv.Length - 2
                        });
                    }
                }
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════
        //  NOTIFICATIONS
        // ════════════════════════════════════════════════════
        if (!await db.Notifications.AnyAsync())
        {
            var notifs = new List<Notification>();
            foreach (var student in studentUsers)
            {
                notifs.Add(new Notification { UserId = student.Id, Title = "Lich hoc duoc xac nhan!",       Content = "Gia su da xac nhan lich hoc cua ban. Chuan bi do dung va dung gio nhe!", Link = "/Booking/MyBookings", IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(1, 24))  });
                notifs.Add(new Notification { UserId = student.Id, Title = "Nhac lich hoc ngay mai",        Content = "Ban co buoi hoc luc 19:00 ngay mai. Dung quen chuan bi bai nhe!",         Link = "/Booking/MyBookings", IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(2, 48))  });
                notifs.Add(new Notification { UserId = student.Id, Title = "AI goi y gia su phu hop",       Content = "Dua tren lich su hoc tap, chung toi tim duoc 3 gia su phu hop hon!",       Link = "/TutorSearch/Search", IsRead = rng.Next(2)==0, CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(10,72)) });
                notifs.Add(new Notification { UserId = student.Id, Title = "Hoan thanh Quiz xuat sac!",     Content = "Ban dat 9/10 trong bai Quiz Toan hoc. Tiep tuc phat huy nhe!",             Link = "/Quiz",               IsRead = rng.Next(2)==0, CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(5, 60)) });
                notifs.Add(new Notification { UserId = student.Id, Title = "Tai lieu moi duoc chia se",     Content = "Gia su cua ban vua chia se tai lieu hoc tap moi. Tai ve va on tap ngay!",  Link = "/Document",           IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(3, 36))  });
            }
            foreach (var tUser in tutorUsers)
            {
                notifs.Add(new Notification { UserId = tUser.Id, Title = "Yeu cau dat lich moi!",           Content = "Mot hoc vien vua gui yeu cau dat lich hoc. Hay xem va xac nhan som!",     Link = "/Booking/TutorRequests", IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(1, 12))   });
                notifs.Add(new Notification { UserId = tUser.Id, Title = "Ban vua nhan danh gia moi!",      Content = "Hoc vien da de lai danh gia 5 sao cho buoi hoc vua roi. Xem ngay!",       Link = "/Tutor/Dashboard",       IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(2, 48))   });
                notifs.Add(new Notification { UserId = tUser.Id, Title = "Huy hieu moi duoc mo khoa!",      Content = "Chuc mung! Ban vua dat huy hieu 'Duoc yeu thich' voi 5 luot danh gia.",   Link = "/Tutor/Dashboard",       IsRead = rng.Next(2)==0, CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(5, 72)) });
                notifs.Add(new Notification { UserId = tUser.Id, Title = "Doanh thu thang nay tang 20%!",   Content = "Thang nay ban co them 8 buoi day so voi thang truoc. Xuat sac lam!",      Link = "/Tutor/Dashboard",       IsRead = rng.Next(2)==0, CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(20,120)) });
                notifs.Add(new Notification { UserId = tUser.Id, Title = "Hoc vien nhan tin cho ban",       Content = "Ban co tin nhan moi tu hoc vien. Hay tra loi som de giu tuong tac tot!",  Link = "/Message",               IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(1, 24))   });
            }
            db.Notifications.AddRange(notifs);
            await db.SaveChangesAsync();
        }

        Console.WriteLine("Seed data hoan thanh! Da tao day du du lieu demo.");
    }
}
