// DemoSeed.cs — Làm giàu dữ liệu demo cho toàn hệ thống (idempotent, chạy sau SeedAllAsync)
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;

namespace TutorPlatform.Infrastructure.Data;

public static class DemoSeed
{
    public static async Task EnrichAsync(AppDbContext db)
    {
        var rng = new Random(2026);

        // ══════════ 1. AVATAR CHO MỌI NGƯỜI DÙNG ══════════
        var users = await db.Users.Where(u => u.AvatarUrl == null || u.AvatarUrl == "").ToListAsync();
        int av = 1;
        foreach (var u in users.OrderBy(u => u.Email))
        {
            // pravatar: ảnh chân dung thật, ổn định theo chỉ số
            u.AvatarUrl = $"https://i.pravatar.cc/300?img={(av % 70) + 1}";
            av += 7;
        }

        // ══════════ 2. VỊ TRÍ BẢN ĐỒ + XÁC THỰC KHUÔN MẶT CHO GIA SƯ ══════════
        var profiles = await db.TutorProfiles.ToListAsync();
        // toạ độ trung tâm theo khu vực dạy
        (double lat, double lng) CityOf(string area)
        {
            area = (area ?? "").ToLower();
            if (area.Contains("hà nội") || area.Contains("long biên") || area.Contains("đống đa")) return (21.0278, 105.8342);
            if (area.Contains("đà nẵng") || area.Contains("hải châu")) return (16.0544, 108.2022);
            if (area.Contains("cần thơ") || area.Contains("ninh kiều")) return (10.0452, 105.7469);
            return (10.7769, 106.7009); // TP.HCM
        }
        foreach (var p in profiles)
        {
            if (p.Latitude == null)
            {
                var (lat, lng) = CityOf(p.TeachingArea);
                p.Latitude = lat + (rng.NextDouble() - 0.5) * 0.06;   // lệch ~±3km
                p.Longitude = lng + (rng.NextDouble() - 0.5) * 0.06;
            }
            if (!p.FaceVerified && p.IsApproved && rng.Next(10) < 8) // ~80% gia sư đã xác thực mặt
            {
                p.FaceVerified = true;
                p.FaceVerifiedAt = DateTime.UtcNow.AddDays(-rng.Next(3, 60));
            }
        }
        await db.SaveChangesAsync();

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@tutor.com");
        var tutorUsers = await db.Users.Where(u => u.Role == "Tutor").OrderBy(u => u.Email).ToListAsync();
        var studentUsers = await db.Users.Where(u => u.Role == "Student").OrderBy(u => u.Email).ToListAsync();

        // ══════════ 3. BLOG: BÀI VIẾT + BÌNH LUẬN + LƯỢT THÍCH ══════════
        if (!await db.Posts.AnyAsync() && admin != null && tutorUsers.Count > 0)
        {
            var posts = new List<Post>
            {
                new() { Title = "5 phương pháp học Toán hiệu quả cho học sinh mất gốc",
                    Summary = "Mất gốc Toán không phải dấu chấm hết. Đây là lộ trình 5 bước đã giúp hàng trăm học sinh lấy lại nền tảng trong 3 tháng.",
                    Content = "Nhiều học sinh nghĩ rằng mất gốc Toán là không thể cứu vãn, nhưng thực tế chỉ cần đúng phương pháp.\n\n1. Quay về đúng chỗ hổng: làm bài kiểm tra chẩn đoán để biết mình hổng từ lớp nào, chương nào — đừng học đuổi theo lớp hiện tại.\n\n2. Học theo chuỗi kiến thức: Toán là môn xây tầng. Ví dụ muốn học đạo hàm phải chắc hàm số; muốn chắc hàm số phải vững biểu thức đại số.\n\n3. Mỗi ngày 30 phút còn hơn cuối tuần 4 tiếng: não ghi nhớ tốt hơn khi lặp lại ngắt quãng.\n\n4. Làm ít bài nhưng hiểu sâu: một bài giải ba cách tốt hơn ba bài giải một cách.\n\n5. Có người đồng hành: gia sư 1-1 giúp phát hiện lỗi sai ngay lập tức thay vì để lỗi thành thói quen.\n\nKiên trì 12 tuần, bạn sẽ ngạc nhiên với chính mình.",
                    ImageUrl = "https://picsum.photos/seed/math-study/800/420", Views = 234 },
                new() { Title = "Lộ trình luyện IELTS từ 5.0 lên 7.0 trong 6 tháng",
                    Summary = "Kinh nghiệm thực chiến từ giáo viên 8.0 IELTS: phân bổ thời gian cho 4 kỹ năng và những tài liệu thực sự đáng dùng.",
                    Content = "Từ 5.0 lên 7.0 là hoàn toàn khả thi trong 6 tháng nếu học đúng trọng tâm.\n\nTháng 1-2: củng cố nền — ngữ pháp cốt lõi (12 thì rút về 6 thì hay dùng), 1500 từ vựng học thuật theo chủ đề. Nghe chép chính tả 15 phút mỗi ngày.\n\nTháng 3-4: luyện kỹ năng — Reading học cách scan/skim và bẫy paraphrase; Listening luyện đề Cambridge 13-18; Speaking thu âm chính mình và tự sửa.\n\nTháng 5-6: giai đoạn đề thi — mỗi tuần 2 đề full, chấm Writing với giáo viên (đây là kỹ năng khó tự học nhất).\n\nSai lầm phổ biến: học tủ Writing task 2, bỏ qua phát âm, và luyện đề quá sớm khi nền chưa vững.",
                    ImageUrl = "https://picsum.photos/seed/ielts-book/800/420", Views = 412 },
                new() { Title = "Học online hay học trực tiếp: chọn thế nào cho đúng?",
                    Summary = "So sánh khách quan ưu nhược điểm của hai hình thức, và cách kết hợp cả hai để tối ưu chi phí lẫn hiệu quả.",
                    Content = "Học online phù hợp khi: bạn ở xa trung tâm, cần linh hoạt giờ giấc, học các môn thiên về lý thuyết và ngôn ngữ. Chi phí thường thấp hơn 20-30% và tiết kiệm thời gian di chuyển.\n\nHọc trực tiếp phù hợp khi: học sinh nhỏ tuổi cần người kèm sát, các môn cần thao tác trên giấy nhiều như Toán hình, hoặc khi bạn dễ mất tập trung trước màn hình.\n\nCông thức kết hợp được nhiều phụ huynh áp dụng: 1 buổi trực tiếp + 1 buổi online mỗi tuần — buổi trực tiếp học kiến thức mới, buổi online chữa bài tập.\n\nDù chọn hình thức nào, yếu tố quyết định vẫn là chất lượng gia sư và sự đều đặn của lịch học.",
                    ImageUrl = "https://picsum.photos/seed/online-learn/800/420", Views = 189 },
                new() { Title = "Cách phụ huynh đánh giá một gia sư giỏi trong 2 buổi đầu",
                    Summary = "Đừng đợi hết tháng mới biết gia sư có hợp không. Có 6 dấu hiệu nhận biết ngay từ hai buổi học đầu tiên.",
                    Content = "1. Buổi đầu có kiểm tra đầu vào không? Gia sư giỏi luôn dành 15-20 phút đánh giá trình độ thay vì dạy ngay.\n\n2. Có hỏi mục tiêu cụ thể? \"Con muốn 8 điểm thi học kỳ\" khác hoàn toàn \"con muốn giỏi Toán\".\n\n3. Giải thích có nhiều cách không? Khi học sinh không hiểu, gia sư giỏi đổi cách tiếp cận thay vì lặp lại to hơn.\n\n4. Có giao bài về nhà vừa sức? Bài quá khó gây nản, quá dễ gây chán.\n\n5. Có phản hồi cho phụ huynh sau buổi học? Vài dòng nhắn về tiến độ là dấu hiệu của sự chuyên nghiệp.\n\n6. Học sinh có mong đến buổi tiếp theo không? Đây là chỉ báo trung thực nhất.",
                    ImageUrl = "https://picsum.photos/seed/parent-tutor/800/420", Views = 156 },
                new() { Title = "Lập trình cho học sinh cấp 3: bắt đầu từ đâu?",
                    Summary = "Con bạn thích máy tính và muốn thử lập trình? Đây là lộ trình phù hợp lứa tuổi, không cần cài đặt phức tạp.",
                    Content = "Cấp 3 là thời điểm vàng để bắt đầu lập trình — đủ tư duy logic và còn nhiều thời gian khám phá.\n\nBước 1 (tháng đầu): Python hoặc Scratch nâng cao. Python có cú pháp gần ngôn ngữ tự nhiên, chạy được ngay trên trình duyệt qua replit.com.\n\nBước 2 (tháng 2-4): làm dự án nhỏ theo sở thích — game đoán số, máy tính điểm trung bình, bot nhắc lịch học. Dự án cá nhân giữ lửa tốt hơn mọi giáo trình.\n\nBước 3 (tháng 5+): nếu định thi HSG Tin học thì chuyển sang C++ và thuật toán; nếu thích sản phẩm thì học web (HTML/CSS/JS).\n\nLưu ý cho phụ huynh: đừng ép học vì 'nghề hot' — hãy để con làm ra thứ con thấy vui trước đã.",
                    ImageUrl = "https://picsum.photos/seed/kid-coding/800/420", Views = 98 },
                new() { Title = "Bí quyết ghi nhớ từ vựng tiếng Nhật cho người mới học",
                    Summary = "Kanji không đáng sợ như bạn nghĩ. Phương pháp liên tưởng hình ảnh + lặp lại ngắt quãng giúp nhớ 50 từ mỗi tuần.",
                    Content = "Tiếng Nhật có ba bảng chữ, và Kanji là nỗi sợ lớn nhất của người mới. Nhưng có phương pháp.\n\n1. Học Kanji theo bộ thủ: chữ 休 (nghỉ) = người 亻 + cây 木 → người tựa gốc cây nghỉ ngơi. Câu chuyện hình ảnh giúp nhớ lâu gấp 5 lần học vẹt.\n\n2. Flashcard ngắt quãng với Anki: ôn đúng lúc sắp quên — ngày 1, 3, 7, 21.\n\n3. Học từ trong câu, không học từ đơn lẻ: thay vì nhớ 食べる (ăn), hãy nhớ 朝ごはんを食べる (ăn sáng).\n\n4. Xem anime/drama có phụ đề Nhật (không phải phụ đề Việt) sau khi đạt N5.\n\nMục tiêu thực tế: N5 trong 4 tháng với 45 phút mỗi ngày.",
                    ImageUrl = "https://picsum.photos/seed/japan-study/800/420", Views = 143 },
            };

            var authors = new List<AppUser> { admin };
            authors.AddRange(tutorUsers.Take(4));
            for (int i = 0; i < posts.Count; i++)
            {
                posts[i].AuthorId = authors[i % authors.Count].Id;
                posts[i].IsPublished = true;
                posts[i].CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(3, 90));
            }
            db.Posts.AddRange(posts);
            await db.SaveChangesAsync();

            // bình luận + lượt thích từ học viên
            var cmts = new[]
            {
                "Bài viết rất hữu ích, cảm ơn thầy/cô đã chia sẻ!",
                "Em đã áp dụng cách số 3 và thấy tiến bộ rõ rệt.",
                "Cho em hỏi tài liệu ở phần 2 tìm ở đâu được ạ?",
                "Đúng cái mình đang cần, lưu lại ngay.",
                "Phụ huynh như mình đọc xong thấy sáng ra nhiều điều.",
                "Hay quá, mong có thêm nhiều bài như thế này ạ."
            };
            foreach (var post in posts)
            {
                int nC = rng.Next(1, 4);
                for (int c = 0; c < nC && c < studentUsers.Count; c++)
                {
                    db.Comments.Add(new Comment
                    {
                        PostId = post.Id,
                        AuthorId = studentUsers[(post.Id + c) % studentUsers.Count].Id,
                        Content = cmts[rng.Next(cmts.Length)],
                        CreatedAt = post.CreatedAt.AddDays(rng.Next(1, 10))
                    });
                }
                int nL = rng.Next(3, Math.Min(9, studentUsers.Count + tutorUsers.Count));
                var likers = studentUsers.Concat(tutorUsers).OrderBy(_ => rng.Next()).Take(nL);
                foreach (var lk in likers)
                    db.PostLikes.Add(new PostLike { PostId = post.Id, UserId = lk.Id });
            }
            await db.SaveChangesAsync();
        }

        // ══════════ 4. TÀI LIỆU: TRỎ VỀ FILE PDF THẬT (TẢI ĐƯỢC) ══════════
        var realFiles = new (string File, string Title, string Desc, long Size)[]
        {
            ("de-cuong-toan-thpt.pdf", "Đề cương ôn tập Toán học THPT", "Tổng hợp 4 chuyên đề trọng tâm: hàm số, đạo hàm, tích phân, số phức kèm dạng bài điển hình.", 22730),
            ("tu-vung-ielts-band7.pdf", "500 từ vựng IELTS mục tiêu Band 7+", "Từ vựng học thuật theo chủ đề Education, Environment, Technology kèm mẹo ghi nhớ.", 22915),
            ("bai-tap-vat-ly-song.pdf", "Bài tập Vật lý: Sóng cơ và Sóng âm", "Tóm tắt công thức + bài tập giao thoa, sóng dừng có đáp án gợi ý.", 24311),
            ("lo-trinh-hoc-lap-trinh.pdf", "Lộ trình học Lập trình Web cho người mới", "Kế hoạch 16 tuần từ HTML/CSS tới dự án CRUD hoàn chỉnh, kèm lời khuyên thực tế.", 25256),
        };

        var docs = await db.Documents.ToListAsync();
        if (docs.Count > 0)
        {
            // Sửa tài liệu seed cũ (file không tồn tại) trỏ về file thật
            int i = 0;
            foreach (var d in docs.Where(d => !d.FilePath.StartsWith("http")))
            {
                var rf = realFiles[i % realFiles.Length];
                d.FilePath = $"/uploads/documents/{rf.File}";
                d.FileName = rf.File;
                d.FileType = "pdf";
                d.FileSize = rf.Size;
                i++;
            }
        }
        else if (tutorUsers.Count > 0)
        {
            for (int i = 0; i < realFiles.Length; i++)
            {
                var rf = realFiles[i];
                db.Documents.Add(new Document
                {
                    Title = rf.Title,
                    Description = rf.Desc,
                    FilePath = $"/uploads/documents/{rf.File}",
                    FileName = rf.File,
                    FileType = "pdf",
                    FileSize = rf.Size,
                    UploaderId = tutorUsers[i % tutorUsers.Count].Id,
                    IsPublic = true,
                    DownloadCount = rng.Next(5, 60),
                    CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(5, 60))
                });
            }
        }
        await db.SaveChangesAsync();

        // ══════════ 5. THANH TOÁN CHO BUỔI ĐÃ HOÀN THÀNH (biểu đồ doanh thu) ══════════
        if (!await db.Payments.AnyAsync())
        {
            var completed = await db.Bookings
                .Include(b => b.TutorProfile)
                .Where(b => b.Status == "Completed")
                .ToListAsync();
            long order = 202600001;
            foreach (var b in completed)
            {
                var hours = (decimal)(b.EndTime - b.StartTime).TotalHours;
                var amount = Math.Round(hours * (b.TutorProfile?.HourlyRate ?? 150000m), 0);
                db.Payments.Add(new Payment
                {
                    BookingId = b.Id,
                    OrderCode = order++,
                    Amount = amount,
                    Status = "Paid",
                    TransactionNo = $"VNP{order}",
                    ResponseCode = "00",
                    CreatedAt = b.StartTime.AddDays(-1),
                    PaidAt = b.StartTime.AddDays(-1).AddMinutes(rng.Next(5, 120))
                });
                b.IsPaid = true;
            }
            await db.SaveChangesAsync();
        }

        // ══════════ 6. GÓI HỌC + LƯỢT MUA ══════════
        if (!await db.LessonPackages.AnyAsync())
        {
            var approved = await db.TutorProfiles.Where(t => t.IsApproved).OrderBy(t => t.Id).Take(4).ToListAsync();
            var pkgs = new List<LessonPackage>();
            foreach (var t in approved)
            {
                pkgs.Add(new LessonPackage { TutorProfileId = t.Id, Name = "Gói 5 buổi tiết kiệm", SessionCount = 5, Price = Math.Round(t.HourlyRate * 5 * 0.95m, 0), IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-45) });
                pkgs.Add(new LessonPackage { TutorProfileId = t.Id, Name = "Gói 10 buổi ưu đãi", SessionCount = 10, Price = Math.Round(t.HourlyRate * 10 * 0.88m, 0), IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-45) });
            }
            db.LessonPackages.AddRange(pkgs);
            await db.SaveChangesAsync();

            if (studentUsers.Count >= 2 && pkgs.Count >= 3)
            {
                long po = 202699001;
                var buy = new[] { (pkgs[0], studentUsers[0], 3), (pkgs[2], studentUsers[1], 7), (pkgs[1], studentUsers[1 % studentUsers.Count], 0) };
                foreach (var (pkg, stu, remaining) in buy)
                {
                    db.PackagePurchases.Add(new PackagePurchase
                    {
                        LessonPackageId = pkg.Id,
                        StudentId = stu.Id,
                        TutorProfileId = pkg.TutorProfileId,
                        TotalSessions = pkg.SessionCount,
                        RemainingSessions = remaining,
                        PricePaid = pkg.Price,
                        OrderCode = po++,
                        Status = remaining > 0 ? "Active" : "Used",
                        CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(10, 40)),
                        ActivatedAt = DateTime.UtcNow.AddDays(-rng.Next(5, 10))
                    });
                }
                await db.SaveChangesAsync();
            }
        }

        // ══════════ 7. MÃ GIẢM GIÁ ══════════
        if (!await db.Coupons.AnyAsync())
        {
            db.Coupons.AddRange(
                new Coupon { Code = "WELCOME10", DiscountType = "Percent", DiscountValue = 10, MaxDiscount = 50000, MinOrder = 0, UsageLimit = 0, UsedCount = 14, IsActive = true },
                new Coupon { Code = "GIAM50K", DiscountType = "Amount", DiscountValue = 50000, MinOrder = 300000, UsageLimit = 100, UsedCount = 27, IsActive = true },
                new Coupon { Code = "HE2026", DiscountType = "Percent", DiscountValue = 15, MaxDiscount = 100000, MinOrder = 200000, ExpiresAt = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc), UsageLimit = 200, UsedCount = 8, IsActive = true }
            );
            await db.SaveChangesAsync();
        }

        // ══════════ 8. GHI CHÚ BUỔI HỌC + TÓM TẮT AI MẪU ══════════
        if (!await db.LessonNotes.AnyAsync())
        {
            var done = await db.Bookings
                .Include(b => b.TutorProfile)
                .Include(b => b.Subject)
                .Where(b => b.Status == "Completed")
                .OrderByDescending(b => b.EndTime)
                .Take(5)
                .ToListAsync();
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

        Console.WriteLine("DemoSeed: đã làm giàu dữ liệu demo (avatar, vị trí, blog, tài liệu, thanh toán, gói, coupon, ghi chú).");
    }
}
