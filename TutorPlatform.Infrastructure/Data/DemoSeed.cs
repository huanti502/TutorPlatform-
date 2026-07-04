// DemoSeed.cs — Làm giàu dữ liệu demo toàn hệ thống (idempotent, chạy sau SeedAllAsync)
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;

namespace TutorPlatform.Infrastructure.Data;

public static class DemoSeed
{
    public static async Task EnrichAsync(AppDbContext db, UserManager<AppUser> userManager)
    {
        var rng = new Random(2026);

        (double lat, double lng) CityOf(string? area)
        {
            area = (area ?? "").ToLower();
            if (area.Contains("hà nội") || area.Contains("long biên") || area.Contains("cầu giấy") || area.Contains("đống đa") || area.Contains("thanh xuân")) return (21.0278, 105.8342);
            if (area.Contains("đà nẵng") || area.Contains("hải châu")) return (16.0544, 108.2022);
            if (area.Contains("cần thơ") || area.Contains("ninh kiều")) return (10.0452, 105.7469);
            return (10.7769, 106.7009); // TP.HCM
        }

        // ══════════ 1. THÊM GIA SƯ MỚI (đa dạng môn / khu vực / giá) ══════════
        var newTutors = new[]
        {
            new { Email="leminhkhoa@tutor.com", Name="Lê Minh Khoa", Phone="0901112223", Addr="Quận 3, TP.HCM",
                  Edu="Thạc sĩ Toán học - ĐH Sư phạm TP.HCM", Exp=6, Area="Quận 1, Quận 3, Bình Thạnh - TP.HCM",
                  Rate=180000m, Mode="Both", Subjects=new[]{1,3}, Approved=true, FaceOk=true,
                  Bio="Thầy Khoa chuyên luyện thi vào 10 và THPTQG môn Toán. Phương pháp dạy theo chuyên đề, có hệ thống bài tập riêng biên soạn 6 năm." },
            new { Email="phamquynhanh@tutor.com", Name="Phạm Quỳnh Anh", Phone="0902223334", Addr="Quận 7, TP.HCM",
                  Edu="Cử nhân Ngôn ngữ Anh - ĐH Ngoại thương, IELTS 8.0", Exp=5, Area="Quận 7, Nhà Bè - TP.HCM & Online",
                  Rate=280000m, Mode="Both", Subjects=new[]{10,2}, Approved=true, FaceOk=true,
                  Bio="Cô Quỳnh Anh đạt IELTS 8.0 (Listening 9.0). Chuyên luyện IELTS mục tiêu 6.5-7.5, lộ trình cá nhân hoá theo band điểm đầu vào." },
            new { Email="dovanhung@tutor.com", Name="Đỗ Văn Hùng", Phone="0903334445", Addr="TP. Thủ Đức, TP.HCM",
                  Edu="Cử nhân Hóa học - ĐH Khoa học Tự nhiên TP.HCM", Exp=4, Area="Thủ Đức, Quận 9 - TP.HCM",
                  Rate=150000m, Mode="Offline", Subjects=new[]{4,1}, Approved=true, FaceOk=true,
                  Bio="Thầy Hùng dạy Hóa THPT với sơ đồ tư duy phản ứng độc quyền. Học sinh nắm chắc chuỗi phản ứng hữu cơ chỉ sau 8 buổi." },
            new { Email="ngothihong@tutor.com", Name="Ngô Thị Hồng", Phone="0904445556", Addr="Bình Thạnh, TP.HCM",
                  Edu="Thạc sĩ Văn học - ĐH Sư phạm TP.HCM", Exp=10, Area="Bình Thạnh, Phú Nhuận - TP.HCM & Online",
                  Rate=130000m, Mode="Both", Subjects=new[]{6,7}, Approved=true, FaceOk=true,
                  Bio="Cô Hồng có 10 năm luyện thi Văn THPTQG, tác giả bộ đề cương nghị luận xã hội được nhiều trường sử dụng. Kiên nhẫn với học sinh sợ môn Văn." },
            new { Email="buiducthang@tutor.com", Name="Bùi Đức Thắng", Phone="0905556667", Addr="Quận 10, TP.HCM",
                  Edu="Kỹ sư CNTT - ĐH Bách Khoa TP.HCM", Exp=7, Area="Quận 10, Quận 5 - TP.HCM & Online",
                  Rate=250000m, Mode="Both", Subjects=new[]{5,9}, Approved=true, FaceOk=true,
                  Bio="Anh Thắng là kỹ sư phần mềm 7 năm kinh nghiệm, dạy lập trình Python/C++ cho học sinh - sinh viên và ôn thi HSG Tin học." },
            new { Email="truongmylinh@tutor.com", Name="Trương Mỹ Linh", Phone="0906667778", Addr="Cầu Giấy, Hà Nội",
                  Edu="Cử nhân Sư phạm Anh - ĐH Ngoại ngữ ĐHQGHN", Exp=6, Area="Cầu Giấy, Nam Từ Liêm - Hà Nội & Online",
                  Rate=240000m, Mode="Both", Subjects=new[]{2,10}, Approved=true, FaceOk=true,
                  Bio="Cô Linh chuyên tiếng Anh giao tiếp và IELTS cho học sinh cấp 2-3. Lớp học nhiều hoạt động tương tác, học sinh nói tiếng Anh từ buổi đầu." },
            new { Email="caoxuanphuc@tutor.com", Name="Cao Xuân Phúc", Phone="0907778889", Addr="Hải Châu, Đà Nẵng",
                  Edu="Tiến sĩ Toán - ĐH Đà Nẵng, giảng viên đại học", Exp=12, Area="Hải Châu - Đà Nẵng & Online toàn quốc",
                  Rate=170000m, Mode="Both", Subjects=new[]{9,1}, Approved=true, FaceOk=false,
                  Bio="Thầy Phúc là giảng viên đại học, chuyên Toán cao cấp, Giải tích, Đại số tuyến tính cho sinh viên năm nhất và ôn thi cao học." },
            new { Email="lythanhtung@tutor.com", Name="Lý Thanh Tùng", Phone="0908889990", Addr="Gò Vấp, TP.HCM",
                  Edu="Cử nhân CNTT - ĐH Công nghệ TP.HCM", Exp=3, Area="Online toàn quốc",
                  Rate=200000m, Mode="Online", Subjects=new[]{5}, Approved=false, FaceOk=false,
                  Bio="Anh Tùng dạy lập trình web (HTML/CSS/JS, C#) cho người mới bắt đầu. Hồ sơ đang chờ duyệt." },
        };

        foreach (var t in newTutors)
        {
            if (await userManager.FindByEmailAsync(t.Email) != null) continue;
            var user = new AppUser
            {
                UserName = t.Email, Email = t.Email, FullName = t.Name, PhoneNumber = t.Phone,
                Address = t.Addr, Role = "Tutor", EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(20, 300))
            };
            var created = await userManager.CreateAsync(user, "Tutor@123");
            if (!created.Succeeded) continue;
            await userManager.AddToRoleAsync(user, "Tutor");

            var profile = new TutorProfile
            {
                UserId = user.Id, Education = t.Edu, Bio = t.Bio, HourlyRate = t.Rate,
                IsApproved = t.Approved, TeachingMode = t.Mode, ExperienceYears = t.Exp, TeachingArea = t.Area,
                FaceVerified = t.FaceOk, FaceVerifiedAt = t.FaceOk ? DateTime.UtcNow.AddDays(-rng.Next(5, 45)) : null
            };
            db.TutorProfiles.Add(profile);
            await db.SaveChangesAsync();

            foreach (var sid in t.Subjects)
                db.TutorSubjects.Add(new TutorSubject { TutorProfileId = profile.Id, SubjectId = sid });

            // 2-3 khung lịch rảnh / tuần
            var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
                       .OrderBy(_ => rng.Next()).Take(rng.Next(2, 4));
            foreach (var d in days)
            {
                int sh = rng.Next(3) switch { 0 => 8, 1 => 14, _ => 18 };
                db.TutorAvailabilities.Add(new TutorAvailability
                {
                    TutorProfileId = profile.Id, DayOfWeek = d,
                    StartTime = new TimeSpan(sh, 0, 0), EndTime = new TimeSpan(sh + 4, 0, 0)
                });
            }
            await db.SaveChangesAsync();
        }

        // ══════════ 2. THÊM HỌC VIÊN MỚI ══════════
        var newStudents = new[]
        {
            (Email:"hocvien6@gmail.com",  Name:"Hồ Gia Bảo",     Addr:"Quận 1, TP.HCM"),
            (Email:"hocvien7@gmail.com",  Name:"Đặng Thu Trang", Addr:"Cầu Giấy, Hà Nội"),
            (Email:"hocvien8@gmail.com",  Name:"Võ Minh Quân",   Addr:"Thủ Đức, TP.HCM"),
            (Email:"hocvien9@gmail.com",  Name:"Phan Ngọc Ánh",  Addr:"Hải Châu, Đà Nẵng"),
            (Email:"hocvien10@gmail.com", Name:"Lương Văn Sơn",  Addr:"Ninh Kiều, Cần Thơ"),
            (Email:"hocvien11@gmail.com", Name:"Mai Thảo Vy",    Addr:"Gò Vấp, TP.HCM"),
        };
        foreach (var s in newStudents)
        {
            if (await userManager.FindByEmailAsync(s.Email) != null) continue;
            var user = new AppUser
            {
                UserName = s.Email, Email = s.Email, FullName = s.Name, Address = s.Addr,
                Role = "Student", EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(10, 200))
            };
            var created = await userManager.CreateAsync(user, "Student@123");
            if (created.Succeeded) await userManager.AddToRoleAsync(user, "Student");
        }

        // ══════════ 3. AVATAR CHO MỌI NGƯỜI DÙNG CHƯA CÓ ══════════
        var usersNoAvatar = await db.Users.Where(u => u.AvatarUrl == null || u.AvatarUrl == "").OrderBy(u => u.Email).ToListAsync();
        int av = 3;
        foreach (var u in usersNoAvatar)
        {
            u.AvatarUrl = $"https://i.pravatar.cc/300?img={(av % 70) + 1}";
            av += 7;
        }
        await db.SaveChangesAsync();

        // ══════════ 4. VỊ TRÍ BẢN ĐỒ + XÁC THỰC MẶT CHO GIA SƯ CŨ ══════════
        var profilesAll = await db.TutorProfiles.ToListAsync();
        foreach (var p in profilesAll)
        {
            if (p.Latitude == null)
            {
                var (lat, lng) = CityOf(p.TeachingArea);
                p.Latitude = lat + (rng.NextDouble() - 0.5) * 0.06;
                p.Longitude = lng + (rng.NextDouble() - 0.5) * 0.06;
            }
            if (!p.FaceVerified && p.IsApproved && rng.Next(10) < 8)
            {
                p.FaceVerified = true;
                p.FaceVerifiedAt = DateTime.UtcNow.AddDays(-rng.Next(3, 60));
            }
        }
        await db.SaveChangesAsync();

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@tutor.com");
        var tutorUsers = await db.Users.Where(u => u.Role == "Tutor").OrderBy(u => u.Email).ToListAsync();
        var studentUsers = await db.Users.Where(u => u.Role == "Student").OrderBy(u => u.Email).ToListAsync();

        // ══════════ 5. BLOG: SỬA ẢNH BÀI CŨ + THÊM BÀI MỚI ══════════
        var seeds = new[] { "study-desk", "classroom", "notebook", "library", "students", "laptop-study", "books-pile", "campus" };
        var noImg = await db.Posts.Where(p => p.ImageUrl == null || p.ImageUrl == "").ToListAsync();
        for (int i = 0; i < noImg.Count; i++)
            noImg[i].ImageUrl = $"https://picsum.photos/seed/{seeds[i % seeds.Length]}-{noImg[i].Id}/800/420";
        await db.SaveChangesAsync();

        var demoPosts = new List<Post>
        {
            new() { Title = "5 phương pháp học Toán hiệu quả cho học sinh mất gốc",
                Summary = "Mất gốc Toán không phải dấu chấm hết. Đây là lộ trình 5 bước đã giúp hàng trăm học sinh lấy lại nền tảng trong 3 tháng.",
                Content = "Nhiều học sinh nghĩ rằng mất gốc Toán là không thể cứu vãn, nhưng thực tế chỉ cần đúng phương pháp.\n\n1. Quay về đúng chỗ hổng: làm bài kiểm tra chẩn đoán để biết mình hổng từ lớp nào, chương nào — đừng học đuổi theo lớp hiện tại.\n\n2. Học theo chuỗi kiến thức: Toán là môn xây tầng. Muốn học đạo hàm phải chắc hàm số; muốn chắc hàm số phải vững biểu thức đại số.\n\n3. Mỗi ngày 30 phút còn hơn cuối tuần 4 tiếng: não ghi nhớ tốt hơn khi lặp lại ngắt quãng.\n\n4. Làm ít bài nhưng hiểu sâu: một bài giải ba cách tốt hơn ba bài giải một cách.\n\n5. Có người đồng hành: gia sư 1-1 giúp phát hiện lỗi sai ngay lập tức thay vì để lỗi thành thói quen.\n\nKiên trì 12 tuần, bạn sẽ ngạc nhiên với chính mình.",
                ImageUrl = "https://picsum.photos/seed/math-study/800/420", Views = 234 },
            new() { Title = "Lộ trình luyện IELTS từ 5.0 lên 7.0 trong 6 tháng",
                Summary = "Kinh nghiệm thực chiến từ giáo viên 8.0 IELTS: phân bổ thời gian cho 4 kỹ năng và những tài liệu thực sự đáng dùng.",
                Content = "Từ 5.0 lên 7.0 là hoàn toàn khả thi trong 6 tháng nếu học đúng trọng tâm.\n\nTháng 1-2: củng cố nền — ngữ pháp cốt lõi, 1500 từ vựng học thuật theo chủ đề. Nghe chép chính tả 15 phút mỗi ngày.\n\nTháng 3-4: luyện kỹ năng — Reading học cách scan/skim và bẫy paraphrase; Listening luyện đề Cambridge 13-18; Speaking thu âm chính mình và tự sửa.\n\nTháng 5-6: giai đoạn đề thi — mỗi tuần 2 đề full, chấm Writing với giáo viên (kỹ năng khó tự học nhất).\n\nSai lầm phổ biến: học tủ Writing task 2, bỏ qua phát âm, và luyện đề quá sớm khi nền chưa vững.",
                ImageUrl = "https://picsum.photos/seed/ielts-book/800/420", Views = 412 },
            new() { Title = "Học online hay học trực tiếp: chọn thế nào cho đúng?",
                Summary = "So sánh khách quan ưu nhược điểm của hai hình thức, và cách kết hợp cả hai để tối ưu chi phí lẫn hiệu quả.",
                Content = "Học online phù hợp khi: bạn ở xa trung tâm, cần linh hoạt giờ giấc, học các môn thiên về lý thuyết và ngôn ngữ. Chi phí thường thấp hơn 20-30% và tiết kiệm thời gian di chuyển.\n\nHọc trực tiếp phù hợp khi: học sinh nhỏ tuổi cần người kèm sát, các môn cần thao tác trên giấy nhiều, hoặc khi bạn dễ mất tập trung trước màn hình.\n\nCông thức kết hợp được nhiều phụ huynh áp dụng: 1 buổi trực tiếp + 1 buổi online mỗi tuần — buổi trực tiếp học kiến thức mới, buổi online chữa bài tập.\n\nDù chọn hình thức nào, yếu tố quyết định vẫn là chất lượng gia sư và sự đều đặn của lịch học.",
                ImageUrl = "https://picsum.photos/seed/online-learn/800/420", Views = 189 },
            new() { Title = "Cách phụ huynh đánh giá một gia sư giỏi trong 2 buổi đầu",
                Summary = "Đừng đợi hết tháng mới biết gia sư có hợp không. Có 6 dấu hiệu nhận biết ngay từ hai buổi học đầu tiên.",
                Content = "1. Buổi đầu có kiểm tra đầu vào không? Gia sư giỏi luôn dành 15-20 phút đánh giá trình độ thay vì dạy ngay.\n\n2. Có hỏi mục tiêu cụ thể? \"Con muốn 8 điểm thi học kỳ\" khác hoàn toàn \"con muốn giỏi Toán\".\n\n3. Giải thích có nhiều cách không? Khi học sinh không hiểu, gia sư giỏi đổi cách tiếp cận thay vì lặp lại to hơn.\n\n4. Có giao bài về nhà vừa sức? Bài quá khó gây nản, quá dễ gây chán.\n\n5. Có phản hồi cho phụ huynh sau buổi học? Vài dòng nhắn về tiến độ là dấu hiệu chuyên nghiệp.\n\n6. Học sinh có mong đến buổi tiếp theo không? Đây là chỉ báo trung thực nhất.",
                ImageUrl = "https://picsum.photos/seed/parent-tutor/800/420", Views = 156 },
            new() { Title = "Lập trình cho học sinh cấp 3: bắt đầu từ đâu?",
                Summary = "Con bạn thích máy tính và muốn thử lập trình? Đây là lộ trình phù hợp lứa tuổi, không cần cài đặt phức tạp.",
                Content = "Cấp 3 là thời điểm vàng để bắt đầu lập trình — đủ tư duy logic và còn nhiều thời gian khám phá.\n\nBước 1 (tháng đầu): Python. Cú pháp gần ngôn ngữ tự nhiên, chạy được ngay trên trình duyệt.\n\nBước 2 (tháng 2-4): làm dự án nhỏ theo sở thích — game đoán số, máy tính điểm trung bình, bot nhắc lịch học. Dự án cá nhân giữ lửa tốt hơn mọi giáo trình.\n\nBước 3 (tháng 5+): nếu định thi HSG Tin học thì chuyển sang C++ và thuật toán; nếu thích sản phẩm thì học web (HTML/CSS/JS).\n\nLưu ý cho phụ huynh: đừng ép học vì nghề hot — hãy để con làm ra thứ con thấy vui trước đã.",
                ImageUrl = "https://picsum.photos/seed/kid-coding/800/420", Views = 98 },
            new() { Title = "Bí quyết ghi nhớ từ vựng tiếng Nhật cho người mới học",
                Summary = "Kanji không đáng sợ như bạn nghĩ. Phương pháp liên tưởng hình ảnh + lặp lại ngắt quãng giúp nhớ 50 từ mỗi tuần.",
                Content = "Tiếng Nhật có ba bảng chữ, và Kanji là nỗi sợ lớn nhất của người mới. Nhưng có phương pháp.\n\n1. Học Kanji theo bộ thủ: chữ 休 (nghỉ) = người 亻 + cây 木 — người tựa gốc cây nghỉ ngơi. Câu chuyện hình ảnh giúp nhớ lâu gấp 5 lần học vẹt.\n\n2. Flashcard ngắt quãng với Anki: ôn đúng lúc sắp quên — ngày 1, 3, 7, 21.\n\n3. Học từ trong câu, không học từ đơn lẻ.\n\n4. Xem anime/drama có phụ đề Nhật (không phải phụ đề Việt) sau khi đạt N5.\n\nMục tiêu thực tế: N5 trong 4 tháng với 45 phút mỗi ngày.",
                ImageUrl = "https://picsum.photos/seed/japan-study/800/420", Views = 143 },
        };

        var authors = new List<AppUser>();
        if (admin != null) authors.Add(admin);
        authors.AddRange(tutorUsers.Take(5));
        var cmts = new[]
        {
            "Bài viết rất hữu ích, cảm ơn thầy/cô đã chia sẻ!",
            "Em đã áp dụng cách số 3 và thấy tiến bộ rõ rệt.",
            "Cho em hỏi tài liệu ở phần 2 tìm ở đâu được ạ?",
            "Đúng cái mình đang cần, lưu lại ngay.",
            "Phụ huynh như mình đọc xong thấy sáng ra nhiều điều.",
            "Hay quá, mong có thêm nhiều bài như thế này ạ."
        };
        int pi = 0;
        foreach (var post in demoPosts)
        {
            if (await db.Posts.AnyAsync(p => p.Title == post.Title)) { pi++; continue; }
            post.AuthorId = authors[pi % Math.Max(1, authors.Count)].Id;
            post.IsPublished = true;
            post.CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(3, 90));
            db.Posts.Add(post);
            await db.SaveChangesAsync();

            int nC = rng.Next(1, 4);
            for (int c = 0; c < nC && c < studentUsers.Count; c++)
                db.Comments.Add(new Comment
                {
                    PostId = post.Id,
                    AuthorId = studentUsers[(post.Id + c) % studentUsers.Count].Id,
                    Content = cmts[rng.Next(cmts.Length)],
                    CreatedAt = post.CreatedAt.AddDays(rng.Next(1, 10))
                });
            var likers = studentUsers.Concat(tutorUsers).OrderBy(_ => rng.Next()).Take(rng.Next(3, 9));
            foreach (var lk in likers)
                db.PostLikes.Add(new PostLike { PostId = post.Id, UserId = lk.Id });
            await db.SaveChangesAsync();
            pi++;
        }

        // ══════════ 6. TÀI LIỆU: TRỎ FILE THẬT + BỔ SUNG CHO ĐỦ 8 ══════════
        var realFiles = new (string File, string Title, string Desc, long Size)[]
        {
            ("de-cuong-toan-thpt.pdf", "Đề cương ôn tập Toán học THPT", "Tổng hợp 4 chuyên đề trọng tâm: hàm số, đạo hàm, tích phân, số phức kèm dạng bài điển hình.", 22730),
            ("tu-vung-ielts-band7.pdf", "500 từ vựng IELTS mục tiêu Band 7+", "Từ vựng học thuật theo chủ đề Education, Environment, Technology kèm mẹo ghi nhớ.", 22915),
            ("bai-tap-vat-ly-song.pdf", "Bài tập Vật lý: Sóng cơ và Sóng âm", "Tóm tắt công thức + bài tập giao thoa, sóng dừng có đáp án gợi ý.", 24311),
            ("lo-trinh-hoc-lap-trinh.pdf", "Lộ trình học Lập trình Web cho người mới", "Kế hoạch 16 tuần từ HTML/CSS tới dự án CRUD hoàn chỉnh, kèm lời khuyên thực tế.", 25256),
            ("de-cuong-toan-thpt.pdf", "Bộ đề kiểm tra 15 phút Đại số", "Tuyển tập đề kiểm tra nhanh giúp học sinh rà soát kiến thức từng chương.", 22730),
            ("tu-vung-ielts-band7.pdf", "Collocations thường gặp trong IELTS Writing", "Danh sách cụm từ tự nhiên giúp bài viết đạt tiêu chí Lexical Resource band 7.", 22915),
            ("bai-tap-vat-ly-song.pdf", "Công thức Vật lý 12 cần nhớ", "Bảng công thức rút gọn theo chương, in ra ôn trước giờ thi rất tiện.", 24311),
            ("lo-trinh-hoc-lap-trinh.pdf", "Bài tập thực hành HTML CSS cơ bản", "10 bài thực hành nhỏ theo độ khó tăng dần cho người mới học web.", 25256),
        };

        var docsExisting = await db.Documents.ToListAsync();
        int fi = 0;
        foreach (var d in docsExisting.Where(d => !d.FilePath.StartsWith("http")))
        {
            var rf = realFiles[fi % 4];
            d.FilePath = $"/uploads/documents/{rf.File}";
            d.FileName = rf.File;
            d.FileType = "pdf";
            d.FileSize = rf.Size;
            fi++;
        }
        await db.SaveChangesAsync();

        int docCount = await db.Documents.CountAsync();
        for (int i = docCount; i < 8 && tutorUsers.Count > 0; i++)
        {
            var rf = realFiles[i % realFiles.Length];
            db.Documents.Add(new Document
            {
                Title = rf.Title, Description = rf.Desc,
                FilePath = $"/uploads/documents/{rf.File}", FileName = rf.File,
                FileType = "pdf", FileSize = rf.Size,
                UploaderId = tutorUsers[i % tutorUsers.Count].Id,
                IsPublic = true, DownloadCount = rng.Next(5, 60),
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(5, 60))
            });
        }
        await db.SaveChangesAsync();

        // ══════════ 7. SINH LỊCH SỬ HỌC CHO GIA SƯ CÒN "TRỐNG" ══════════
        var goodComments = new[]
        {
            "Thầy dạy rất dễ hiểu, con mình tiến bộ rõ sau một tháng.",
            "Cô nhiệt tình, chuẩn bị bài kỹ, luôn đúng giờ. Rất đáng tin cậy!",
            "Giảng bài logic, có nhiều ví dụ thực tế. Sẽ tiếp tục học lâu dài.",
            "Gia sư thân thiện, biết cách tạo động lực cho học sinh lười học như em.",
            "Phương pháp dạy hay, sau 5 buổi em đã tự giải được dạng bài khó.",
            "Rất hài lòng, thầy còn gửi bài tập bổ trợ sau mỗi buổi học."
        };
        var mediumComments = new[]
        {
            "Dạy ổn nhưng đôi khi hơi nhanh, mong thầy chậm lại ở phần khó.",
            "Kiến thức chắc, tuy nhiên bài tập về nhà hơi nhiều so với lịch học của em.",
            "Buổi học tốt, chỉ tiếc là đôi lúc bắt đầu trễ 5-10 phút."
        };
        var badComments = new[]
        {
            "Gia sư đến muộn 15 phút và chưa chuẩn bị giáo án, khá thất vọng.",
            "Giảng bài thiếu ví dụ, em hỏi lại thì giải thích y hệt lần đầu.",
            "Hai buổi liền đổi lịch sát giờ, ảnh hưởng kế hoạch của gia đình."
        };

        var tutorsForHistory = await db.TutorProfiles
            .Include(t => t.TutorSubjects)
            .Include(t => t.Bookings)
            .Where(t => t.IsApproved)
            .ToListAsync();

        foreach (var tutor in tutorsForHistory)
        {
            if (tutor.Bookings.Count(b => b.Status == "Completed") >= 3) continue;
            if (tutor.TutorSubjects.Count == 0 || studentUsers.Count == 0) continue;

            int n = rng.Next(8, 15);
            for (int i = 0; i < n; i++)
            {
                var stu = studentUsers[rng.Next(studentUsers.Count)];
                var subId = tutor.TutorSubjects.ElementAt(rng.Next(tutor.TutorSubjects.Count)).SubjectId;
                var daysAgo = rng.Next(3, 150);
                var start = DateTime.UtcNow.Date.AddDays(-daysAgo).AddHours(rng.Next(8, 19));
                var dur = rng.Next(2) == 0 ? 1.5 : 2.0;
                var booking = new Booking
                {
                    StudentId = stu.Id, TutorProfileId = tutor.Id, SubjectId = subId,
                    StartTime = start, EndTime = start.AddHours(dur),
                    Status = "Completed", IsPaid = true,
                    TeachingMode = tutor.TeachingMode == "Offline" ? "Offline" : "Online",
                    MeetingRoomId = $"TutorPlatform-h{tutor.Id}-{Guid.NewGuid().ToString("N")[..8]}",
                    CreatedAt = start.AddDays(-rng.Next(1, 5))
                };
                db.Bookings.Add(booking);
                await db.SaveChangesAsync();

                if (rng.Next(10) < 8)
                {
                    int roll = rng.Next(10);
                    (int rating, string cmt) = roll < 6
                        ? (5, goodComments[rng.Next(goodComments.Length)])
                        : roll < 8
                            ? (4, goodComments[rng.Next(goodComments.Length)])
                            : roll < 9
                                ? (3, mediumComments[rng.Next(mediumComments.Length)])
                                : (2, badComments[rng.Next(badComments.Length)]);
                    db.Reviews.Add(new Review
                    {
                        StudentId = stu.Id, TutorProfileId = tutor.Id, BookingId = booking.Id,
                        Rating = rating, Comment = cmt,
                        CreatedAt = booking.EndTime.AddHours(rng.Next(1, 48))
                    });
                }
            }

            // 1-2 buổi sắp tới
            for (int i = 0; i < rng.Next(1, 3); i++)
            {
                var stu = studentUsers[rng.Next(studentUsers.Count)];
                var subId = tutor.TutorSubjects.ElementAt(rng.Next(tutor.TutorSubjects.Count)).SubjectId;
                var start = DateTime.UtcNow.Date.AddDays(rng.Next(1, 10)).AddHours(rng.Next(8, 19));
                var st = rng.Next(2) == 0 ? "Confirmed" : "Pending";
                db.Bookings.Add(new Booking
                {
                    StudentId = stu.Id, TutorProfileId = tutor.Id, SubjectId = subId,
                    StartTime = start, EndTime = start.AddHours(1.5),
                    Status = st,
                    TeachingMode = tutor.TeachingMode == "Offline" ? "Offline" : "Online",
                    MeetingRoomId = st == "Confirmed" ? $"TutorPlatform-u{tutor.Id}-{Guid.NewGuid().ToString("N")[..8]}" : null,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                });
            }
            await db.SaveChangesAsync();
        }

        // ══════════ 8. THANH TOÁN "PAID" CHO MỌI BUỔI HOÀN THÀNH CHƯA CÓ ══════════
        var paidBookingIds = await db.Payments.Select(p => p.BookingId).ToListAsync();
        var needPay = await db.Bookings
            .Include(b => b.TutorProfile)
            .Where(b => b.Status == "Completed" && !paidBookingIds.Contains(b.Id))
            .ToListAsync();
        long order = 202600001 + paidBookingIds.Count;
        foreach (var b in needPay)
        {
            var hours = (decimal)(b.EndTime - b.StartTime).TotalHours;
            var amount = Math.Round(hours * (b.TutorProfile?.HourlyRate ?? 150000m), 0);
            db.Payments.Add(new Payment
            {
                BookingId = b.Id, OrderCode = order, Amount = amount, Status = "Paid",
                TransactionNo = $"VNP{order}", ResponseCode = "00",
                CreatedAt = b.StartTime.AddDays(-1),
                PaidAt = b.StartTime.AddDays(-1).AddMinutes(rng.Next(5, 120))
            });
            b.IsPaid = true;
            order++;
        }
        await db.SaveChangesAsync();

        // ══════════ 9. GÓI HỌC CHO GIA SƯ CHƯA CÓ + LƯỢT MUA ══════════
        var pkgTutorIds = await db.LessonPackages.Select(p => p.TutorProfileId).Distinct().ToListAsync();
        var pkgTargets = await db.TutorProfiles
            .Where(t => t.IsApproved && !pkgTutorIds.Contains(t.Id))
            .OrderBy(t => t.Id).Take(8).ToListAsync();
        foreach (var t in pkgTargets)
        {
            db.LessonPackages.Add(new LessonPackage { TutorProfileId = t.Id, Name = "Gói 5 buổi tiết kiệm", SessionCount = 5, Price = Math.Round(t.HourlyRate * 5 * 0.95m, 0), IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-45) });
            db.LessonPackages.Add(new LessonPackage { TutorProfileId = t.Id, Name = "Gói 10 buổi ưu đãi", SessionCount = 10, Price = Math.Round(t.HourlyRate * 10 * 0.88m, 0), IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-45) });
        }
        await db.SaveChangesAsync();

        if (!await db.PackagePurchases.AnyAsync())
        {
            var pkgs = await db.LessonPackages.OrderBy(p => p.Id).Take(6).ToListAsync();
            if (studentUsers.Count >= 2 && pkgs.Count >= 3)
            {
                long po = 202699001;
                var buy = new[] { (pkgs[0], studentUsers[0], 3), (pkgs[2], studentUsers[1], 7), (pkgs[1], studentUsers[1 % studentUsers.Count], 0) };
                foreach (var (pkg, stu, remaining) in buy)
                {
                    db.PackagePurchases.Add(new PackagePurchase
                    {
                        LessonPackageId = pkg.Id, StudentId = stu.Id, TutorProfileId = pkg.TutorProfileId,
                        TotalSessions = pkg.SessionCount, RemainingSessions = remaining,
                        PricePaid = pkg.Price, OrderCode = po++,
                        Status = remaining > 0 ? "Active" : "Used",
                        CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(10, 40)),
                        ActivatedAt = DateTime.UtcNow.AddDays(-rng.Next(5, 10))
                    });
                }
                await db.SaveChangesAsync();
            }
        }

        // ══════════ 10. MÃ GIẢM GIÁ ══════════
        if (!await db.Coupons.AnyAsync())
        {
            db.Coupons.AddRange(
                new Coupon { Code = "WELCOME10", DiscountType = "Percent", DiscountValue = 10, MaxDiscount = 50000, MinOrder = 0, UsageLimit = 0, UsedCount = 14, IsActive = true },
                new Coupon { Code = "GIAM50K", DiscountType = "Amount", DiscountValue = 50000, MinOrder = 300000, UsageLimit = 100, UsedCount = 27, IsActive = true },
                new Coupon { Code = "HE2026", DiscountType = "Percent", DiscountValue = 15, MaxDiscount = 100000, MinOrder = 200000, ExpiresAt = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc), UsageLimit = 200, UsedCount = 8, IsActive = true }
            );
            await db.SaveChangesAsync();
        }

        // ══════════ 11. GHI CHÚ BUỔI HỌC + TÓM TẮT AI MẪU ══════════
        if (await db.LessonNotes.CountAsync() < 5)
        {
            var noted = await db.LessonNotes.Select(n => n.BookingId).ToListAsync();
            var done = await db.Bookings
                .Include(b => b.TutorProfile)
                .Include(b => b.Subject)
                .Where(b => b.Status == "Completed" && !noted.Contains(b.Id))
                .OrderByDescending(b => b.EndTime)
                .Take(6).ToListAsync();
            foreach (var b in done)
            {
                var subj = b.Subject?.Name ?? "môn học";
                db.LessonNotes.Add(new LessonNote
                {
                    BookingId = b.Id,
                    TutorId = b.TutorProfile?.UserId ?? "",
                    StudentId = b.StudentId,
                    Content = $"Buổi học {subj}: đã chữa toàn bộ bài tập về nhà, giảng phần kiến thức mới và làm 5 bài luyện tập tại lớp. Học viên tiếp thu tốt nhưng còn nhầm lẫn ở bước biến đổi trung gian, cần luyện thêm dạng bài tương tự trong tuần.",
                    AiSummary = $"Đã học\n- Chữa bài tập về nhà, giảng kiến thức mới của {subj}\n- Làm 5 bài luyện tập tại lớp\n\nCần ôn tập\n- Bước biến đổi trung gian còn nhầm lẫn\n\nGợi ý luyện tập\n- Làm thêm 3-5 bài cùng dạng trong tuần\n- Ghi lại lỗi sai vào sổ tay để tránh lặp lại",
                    CreatedAt = b.EndTime.AddHours(1),
                    UpdatedAt = b.EndTime.AddHours(1)
                });
            }
            await db.SaveChangesAsync();
        }

        Console.WriteLine("DemoSeed v2: đã bổ sung gia sư/học viên mới, lịch sử học, blog có ảnh, tài liệu, thanh toán.");
    }

    // ══════════════════════════════════════════════════════════════
    // Làm giàu dữ liệu "gamification" & học tập: XP, bài tập, lớp cần
    // gia sư, đánh giá 2 chiều. Gọi SAU khi các bảng đã được tạo.
    // Idempotent: chỉ thêm khi dữ liệu còn thiếu, an toàn khi chạy lại.
    // ══════════════════════════════════════════════════════════════
    public static async Task SeedGamificationAsync(AppDbContext db)
    {
        var rng = new Random(2027);

        string LevelOf(int xp) =>
            xp >= 5000 ? "Huyền thoại" :
            xp >= 2000 ? "Chuyên gia" :
            xp >= 1000 ? "Xuất sắc" :
            xp >= 600 ? "Thành thạo" :
            xp >= 300 ? "Chăm chỉ" :
            xp >= 100 ? "Học viên" : "Mới bắt đầu";

        // ── 1. XP cho học viên (để bảng xếp hạng sinh động) ──
        // Chỉ gán cho học viên đang có 0 XP → không ghi đè XP thật đã tích luỹ.
        var students = await db.Users.Where(u => u.Role == "Student").ToListAsync();
        var zeroXpStudents = students.Where(s => s.XpPoints == 0).ToList();
        if (zeroXpStudents.Count > 0)
        {
            // Tạo phân bố XP đa dạng: vài người top cao, phần lớn ở giữa, số ít mới.
            int idx = 0;
            foreach (var s in zeroXpStudents)
            {
                int xp = (idx % 7) switch
                {
                    0 => rng.Next(1200, 2600),   // top
                    1 => rng.Next(600, 1200),
                    2 => rng.Next(300, 700),
                    3 => rng.Next(150, 400),
                    4 => rng.Next(80, 260),
                    5 => rng.Next(30, 150),
                    _ => rng.Next(0, 90),        // mới bắt đầu
                };
                // làm tròn về bội số 10 cho đẹp
                xp = xp / 10 * 10;
                s.XpPoints = xp;
                s.XpLevel = LevelOf(xp);
                idx++;
            }
            await db.SaveChangesAsync();
        }

        // Cho gia sư một ít XP nữa (họ cũng xuất hiện ở Battle/Top XP).
        var tutorsZero = await db.Users.Where(u => u.Role == "Tutor" && u.XpPoints == 0).ToListAsync();
        foreach (var t in tutorsZero)
        {
            int xp = rng.Next(20, 400) / 10 * 10;
            t.XpPoints = xp;
            t.XpLevel = LevelOf(xp);
        }
        if (tutorsZero.Count > 0) await db.SaveChangesAsync();

        // ── 2. Bài tập mẫu (Assignments) gắn với buổi học đã có ──
        if (await db.Assignments.CountAsync() < 8)
        {
            var withAssignment = await db.Assignments.Select(a => a.BookingId).ToListAsync();
            var bookingsForHw = await db.Bookings
                .Include(b => b.TutorProfile)
                .Include(b => b.Subject)
                .Where(b => (b.Status == "Completed" || b.Status == "Confirmed")
                    && b.TutorProfile != null
                    && !withAssignment.Contains(b.Id))
                .OrderByDescending(b => b.StartTime)
                .Take(14)
                .ToListAsync();

            var hwTitles = new[]
            {
                ("Bài tập về nhà buổi {0}", "Hoàn thành các bài trong phiếu bài tập đã gửi, chú ý trình bày các bước rõ ràng."),
                ("Ôn tập chương {0}", "Làm lại toàn bộ ví dụ trong chương và 5 bài luyện tập cuối chương."),
                ("Luyện đề số {0}", "Làm đề trong 45 phút rồi tự chấm, ghi lại câu sai để buổi sau chữa."),
                ("Bài tập nâng cao {0}", "Thử sức 3 bài nâng cao, không bắt buộc làm hết nhưng cần ghi ý tưởng."),
            };
            var grades = new[] { "9/10", "8/10", "7/10", "Đạt", "10/10", "6/10" };
            var feedbacks = new[]
            {
                "Làm bài tốt, trình bày sạch. Cố gắng phát huy!",
                "Đúng phần lớn, còn sai bước biến đổi ở câu 3. Xem lại nhé.",
                "Có tiến bộ so với buổi trước, tiếp tục luyện thêm dạng này.",
                "Cần cẩn thận hơn ở phần tính toán, kiến thức đã nắm được.",
            };

            int k = 0;
            foreach (var b in bookingsForHw)
            {
                var (titleTpl, desc) = hwTitles[rng.Next(hwTitles.Length)];
                var created = b.StartTime.AddHours(2);
                var due = created.AddDays(rng.Next(3, 8));

                // Trạng thái đa dạng: chưa làm / đã nộp / đã chấm.
                int roll = rng.Next(10);
                string status; DateTime? submittedAt = null, gradedAt = null;
                string? submissionText = null, grade = null, feedback = null;

                if (b.Status == "Completed" && roll < 5)
                {
                    status = "Graded";
                    submittedAt = created.AddDays(rng.Next(1, 4));
                    submissionText = "Em đã hoàn thành bài tập, có một câu em chưa chắc chắn ạ.";
                    gradedAt = submittedAt.Value.AddHours(rng.Next(4, 30));
                    grade = grades[rng.Next(grades.Length)];
                    feedback = feedbacks[rng.Next(feedbacks.Length)];
                }
                else if (roll < 8)
                {
                    status = "Submitted";
                    submittedAt = created.AddDays(rng.Next(1, 5));
                    submissionText = "Em nộp bài ạ, em làm hết các câu bắt buộc.";
                }
                else
                {
                    status = "Assigned";
                }

                db.Assignments.Add(new Assignment
                {
                    BookingId = b.Id,
                    TutorId = b.TutorProfile!.UserId,
                    StudentId = b.StudentId,
                    Title = string.Format(titleTpl, rng.Next(1, 6)) + $" - {b.Subject?.Name}",
                    Description = desc,
                    DueDate = due,
                    CreatedAt = created,
                    SubmissionText = submissionText,
                    SubmittedAt = submittedAt,
                    Grade = grade,
                    Feedback = feedback,
                    GradedAt = gradedAt,
                    Status = status
                });
                k++;
            }
            await db.SaveChangesAsync();
        }

        // ── 3. Lớp cần gia sư (ClassRequests) ──
        if (await db.ClassRequests.CountAsync(c => c.Status == "Open") < 5 && students.Count > 0)
        {
            var subjects = await db.Subjects.Where(s => s.IsActive).ToListAsync();
            int? SubjId(string name) => subjects.FirstOrDefault(s => s.Name.Contains(name))?.Id;

            var samples = new (string Title, string Desc, string Subj, string Mode, string? Loc, string Sched, int Sess, decimal Budget)[]
            {
                ("Toán lớp 8 - lấy lại gốc, 2 buổi/tuần", "- Học sinh Nam, học lực trung bình, cần kèm lại kiến thức nền lớp 7-8.\n- Yêu cầu: gia sư kiên nhẫn, dạy dễ hiểu.\n- Ưu tiên sinh viên sư phạm.", "Toán", "Offline", "P.25, Q. Bình Thạnh, TP.HCM", "Tối T2, T5 từ 19h30", 2, 180000),
                ("Tiếng Anh giao tiếp cho người đi làm", "- Học viên đã đi làm, mất gốc, muốn giao tiếp cơ bản trong công việc.\n- Học online, linh hoạt giờ.", "Anh", "Online", null, "Tối T3, T5, CN", 3, 250000),
                ("Luyện thi IELTS mục tiêu 6.5", "- Học sinh lớp 12, hiện band 5.0, cần đạt 6.5 trong 4 tháng.\n- Tập trung Writing và Speaking.", "Anh", "Both", "Q.1, TP.HCM", "T7, CN buổi sáng", 2, 400000),
                ("Vật lý lớp 11 - nâng cao", "- Học sinh khá, muốn học nâng cao chuẩn bị thi HSG.\n- Cần gia sư chuyên Lý, ra bài tập khó.", "Lý", "Online", null, "Tối T2, T6 từ 20h", 2, 300000),
                ("Hóa học lớp 10 - cơ bản đến nâng cao", "- Học sinh Nữ, mất gốc Hóa, cần xây lại từ đầu.\n- Kèm sát chương trình trên lớp.", "Hóa", "Offline", "TP. Thủ Đức, TP.HCM", "Chiều T4, T7", 2, 200000),
                ("Lập trình Python cho học sinh cấp 3", "- Học sinh muốn học Python từ cơ bản, định hướng thi tin học trẻ.\n- Gia sư biết dạy trực quan, có project.", "Tin", "Online", null, "Tối T3, T6", 2, 280000),
                ("Ngữ văn lớp 9 - ôn thi vào 10", "- Học sinh cần ôn thi chuyển cấp, yếu phần nghị luận.\n- Gia sư có kinh nghiệm luyện thi vào 10.", "Văn", "Offline", "Q. Gò Vấp, TP.HCM", "Tối T2, T4, T6", 3, 220000),
                ("Toán tư duy cho học sinh tiểu học", "- Bé lớp 4, phụ huynh muốn phát triển tư duy Toán sớm.\n- Gia sư nhẹ nhàng, tạo hứng thú học.", "Toán", "Offline", "P. Hiệp Bình Chánh, TP. Thủ Đức", "Chiều T3, T5", 2, 150000),
            };

            foreach (var s in samples)
            {
                var student = students[rng.Next(students.Count)];
                db.ClassRequests.Add(new ClassRequest
                {
                    StudentId = student.Id,
                    Title = s.Title,
                    Description = s.Desc,
                    SubjectId = SubjId(s.Subj),
                    Mode = s.Mode,
                    Location = s.Loc,
                    Schedule = s.Sched,
                    SessionsPerWeek = s.Sess,
                    BudgetPerSession = s.Budget,
                    Status = "Open",
                    CreatedAt = DateTime.UtcNow.AddHours(-rng.Next(1, 120))
                });
            }
            await db.SaveChangesAsync();
        }

        // ── 4. Đánh giá 2 chiều (gia sư đánh giá học viên) ──
        if (await db.StudentReviews.CountAsync() < 5)
        {
            var reviewed = await db.StudentReviews.Select(r => r.BookingId).ToListAsync();
            var completedForSr = await db.Bookings
                .Include(b => b.TutorProfile)
                .Where(b => b.Status == "Completed" && b.TutorProfile != null && !reviewed.Contains(b.Id))
                .OrderByDescending(b => b.EndTime)
                .Take(12)
                .ToListAsync();

            var srComments = new[]
            {
                "Học viên chăm chỉ, làm bài đầy đủ và đúng giờ.",
                "Tiếp thu nhanh, chủ động đặt câu hỏi khi chưa hiểu.",
                "Có tinh thần cầu tiến, tiến bộ rõ qua từng buổi.",
                "Ngoan, hợp tác tốt trong giờ học. Cần luyện thêm ở nhà.",
                "Thái độ học tập tích cực, làm bài tập nghiêm túc.",
            };

            foreach (var b in completedForSr)
            {
                if (rng.Next(10) < 3) continue; // không phải buổi nào cũng có đánh giá
                db.StudentReviews.Add(new StudentReview
                {
                    BookingId = b.Id,
                    TutorProfileId = b.TutorProfileId,
                    StudentId = b.StudentId,
                    Rating = rng.Next(10) < 8 ? 5 : 4,
                    Comment = srComments[rng.Next(srComments.Length)],
                    CreatedAt = b.EndTime.AddHours(rng.Next(1, 24))
                });
            }
            await db.SaveChangesAsync();
        }

        Console.WriteLine("DemoSeed gamification: đã bổ sung XP, bài tập, lớp cần gia sư, đánh giá 2 chiều.");
    }
}
