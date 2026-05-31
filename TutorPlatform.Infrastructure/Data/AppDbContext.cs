using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TutorPlatform.Core.Models;

namespace TutorPlatform.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── Bảng CŨ (giữ nguyên) ──────────────────────────────
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

    // ── Bảng MỚI ✅ ───────────────────────────────────────
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<TutorBadge> TutorBadges => Set<TutorBadge>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();

    // Thêm bảng ReviewReply
    public DbSet<ReviewReply> ReviewReplies => Set<ReviewReply>();

    // Thêm bảng Document
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Cấu hình CŨ (giữ nguyên toàn bộ)
        builder.Entity<TutorSubject>()
            .HasKey(ts => new { ts.TutorProfileId, ts.SubjectId });

        builder.Entity<TutorProfile>()
            .HasOne(t => t.User).WithOne(u => u.TutorProfile)
            .HasForeignKey<TutorProfile>(t => t.UserId);

        builder.Entity<Comment>()
            .HasOne(c => c.Author).WithMany()
            .HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Booking>()
            .HasOne(b => b.Student).WithMany(u => u.StudentBookings)
            .HasForeignKey(b => b.StudentId).OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Booking>()
            .HasOne(b => b.TutorProfile).WithMany(t => t.Bookings)
            .HasForeignKey(b => b.TutorProfileId).OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Review>()
            .HasOne(r => r.Student).WithMany(u => u.Reviews)
            .HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Review>()
            .HasOne(r => r.TutorProfile).WithMany(t => t.ReceivedReviews)
            .HasForeignKey(r => r.TutorProfileId).OnDelete(DeleteBehavior.NoAction);

        // Cấu hình PostLike (MỚI THÊM VÀO)
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
        builder.Entity<Certificate>()
    .       HasOne(c => c.TutorProfile)
    .       WithMany(t => t.Certificates)
    .       HasForeignKey(c => c.TutorProfileId)
    .       OnDelete(DeleteBehavior.Cascade);

        // Unique: 1 user chỉ like 1 bài 1 lần
        builder.Entity<PostLike>()
            .HasIndex(pl => new { pl.PostId, pl.UserId })
            .IsUnique();

        // Cấu hình MỚI ✅ - TutorBadge
        builder.Entity<TutorBadge>()
            .HasOne(tb => tb.TutorProfile).WithMany(t => t.TutorBadges)
            .HasForeignKey(tb => tb.TutorProfileId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TutorBadge>()
            .HasOne(tb => tb.Badge).WithMany(b => b.TutorBadges)
            .HasForeignKey(tb => tb.BadgeId).OnDelete(DeleteBehavior.Cascade);

        // Cấu hình MỚI ✅ - ReviewReply
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

        // Cấu hình MỚI ✅ - Document
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

        // Seed dữ liệu CŨ (giữ nguyên)
        builder.Entity<Subject>().HasData(
            new Subject { Id = 1, Name = "Toán học", Level = "THPT", IsActive = true },
            new Subject { Id = 2, Name = "Tiếng Anh", Level = "THPT", IsActive = true },
            new Subject { Id = 3, Name = "Vật lý", Level = "THPT", IsActive = true },
            new Subject { Id = 4, Name = "Hóa học", Level = "THPT", IsActive = true },
            new Subject { Id = 5, Name = "Lập trình", Level = "Đại học", IsActive = true }
        );

        // Seed Badge MỚI ✅ — 10 huy hiệu mặc định
        builder.Entity<Badge>().HasData(
            new Badge { Id = 1, Name = "Khởi đầu", Description = "Hoàn thành buổi học đầu tiên", Icon = "🌱", Color = "#4CAF50", Type = BadgeType.Sessions, RequiredCount = 1 },
            new Badge { Id = 2, Name = "Đang lên", Description = "Hoàn thành 10 buổi học", Icon = "⚡", Color = "#2196F3", Type = BadgeType.Sessions, RequiredCount = 10 },
            new Badge { Id = 3, Name = "Chuyên nghiệp", Description = "Hoàn thành 50 buổi học", Icon = "🎯", Color = "#9C27B0", Type = BadgeType.Sessions, RequiredCount = 50 },
            new Badge { Id = 4, Name = "Huyền thoại", Description = "Hoàn thành 100 buổi học", Icon = "🏆", Color = "#FFD700", Type = BadgeType.Sessions, RequiredCount = 100 },
            new Badge { Id = 5, Name = "Được yêu thích", Description = "Nhận được 5 lượt đánh giá", Icon = "⭐", Color = "#FF9800", Type = BadgeType.Reviews, RequiredCount = 5 },
            new Badge { Id = 6, Name = "Top Rated", Description = "Nhận được 20 lượt đánh giá", Icon = "🌟", Color = "#FF5722", Type = BadgeType.Reviews, RequiredCount = 20 },
            new Badge { Id = 7, Name = "Gia sư xuất sắc", Description = "Điểm TB ≥ 4.5⭐ (ít nhất 5 đánh giá)", Icon = "💎", Color = "#00BCD4", Type = BadgeType.Rating, RequiredCount = 45 },
            new Badge { Id = 8, Name = "Hoàn hảo", Description = "Điểm TB ≥ 4.8⭐ (ít nhất 5 đánh giá)", Icon = "👑", Color = "#E91E63", Type = BadgeType.Rating, RequiredCount = 48 },
            new Badge { Id = 9, Name = "Đa năng", Description = "Dạy từ 3 môn học trở lên", Icon = "📚", Color = "#607D8B", Type = BadgeType.Subjects, RequiredCount = 3 },
            new Badge { Id = 10, Name = "Triệu phú", Description = "Tích lũy doanh thu 1,000,000 VNĐ", Icon = "💰", Color = "#795548", Type = BadgeType.Revenue, RequiredCount = 1000 }
        );
        builder.Entity<QuizAttempt>()
    .HasOne(q => q.User).WithMany()
    .HasForeignKey(q => q.UserId)
    .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<QuizAttempt>()
            .HasOne(q => q.Subject).WithMany()
            .HasForeignKey(q => q.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}