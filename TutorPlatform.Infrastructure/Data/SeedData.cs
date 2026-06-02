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
        if (await db.TutorProfiles.CountAsync() >= 8) return;

        var rng = Random.Shared;

        // ROLES
        foreach (var role in new[] { "Admin", "Tutor", "Student" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // ADMIN
        if (await userManager.FindByEmailAsync("admin@tutor.com") == null)
        {
            var admin = new AppUser { UserName="admin@tutor.com", Email="admin@tutor.com", FullName="Quan tri vien", Role="Admin", EmailConfirmed=true, CreatedAt=DateTime.UtcNow };
            await userManager.CreateAsync(admin, "Admin@123");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // SUBJECTS
        var existingIds = await db.Subjects.Select(s => s.Id).ToListAsync();
        var subjects = new[] {
            new Subject { Id=1,  Name="Toan hoc",    Level="THPT",      IsActive=true },
            new Subject { Id=2,  Name="Tieng Anh",   Level="THPT",      IsActive=true },
            new Subject { Id=3,  Name="Vat ly",      Level="THPT",      IsActive=true },
            new Subject { Id=4,  Name="Hoa hoc",     Level="THPT",      IsActive=true },
            new Subject { Id=5,  Name="Lap trinh",   Level="Dai hoc",   IsActive=true },
            new Subject { Id=6,  Name="Ngu van",     Level="THPT",      IsActive=true },
            new Subject { Id=7,  Name="Lich su",     Level="THPT",      IsActive=true },
            new Subject { Id=8,  Name="Tieng Nhat",  Level="Dai hoc",   IsActive=true },
            new Subject { Id=9,  Name="Toan cao cap",Level="Dai hoc",   IsActive=true },
            new Subject { Id=10, Name="IELTS",       Level="Chung chi", IsActive=true },
        };
        var missing = subjects.Where(s => !existingIds.Contains(s.Id)).ToList();
        if (missing.Any()) { db.Subjects.AddRange(missing); await db.SaveChangesAsync(); }

        // BADGES
        if (!await db.Badges.AnyAsync())
        {
            db.Badges.AddRange(
                new Badge { Id=1,  Name="Khoi dau",       Description="Hoan thanh buoi hoc dau tien",         Icon="🌱", Color="#4CAF50", Type=BadgeType.Sessions, RequiredCount=1    },
                new Badge { Id=2,  Name="Dang len",        Description="Hoan thanh 10 buoi hoc",               Icon="⚡", Color="#2196F3", Type=BadgeType.Sessions, RequiredCount=10   },
                new Badge { Id=3,  Name="Chuyen nghiep",   Description="Hoan thanh 50 buoi hoc",               Icon="🎯", Color="#9C27B0", Type=BadgeType.Sessions, RequiredCount=50   },
                new Badge { Id=4,  Name="Huyen thoai",     Description="Hoan thanh 100 buoi hoc",              Icon="🏆", Color="#FFD700", Type=BadgeType.Sessions, RequiredCount=100  },
                new Badge { Id=5,  Name="Duoc yeu thich",  Description="Nhan duoc 5 luot danh gia",            Icon="⭐", Color="#FF9800", Type=BadgeType.Reviews,  RequiredCount=5    },
                new Badge { Id=6,  Name="Top Rated",       Description="Nhan duoc 20 luot danh gia",           Icon="🌟", Color="#FF5722", Type=BadgeType.Reviews,  RequiredCount=20   },
                new Badge { Id=7,  Name="Gia su xuat sac", Description="Diem TB >= 4.5 sao",                   Icon="💎", Color="#00BCD4", Type=BadgeType.Rating,   RequiredCount=45   },
                new Badge { Id=8,  Name="Hoan hao",        Description="Diem TB >= 4.8 sao",                   Icon="👑", Color="#E91E63", Type=BadgeType.Rating,   RequiredCount=48   },
                new Badge { Id=9,  Name="Da nang",         Description="Day tu 3 mon hoc tro len",             Icon="📚", Color="#607D8B", Type=BadgeType.Subjects, RequiredCount=3    },
                new Badge { Id=10, Name="Trieu phu",       Description="Tich luy doanh thu 1,000,000 VND",     Icon="💰", Color="#795548", Type=BadgeType.Revenue,  RequiredCount=1000 }
            );
            await db.SaveChangesAsync();
        }

        // ── 10 HỌC VIÊN ──────────────────────────────────────────────────
        var studentEmails = new[] {
            ("hocvien1@gmail.com",  "Nguyen Thi Bao Chau",  "0321234567", "Cau Giay, Ha Noi"),
            ("hocvien2@gmail.com",  "Tran Minh Khoa",       "0332345678", "Binh Thanh, TP.HCM"),
            ("hocvien3@gmail.com",  "Le Thi Thu Ha",        "0343456789", "Dong Da, Ha Noi"),
            ("hocvien4@gmail.com",  "Pham Quoc Bao",        "0354567890", "Thu Duc, TP.HCM"),
            ("hocvien5@gmail.com",  "Hoang Thi Yen Nhi",   "0365678901", "Hai Chau, Da Nang"),
            ("hocvien6@gmail.com",  "Vu Dinh Anh Tuan",    "0376789012", "Long Bien, Ha Noi"),
            ("hocvien7@gmail.com",  "Dang Thi My Linh",    "0387890123", "Ninh Kieu, Can Tho"),
            ("hocvien8@gmail.com",  "Bui Thanh Hai",        "0398901234", "Thanh Xuan, Ha Noi"),
            ("hocvien9@gmail.com",  "Ngo Thi Lan Anh",     "0309012345", "Go Vap, TP.HCM"),
            ("hocvien10@gmail.com", "Dinh Van Manh",        "0310123456", "Son Tra, Da Nang"),
        };
        var studentUsers = new List<AppUser>();
        foreach (var (email, name, phone, addr) in studentEmails)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing != null) { studentUsers.Add(existing); continue; }
            var u = new AppUser { UserName=email, Email=email, FullName=name, PhoneNumber=phone, Address=addr, Role="Student", EmailConfirmed=true, CreatedAt=DateTime.UtcNow.AddDays(-rng.Next(10,200)), XpPoints=rng.Next(100,900) };
            await userManager.CreateAsync(u, "Student@123");
            await userManager.AddToRoleAsync(u, "Student");
            studentUsers.Add(u);
        }

        // ── 8 GIA SƯ ──────────────────────────────────────────────────
        var tutorDefs = new[] {
            (Email:"nguyenvanminh@tutor.com",  Name:"Nguyen Van Minh",  Phone:"0901234567", Addr:"Cau Giay, Ha Noi",      Edu:"Thac si Toan hoc - DH Su pham Ha Noi",           Exp:8,  Area:"Cau Giay, Dong Da, Ba Dinh - Ha Noi",   Rate:200000m, Mode:"Both",    Bio:"Thay Minh co 8 nam kinh nghiem luyen thi Toan THPT va Dai hoc. Phuong phap day ro rang, logic. Da co hon 150 hoc sinh dau dai hoc cac truong top.", Subs:new[]{1,3,9}),
            (Email:"tranthihuong@tutor.com",   Name:"Tran Thi Huong",   Phone:"0912345678", Addr:"Binh Thanh, TP.HCM",    Edu:"Cu nhan Ngon ngu Anh - DH Ngoai ngu, IELTS 8.0", Exp:6,  Area:"Binh Thanh, Go Vap, Phu Nhuan - TP.HCM", Rate:250000m, Mode:"Online",  Bio:"Co Huong chuyen day Tieng Anh giao tiep va luyen thi IELTS. Da dat IELTS 8.0 va co 6 nam kinh nghiem.", Subs:new[]{2,10}),
            (Email:"lephantrung@tutor.com",    Name:"Le Phan Trung",    Phone:"0923456789", Addr:"Thu Duc, TP.HCM",        Edu:"Ky su CNTT - DH Bach Khoa TP.HCM",               Exp:5,  Area:"TP.HCM - Online toan quoc",              Rate:300000m, Mode:"Online",  Bio:"Anh Trung la Senior Developer voi 5 nam thuc chien. Chuyen day lap trinh Web C# .NET, React, NodeJS.", Subs:new[]{5,9}),
            (Email:"phamthilan@tutor.com",     Name:"Pham Thi Lan",     Phone:"0934567890", Addr:"Thanh Xuan, Ha Noi",     Edu:"Tien si Hoa hoc - DH Khoa hoc Tu nhien Ha Noi",  Exp:12, Area:"Thanh Xuan, Hoang Mai, Ha Dong - Ha Noi", Rate:180000m, Mode:"Offline", Bio:"Co Lan la Tien si Hoa hoc voi 12 nam giang day. Chuyen luyen thi Hoa THPTQG va thi hoc sinh gioi.", Subs:new[]{4,3}),
            (Email:"vuthanhlong@tutor.com",    Name:"Vu Thanh Long",    Phone:"0945678901", Addr:"Hai Chau, Da Nang",      Edu:"Cu nhan Su pham Ngu van - DH Da Nang",           Exp:7,  Area:"Hai Chau, Thanh Khe, Son Tra - Da Nang", Rate:150000m, Mode:"Both",    Bio:"Thay Long chuyen day Ngu van THPT va luyen thi THPTQG. Nhieu hoc sinh dat 8-9 diem Van.", Subs:new[]{6,7}),
            (Email:"hoangminhtu@tutor.com",    Name:"Hoang Minh Tu",    Phone:"0956789012", Addr:"Ninh Kieu, Can Tho",     Edu:"Thac si Vat ly - DH Can Tho",                    Exp:9,  Area:"Ninh Kieu, Binh Thuy - Can Tho & Online", Rate:160000m, Mode:"Both",    Bio:"Thay Tu co 9 nam kinh nghiem day Vat ly. Truyen dat khai niem phuc tap mot cach don gian.", Subs:new[]{3,1}),
            (Email:"nguyenthimai@tutor.com",   Name:"Nguyen Thi Mai",   Phone:"0967890123", Addr:"Dong Da, Ha Noi",        Edu:"Cu nhan Tieng Nhat - DH Ha Noi, N1 JLPT",        Exp:4,  Area:"Ha Noi & Online toan quoc",              Rate:220000m, Mode:"Online",  Bio:"Co Mai dat chung chi N1 JLPT, tung lam viec 2 nam tai Nhat. Day tieng Nhat tu mat goc den N1.", Subs:new[]{8,2}),
            (Email:"tranvanduc@tutor.com",     Name:"Tran Van Duc",     Phone:"0978901234", Addr:"Long Bien, Ha Noi",      Edu:"Ky su Toan - Tin DH Bach Khoa Ha Noi",           Exp:3,  Area:"Long Bien, Gia Lam - Ha Noi & Online",   Rate:140000m, Mode:"Both",    Bio:"Anh Duc tot nghiep loai gioi DH Bach Khoa. Nhiet tinh, kien nhan. Gia hop ly cho hoc sinh bo sung.", Subs:new[]{1,5}),
        };

        var tutorProfiles = new List<TutorProfile>();
        var tutorUsers    = new List<AppUser>();
        var availDays     = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

        foreach (var td in tutorDefs)
        {
            var existing = await userManager.FindByEmailAsync(td.Email);
            AppUser tUser;
            if (existing != null)
            {
                tUser = existing;
            }
            else
            {
                tUser = new AppUser { UserName=td.Email, Email=td.Email, FullName=td.Name, PhoneNumber=td.Phone, Address=td.Addr, Role="Tutor", EmailConfirmed=true, CreatedAt=DateTime.UtcNow.AddDays(-rng.Next(60,400)), XpPoints=rng.Next(200,2000) };
                await userManager.CreateAsync(tUser, "Tutor@123");
                await userManager.AddToRoleAsync(tUser, "Tutor");
            }

            var profile = await db.TutorProfiles.FirstOrDefaultAsync(p => p.UserId == tUser.Id);
            if (profile == null)
            {
                profile = new TutorProfile { UserId=tUser.Id, Education=td.Edu, Bio=td.Bio, HourlyRate=td.Rate, IsApproved=true, TeachingMode=td.Mode, ExperienceYears=td.Exp, TeachingArea=td.Area };
                db.TutorProfiles.Add(profile);
                await db.SaveChangesAsync();

                foreach (var sid in td.Subs)
                    db.TutorSubjects.Add(new TutorSubject { TutorProfileId=profile.Id, SubjectId=sid });

                foreach (var day in availDays.OrderBy(_ => rng.Next()).Take(rng.Next(3, 6)))
                {
                    db.TutorAvailabilities.Add(new TutorAvailability { TutorProfileId=profile.Id, DayOfWeek=day, StartTime=new TimeSpan(8,0,0),  EndTime=new TimeSpan(11,0,0) });
                    db.TutorAvailabilities.Add(new TutorAvailability { TutorProfileId=profile.Id, DayOfWeek=day, StartTime=new TimeSpan(18,0,0), EndTime=new TimeSpan(21,0,0) });
                }
                await db.SaveChangesAsync();
            }
            tutorProfiles.Add(profile);
            tutorUsers.Add(tUser);
        }

        // ── BOOKINGS + REVIEWS ────────────────────────────────────────
        var reviewTexts = new[] {
            "Thay Co day rat de hieu, toi tien bo ro ret sau vai buoi hoc!",
            "Phuong phap giang day rat hay, bai tap phong phu va sat de thi.",
            "Giai thich ro rang tung buoc, kien nhan voi hoc sinh. Rat hai long!",
            "Noi dung hoc chuan bi ky, dung trong tam can on thi.",
            "Thay Co nhiet tinh, luon giai dap thac mac ke ca ngoai gio hoc.",
            "Hoc voi Thay Co tien bo nhanh hon han tu hoc. Rat recommend!",
            "Diem thi cua toi tang tu 5 len 8 sau 2 thang hoc. Cam on nhieu!",
            "Buoi hoc dau tien da thay ro su khac biet so voi hoc them o truong.",
            "Thay Co co nhieu meo hay giup toi nho cong thuc nhanh hon.",
            "Phong cach day sinh dong, khong nham chan, hoc rat vao.",
        };
        var replyTexts = new[] {
            "Cam on em da tin tuong va de lai danh gia! Chuc em hoc tot!",
            "Thay Co rat vui khi em co tien bo. Co gang len em nhe!",
            "Cam on em! Neu co gi can ho tro them em cu nhan tin nhe.",
            "That vui khi duoc dong hanh cung em. Chuc em dat ket qua tot!",
            "Cam on em rat nhieu! Thay Co se tiep tuc chuan bi bai tot hon.",
        };
        var modes = new[] { "Online", "Offline" };

        for (int ti = 0; ti < tutorProfiles.Count; ti++)
        {
            var tutor  = tutorProfiles[ti];
            var tUser  = tutorUsers[ti];
            var subId  = td_Subs(tutorDefs[ti].Subs);
            int bCount = rng.Next(15, 25);

            for (int bi = 0; bi < bCount; bi++)
            {
                var student = studentUsers[rng.Next(studentUsers.Count)];
                // Đảm bảo student không phải tutor
                if (student.Id == tUser.Id) continue;

                int daysAgo = rng.Next(-5, 120);
                var start   = DateTime.Now.AddDays(-daysAgo).Date.AddHours(rng.Next(7, 20));
                var end     = start.AddHours(rng.Next(1, 3));

                string status;
                if      (daysAgo > 20) status = rng.Next(10) < 8 ? "Completed" : "Cancelled";
                else if (daysAgo > 3)  status = rng.Next(10) < 6 ? "Confirmed" : "Completed";
                else if (daysAgo < 0)  status = "Pending";
                else                   status = "Confirmed";

                string? roomId = (status == "Confirmed") ? $"Room-{ti}-{bi}-{Guid.NewGuid().ToString("N")[..6]}" : null;

                var booking = new Booking
                {
                    StudentId=student.Id, TutorProfileId=tutor.Id, SubjectId=subId,
                    StartTime=start, EndTime=end, Status=status,
                    TeachingMode=tutor.TeachingMode=="Both" ? modes[rng.Next(2)] : tutor.TeachingMode,
                    Note="On tap phan trong tam", MeetingRoomId=roomId,
                    CreatedAt=start.AddDays(-rng.Next(1,7)), IsPaid=status=="Completed"
                };
                try
                {
                    db.Bookings.Add(booking);
                    await db.SaveChangesAsync();
                }
                catch { db.ChangeTracker.Clear(); continue; }

                if (status == "Completed" && rng.Next(100) < 90)
                {
                    int rating = rng.Next(10) < 7 ? rng.Next(4, 6) : rng.Next(3, 5);
                    try
                    {
                        var review = new Review { StudentId=student.Id, TutorProfileId=tutor.Id, BookingId=booking.Id, Rating=rating, Comment=reviewTexts[rng.Next(reviewTexts.Length)], CreatedAt=end.AddHours(rng.Next(1,48)) };
                        db.Reviews.Add(review);
                        await db.SaveChangesAsync();

                        // Reply 60%
                        if (rng.Next(10) < 6)
                        {
                            db.ReviewReplies.Add(new ReviewReply { ReviewId=review.Id, AuthorId=tUser.Id, Content=replyTexts[rng.Next(replyTexts.Length)], CreatedAt=review.CreatedAt.AddHours(rng.Next(1,72)) });
                            await db.SaveChangesAsync();
                        }
                    }
                    catch { db.ChangeTracker.Clear(); }
                }
            }
        }

        // ── BADGES CHO GIA SƯ ────────────────────────────────────────
        var allBadges = await db.Badges.ToListAsync();
        foreach (var profile in await db.TutorProfiles.Include(t=>t.Bookings).Include(t=>t.ReceivedReviews).Include(t=>t.TutorSubjects).ToListAsync())
        {
            var completed    = profile.Bookings.Count(b => b.Status == "Completed");
            var reviewCount  = profile.ReceivedReviews.Count;
            var avgRating    = reviewCount > 0 ? profile.ReceivedReviews.Average(r => r.Rating) : 0;
            var subjectCount = profile.TutorSubjects.Count;
            var revenue      = profile.Bookings.Where(b=>b.Status=="Completed").Sum(b=>(decimal)(b.EndTime-b.StartTime).TotalHours*profile.HourlyRate);

            foreach (var badge in allBadges)
            {
                bool ok = badge.Type switch {
                    BadgeType.Sessions => completed    >= badge.RequiredCount,
                    BadgeType.Reviews  => reviewCount  >= badge.RequiredCount,
                    BadgeType.Subjects => subjectCount >= badge.RequiredCount,
                    BadgeType.Revenue  => revenue      >= badge.RequiredCount * 1000,
                    BadgeType.Rating   => reviewCount >= 3 && avgRating * 10 >= badge.RequiredCount,
                    _ => false
                };
                if (!ok) continue;
                if (!await db.TutorBadges.AnyAsync(tb => tb.TutorProfileId==profile.Id && tb.BadgeId==badge.Id))
                {
                    try { db.TutorBadges.Add(new TutorBadge { TutorProfileId=profile.Id, BadgeId=badge.Id, EarnedAt=DateTime.UtcNow.AddDays(-rng.Next(1,30)) }); await db.SaveChangesAsync(); }
                    catch { db.ChangeTracker.Clear(); }
                }
            }
        }

        // ── CERTIFICATES ────────────────────────────────────────
        if (!await db.Certificates.AnyAsync())
        {
            var certs = new[] {
                (0, "Bang Thac si Toan hoc",          CertificateType.Degree),
                (0, "Chung chi Su pham Cambridge",    CertificateType.Certificate),
                (1, "Bang Cu nhan Ngon ngu Anh",      CertificateType.Degree),
                (1, "Chung chi IELTS 8.0",            CertificateType.Certificate),
                (2, "Bang Ky su CNTT",                CertificateType.Degree),
                (2, "Chung chi AWS Developer",        CertificateType.Certificate),
                (3, "Bang Tien si Hoa hoc",           CertificateType.Degree),
                (4, "Bang Cu nhan Su pham Ngu van",   CertificateType.Degree),
                (5, "Bang Thac si Vat ly",            CertificateType.Degree),
                (6, "Bang Cu nhan Tieng Nhat",        CertificateType.Degree),
                (6, "Chung chi JLPT N1",              CertificateType.Certificate),
                (7, "Bang Ky su Toan - Tin",          CertificateType.Degree),
            };
            foreach (var (idx, title, certType) in certs)
                if (idx < tutorProfiles.Count)
                {
                    try
                    {
                        db.Certificates.Add(new Certificate { TutorProfileId=tutorProfiles[idx].Id, Title=title, FilePath=$"/uploads/certificates/cert_{idx}.jpg", FileType="image", Type=certType, IsVerified=rng.Next(10)<8, UploadedAt=DateTime.UtcNow.AddDays(-rng.Next(10,180)) });
                        await db.SaveChangesAsync();
                    }
                    catch { db.ChangeTracker.Clear(); }
                }
        }

        // ── QUIZ ATTEMPTS ────────────────────────────────────────
        if (!await db.QuizAttempts.AnyAsync())
        {
            var levels = new[] { "Co ban", "Trung binh", "Nang cao" };
            var subIds = new[] { 1, 2, 3, 4, 5 };
            foreach (var student in studentUsers)
                for (int a = 0; a < rng.Next(3, 10); a++)
                {
                    try
                    {
                        db.QuizAttempts.Add(new QuizAttempt { UserId=student.Id, SubjectId=subIds[rng.Next(subIds.Length)], Level=levels[rng.Next(levels.Length)], Score=rng.Next(4,11), TotalQuestions=10, CreatedAt=DateTime.UtcNow.AddDays(-rng.Next(1,90)) });
                        await db.SaveChangesAsync();
                    }
                    catch { db.ChangeTracker.Clear(); }
                }
        }

        // ── BLOG POSTS ────────────────────────────────────────
        if (!await db.Posts.AnyAsync() && tutorUsers.Count >= 5)
        {
            var postDefs = new[] {
                (0, "5 phuong phap hoc Toan hieu qua cho ky thi THPTQG",        "Chia se tu gia su 8 nam kinh nghiem: Cach hoc Toan hieu qua, tranh hoc vet va tang diem nhanh.",        45),
                (1, "Roadmap hoc IELTS tu 0 len 7.0 trong 6 thang",             "Lo trinh hoc IELTS chi tiet theo tung thang, kem tai lieu va app hoc mien phi.",                          32),
                (2, "Hoc lap trinh Web nam 2025: Nen bat dau tu dau?",           "Huong dan toan dien cho nguoi moi muon hoc lap trinh Web, tu HTML den Full-stack.",                       20),
                (3, "Bi quyet hoc Hoa huu co khong bao gio quen",               "TS Hoa hoc chia se cach hoc Hoa huu co mot lan nho mai, khong can hoc thuoc.",                           15),
                (4, "Cach viet mo bai - ket bai Van nghi luan gay an tuong",     "Bi quyet viet mo bai sang tao va ket bai dong lai cam xuc cho diem cao.",                                10),
                (1, "Top 10 app hoc tieng Anh mien phi tot nhat 2025",          "Tong hop cac app hoc tieng Anh hieu qua nhat, tu nguoi moi den nang cao.",                               5),
                (5, "Tai sao Vat ly khong kho nhu ban nghi",                     "Thay Tu chia se cach tiep can Vat ly bang hinh anh, vi du thuc te xung quanh.",                          8),
                (6, "Tai sao nen hoc tieng Nhat va co hoi viec lam 2025",        "Thi truong lao dong Nhat Ban dang mo rong, co hoi rat lon cho nguoi biet tieng Nhat.",                   3),
            };

            var commentTexts = new[] {
                "Bai viet rat huu ich! Cam on thay co da chia se.",
                "Toi dang ap dung phuong phap nay va thay hieu qua hon han.",
                "Cho toi hoi them ve tai lieu de luyen tap duoc khong a?",
                "Bai nay nen pin lai de doc lai nhieu lan!",
                "Chia se rat thuc te va chi tiet. Cam on nhieu!",
                "Toi da thu theo phuong phap nay 2 tuan, diem tang ro ret.",
                "Thong tin rat bo ich cho ky thi sap toi cua toi.",
                "Mong thay co ra them nhieu bai viet nhu the nay!",
            };

            foreach (var (ti, title, summary, days) in postDefs)
            {
                if (ti >= tutorUsers.Count) continue;
                try
                {
                    var post = new Post { AuthorId=tutorUsers[ti].Id, Title=title, Summary=summary, Content=summary + "\n\nNoi dung chi tiet ve chu de nay rat phong phu va bo ich cho hoc vien...", IsPublished=true, Views=rng.Next(200,2000), CreatedAt=DateTime.Now.AddDays(-days) };
                    db.Posts.Add(post);
                    await db.SaveChangesAsync();

                    // Comments
                    for (int c = 0; c < rng.Next(3, 8); c++)
                    {
                        var commenter = studentUsers[rng.Next(studentUsers.Count)];
                        db.Comments.Add(new Comment { PostId=post.Id, AuthorId=commenter.Id, Content=commentTexts[rng.Next(commentTexts.Length)], CreatedAt=post.CreatedAt.AddDays(rng.Next(1,10)) });
                    }
                    await db.SaveChangesAsync();

                    // Likes
                    foreach (var liker in studentUsers.OrderBy(_ => rng.Next()).Take(rng.Next(4, 10)))
                    {
                        if (!await db.PostLikes.AnyAsync(pl => pl.PostId==post.Id && pl.UserId==liker.Id))
                            db.PostLikes.Add(new PostLike { PostId=post.Id, UserId=liker.Id, CreatedAt=post.CreatedAt.AddDays(rng.Next(1,5)) });
                    }
                    await db.SaveChangesAsync();
                }
                catch { db.ChangeTracker.Clear(); }
            }
        }

        // ── MESSAGES ────────────────────────────────────────
        if (!await db.Messages.AnyAsync())
        {
            var msgs = new[] {
                ("s","Xin chao thay co! Em muon hoi ve lich hoc a."),
                ("t","Chao em! Thay Co co lich trong toi thu 2, 4, 6."),
                ("s","Da em muon hoc toi thu 4 luc 19h a."),
                ("t","Ok em nhe! Thay Co se chuan bi bai tap cho em."),
                ("s","Da cam on thay co! Em da dat lich roi a."),
                ("t","Thay Co xac nhan roi. Hen gap em toi thu 4!"),
            };
            for (int ti = 0; ti < Math.Min(tutorUsers.Count, 4); ti++)
                for (int si = 0; si < Math.Min(3, studentUsers.Count); si++)
                {
                    var sUser = studentUsers[(ti + si) % studentUsers.Count];
                    var tUser = tutorUsers[ti];
                    if (sUser.Id == tUser.Id) continue;
                    var baseT = DateTime.UtcNow.AddDays(-rng.Next(1, 14));
                    for (int mi = 0; mi < msgs.Length; mi++)
                    {
                        bool isS = msgs[mi].Item1 == "s";
                        try
                        {
                            db.Messages.Add(new Message { SenderId=isS?sUser.Id:tUser.Id, ReceiverId=isS?tUser.Id:sUser.Id, Content=msgs[mi].Item2, SentAt=baseT.AddMinutes(mi*5), IsRead=mi<msgs.Length-2 });
                            await db.SaveChangesAsync();
                        }
                        catch { db.ChangeTracker.Clear(); }
                    }
                }
        }

        // ── NOTIFICATIONS ────────────────────────────────────────
        if (!await db.Notifications.AnyAsync())
        {
            var notifs = new List<Notification>();
            foreach (var s in studentUsers)
            {
                notifs.Add(new Notification { UserId=s.Id, Title="Lich hoc duoc xac nhan!",    Content="Gia su da xac nhan lich hoc cua ban.",      Link="/Booking/MyBookings", IsRead=false, CreatedAt=DateTime.UtcNow.AddHours(-rng.Next(1,24))  });
                notifs.Add(new Notification { UserId=s.Id, Title="Nhac lich hoc ngay mai",     Content="Ban co buoi hoc luc 19:00 ngay mai.",        Link="/Booking/MyBookings", IsRead=false, CreatedAt=DateTime.UtcNow.AddHours(-rng.Next(2,48))  });
                notifs.Add(new Notification { UserId=s.Id, Title="Hoan thanh Quiz xuat sac!",  Content="Ban dat 9/10 trong bai Quiz Toan hoc.",      Link="/Quiz",               IsRead=false, CreatedAt=DateTime.UtcNow.AddHours(-rng.Next(1,24))  });
            }
            foreach (var t in tutorUsers)
            {
                notifs.Add(new Notification { UserId=t.Id, Title="Yeu cau dat lich moi!",      Content="Mot hoc vien vua gui yeu cau dat lich.",     Link="/Booking/TutorRequests", IsRead=false, CreatedAt=DateTime.UtcNow.AddHours(-rng.Next(1,12)) });
                notifs.Add(new Notification { UserId=t.Id, Title="Ban vua nhan danh gia moi!", Content="Hoc vien de lai danh gia 5 sao.",            Link="/Tutor/Dashboard",    IsRead=false, CreatedAt=DateTime.UtcNow.AddHours(-rng.Next(2,48)) });
                notifs.Add(new Notification { UserId=t.Id, Title="Huy hieu moi duoc mo khoa!",Content="Ban vua dat huy hieu Duoc yeu thich.",        Link="/Tutor/Dashboard",    IsRead=false, CreatedAt=DateTime.UtcNow.AddHours(-rng.Next(5,72)) });
            }
            try { db.Notifications.AddRange(notifs); await db.SaveChangesAsync(); } catch { db.ChangeTracker.Clear(); }
        }

        Console.WriteLine("=== SEED HOAN THANH! ===");
    }

    private static int td_Subs(int[] subs) => subs.Length > 0 ? subs[0] : 1;
}
