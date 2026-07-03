using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;

namespace TutorPlatform.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser>, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Khoá mã hoá DataProtection (lưu vào DB để cố định qua các lần deploy trên Render).
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // ── Bảng CŨ ──────────────────────────────
    public DbSet<TutorProfile> TutorProfiles => Set<TutorProfile>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<TutorSubject> TutorSubjects => Set<TutorSubject>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TutorAvailability> TutorAvailabilities => Set<TutorAvailability>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<Certificate> Certificates => Set<Certificate>();

    // ── Bảng MỚI ─────────────────────────────
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<TutorBadge> TutorBadges => Set<TutorBadge>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<LearningGoal> LearningGoals => Set<LearningGoal>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<BannedWord> BannedWords => Set<BannedWord>();
    public DbSet<BattleRoomEntity> BattleRooms => Set<BattleRoomEntity>();

    // ── Bảng thêm sau ────────────────────────
    public DbSet<ReviewReply> ReviewReplies => Set<ReviewReply>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<LessonNote> LessonNotes => Set<LessonNote>();
    public DbSet<LessonPackage> LessonPackages => Set<LessonPackage>();
    public DbSet<PackagePurchase> PackagePurchases => Set<PackagePurchase>();
    public DbSet<Referral> Referrals => Set<Referral>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ─────────────────────────────────────
        // Cấu hình TutorSubject
        // ─────────────────────────────────────
        builder.Entity<TutorSubject>()
            .HasKey(ts => new { ts.TutorProfileId, ts.SubjectId });

        builder.Entity<TutorProfile>()
            .HasOne(t => t.User)
            .WithOne(u => u.TutorProfile)
            .HasForeignKey<TutorProfile>(t => t.UserId);

        // ─────────────────────────────────────
        // Cấu hình Comment
        // ─────────────────────────────────────
        builder.Entity<Comment>()
            .HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.NoAction);

        // ─────────────────────────────────────
        // Cấu hình Booking
        // ─────────────────────────────────────
        builder.Entity<Booking>()
            .HasOne(b => b.Student)
            .WithMany(u => u.StudentBookings)
            .HasForeignKey(b => b.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Booking>()
            .HasOne(b => b.TutorProfile)
            .WithMany(t => t.Bookings)
            .HasForeignKey(b => b.TutorProfileId)
            .OnDelete(DeleteBehavior.NoAction);

        // ─────────────────────────────────────
        // Cấu hình Review
        // ─────────────────────────────────────
        builder.Entity<Review>()
            .HasOne(r => r.Student)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Review>()
            .HasOne(r => r.TutorProfile)
            .WithMany(t => t.ReceivedReviews)
            .HasForeignKey(r => r.TutorProfileId)
            .OnDelete(DeleteBehavior.NoAction);

        // ─────────────────────────────────────
        // Cấu hình PostLike
        // ─────────────────────────────────────
        builder.Entity<PostLike>()
            .HasOne(pl => pl.Post)
            .WithMany(p => p.Likes)
            .HasForeignKey(pl => pl.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PostLike>()
            .HasOne(pl => pl.User)
            .WithMany()
            .HasForeignKey(pl => pl.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<PostLike>()
            .HasIndex(pl => new { pl.PostId, pl.UserId })
            .IsUnique();

        // ─────────────────────────────────────
        // Cấu hình Certificate
        // ─────────────────────────────────────
        builder.Entity<Certificate>()
            .HasOne(c => c.TutorProfile)
            .WithMany(t => t.Certificates)
            .HasForeignKey(c => c.TutorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─────────────────────────────────────
        // Cấu hình TutorBadge
        // ─────────────────────────────────────
        builder.Entity<TutorBadge>()
            .HasOne(tb => tb.TutorProfile)
            .WithMany(t => t.TutorBadges)
            .HasForeignKey(tb => tb.TutorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TutorBadge>()
            .HasOne(tb => tb.Badge)
            .WithMany(b => b.TutorBadges)
            .HasForeignKey(tb => tb.BadgeId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─────────────────────────────────────
        // Cấu hình ReviewReply
        // ─────────────────────────────────────
        builder.Entity<ReviewReply>()
            .HasOne(r => r.Review)
            .WithOne(r => r.Reply)
            .HasForeignKey<ReviewReply>(r => r.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ReviewReply>()
            .HasOne(r => r.Author)
            .WithMany()
            .HasForeignKey(r => r.AuthorId)
            .OnDelete(DeleteBehavior.NoAction);

        // ─────────────────────────────────────
        // Cấu hình Document
        // ─────────────────────────────────────
        builder.Entity<Document>()
            .HasOne(d => d.Uploader)
            .WithMany()
            .HasForeignKey(d => d.UploaderId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Document>()
            .HasOne(d => d.Booking)
            .WithMany()
            .HasForeignKey(d => d.BookingId)
            .OnDelete(DeleteBehavior.SetNull);

        // ─────────────────────────────────────
        // Cấu hình QuizAttempt
        // ─────────────────────────────────────
        builder.Entity<QuizAttempt>()
            .HasOne(q => q.User)
            .WithMany()
            .HasForeignKey(q => q.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<QuizAttempt>()
            .HasOne(q => q.Subject)
            .WithMany()
            .HasForeignKey(q => q.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─────────────────────────────────────
        // Cấu hình BattleRoomEntity
        // ─────────────────────────────────────
        builder.Entity<BattleRoomEntity>()
            .HasIndex(b => b.RoomId)
            .IsUnique();

        builder.Entity<BattleRoomEntity>()
            .Property(b => b.RoomId)
            .HasMaxLength(20);

        builder.Entity<BattleRoomEntity>()
            .Property(b => b.SubjectName)
            .HasMaxLength(200);

        builder.Entity<BattleRoomEntity>()
            .Property(b => b.Level)
            .HasMaxLength(50);

        builder.Entity<BattleRoomEntity>()
            .Property(b => b.Player1Id)
            .HasMaxLength(450);

        builder.Entity<BattleRoomEntity>()
            .Property(b => b.Player2Id)
            .HasMaxLength(450);

        builder.Entity<BattleRoomEntity>()
            .Property(b => b.Player1Name)
            .HasMaxLength(200);

        builder.Entity<BattleRoomEntity>()
            .Property(b => b.Player2Name)
            .HasMaxLength(200);

        builder.Entity<BattleRoomEntity>()
            .Property(b => b.Player1ConnectionId)
            .HasMaxLength(200);

        builder.Entity<BattleRoomEntity>()
            .Property(b => b.Player2ConnectionId)
            .HasMaxLength(200);

        builder.Entity<BattleRoomEntity>()
            .Property(b => b.Status)
            .HasMaxLength(30);

        // ─────────────────────────────────────
        // Cấu hình Favorite (gia sư đã lưu)
        // ─────────────────────────────────────
        builder.Entity<Favorite>()
            .HasOne(f => f.Student)
            .WithMany()
            .HasForeignKey(f => f.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Favorite>()
            .HasOne(f => f.TutorProfile)
            .WithMany()
            .HasForeignKey(f => f.TutorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Mỗi học viên chỉ lưu 1 gia sư 1 lần
        builder.Entity<Favorite>()
            .HasIndex(f => new { f.StudentId, f.TutorProfileId })
            .IsUnique();

        // ─────────────────────────────────────
        // Cấu hình Payment (giao dịch thanh toán)
        // ─────────────────────────────────────
        builder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

        builder.Entity<Payment>()
            .HasOne(p => p.Booking)
            .WithMany()
            .HasForeignKey(p => p.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Payment>()
            .HasIndex(p => p.OrderCode);

        // ─────────────────────────────────────
        // Seed Subject
        // ─────────────────────────────────────
        builder.Entity<Subject>().HasData(
            new Subject { Id = 1, Name = "Toán học", Level = "THPT", IsActive = true },
            new Subject { Id = 2, Name = "Tiếng Anh", Level = "THPT", IsActive = true },
            new Subject { Id = 3, Name = "Vật lý", Level = "THPT", IsActive = true },
            new Subject { Id = 4, Name = "Hóa học", Level = "THPT", IsActive = true },
            new Subject { Id = 5, Name = "Lập trình", Level = "Đại học", IsActive = true }
        );

        // ─────────────────────────────────────
        // Seed Badge
        // ─────────────────────────────────────
        builder.Entity<Badge>().HasData(
            new Badge
            {
                Id = 1,
                Name = "Khởi đầu",
                Description = "Hoàn thành buổi học đầu tiên",
                Icon = "🌱",
                Color = "#4CAF50",
                Type = BadgeType.Sessions,
                RequiredCount = 1
            },
            new Badge
            {
                Id = 2,
                Name = "Đang lên",
                Description = "Hoàn thành 10 buổi học",
                Icon = "⚡",
                Color = "#2196F3",
                Type = BadgeType.Sessions,
                RequiredCount = 10
            },
            new Badge
            {
                Id = 3,
                Name = "Chuyên nghiệp",
                Description = "Hoàn thành 50 buổi học",
                Icon = "🎯",
                Color = "#9C27B0",
                Type = BadgeType.Sessions,
                RequiredCount = 50
            },
            new Badge
            {
                Id = 4,
                Name = "Huyền thoại",
                Description = "Hoàn thành 100 buổi học",
                Icon = "🏆",
                Color = "#FFD700",
                Type = BadgeType.Sessions,
                RequiredCount = 100
            },
            new Badge
            {
                Id = 5,
                Name = "Được yêu thích",
                Description = "Nhận được 5 lượt đánh giá",
                Icon = "⭐",
                Color = "#FF9800",
                Type = BadgeType.Reviews,
                RequiredCount = 5
            },
            new Badge
            {
                Id = 6,
                Name = "Top Rated",
                Description = "Nhận được 20 lượt đánh giá",
                Icon = "🌟",
                Color = "#FF5722",
                Type = BadgeType.Reviews,
                RequiredCount = 20
            },
            new Badge
            {
                Id = 7,
                Name = "Gia sư xuất sắc",
                Description = "Điểm TB ≥ 4.5⭐ (ít nhất 5 đánh giá)",
                Icon = "💎",
                Color = "#00BCD4",
                Type = BadgeType.Rating,
                RequiredCount = 45
            },
            new Badge
            {
                Id = 8,
                Name = "Hoàn hảo",
                Description = "Điểm TB ≥ 4.8⭐ (ít nhất 5 đánh giá)",
                Icon = "👑",
                Color = "#E91E63",
                Type = BadgeType.Rating,
                RequiredCount = 48
            },
            new Badge
            {
                Id = 9,
                Name = "Đa năng",
                Description = "Dạy từ 3 môn học trở lên",
                Icon = "📚",
                Color = "#607D8B",
                Type = BadgeType.Subjects,
                RequiredCount = 3
            },
            new Badge
            {
                Id = 10,
                Name = "Triệu phú",
                Description = "Tích lũy doanh thu 1,000,000 VNĐ",
                Icon = "💰",
                Color = "#795548",
                Type = BadgeType.Revenue,
                RequiredCount = 1000
            }
        );
    }
}