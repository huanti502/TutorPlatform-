using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TutorPlatform.Core.Models;
using TutorPlatform.Infrastructure.Data;
using TutorPlatform.Web.Data;
using TutorPlatform.Web.Hubs;
using TutorPlatform.Web.Services;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// CẤU HÌNH DATABASE POSTGRESQL CHO RENDER
// ======================================================
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrWhiteSpace(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');

    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";

    var npgsqlConn =
        $"Host={uri.Host};" +
        $"Port={uri.Port};" +
        $"Database={uri.AbsolutePath.TrimStart('/')};" +
        $"Username={username};" +
        $"Password={password};" +
        $"SSL Mode=Require;" +
        $"Trust Server Certificate=true";

    builder.Configuration["ConnectionStrings:DefaultConnection"] = npgsqlConn;
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Chưa cấu hình ConnectionStrings:DefaultConnection hoặc DATABASE_URL. " +
        "Hãy kiểm tra appsettings.json hoặc Environment Variables trên Render.");
}

// ======================================================
// ĐĂNG KÝ DBCONTEXT
// ======================================================
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString)
        .ConfigureWarnings(w =>
        {
            w.Ignore(RelationalEventId.PendingModelChangesWarning);
        });
});

// ======================================================
// IDENTITY
// ======================================================
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// ======================================================
// ĐĂNG NHẬP BẰNG GOOGLE (OAuth)
// AddIdentity đã đặt DefaultSignInScheme = ExternalScheme,
// nên Google handler tự đăng nhập vào external cookie đúng chuẩn.
// ClientId/ClientSecret lấy từ cấu hình (User Secrets / Env / appsettings).
// ======================================================
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
    });

// ======================================================
// MVC + WEB API + SIGNALR
// ======================================================
builder.Services.AddControllersWithViews();
builder.Services.AddControllers();          // <-- THÊM: bật Web API ([ApiController] + route api/...)
builder.Services.AddSignalR();

// ======================================================
// SERVICES
// ======================================================
builder.Services.AddScoped<BadgeService>();
builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<XpService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<TutorPlatform.Web.Services.NotificationService>();
builder.Services.AddHostedService<TutorPlatform.Web.Services.BookingReminderService>();

// Lưu khoá DataProtection vào PostgreSQL để không bị mất mỗi lần deploy (Render Free ổ đĩa ephemeral).
// Khắc phục lỗi antiforgery "key not found in the key ring".
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .SetApplicationName("TutorPlatform");

builder.Services.AddHttpClient();
builder.Services.AddSingleton<CloudinaryService>();
builder.Services.AddScoped<TutorPlatform.Web.Services.EmailSender>();

var app = builder.Build();

// ======================================================
// PROXY HEADERS (Render/Heroku... đứng sau reverse proxy HTTPS)
// Phải đặt SỚM NHẤT để app nhận đúng scheme https, nếu không
// đăng nhập Google sẽ lỗi "Correlation failed" (cookie sai scheme).
// ======================================================
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

// ======================================================
// MIGRATION + SEED DATABASE
// ======================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Đang kiểm tra và migrate database...");

        var db = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync();

        logger.LogInformation("Database migration hoàn tất.");

        // Thêm cột ReminderSent cho Bookings TRƯỚC khi seed (seed có truy vấn Bookings).
        // Dùng raw SQL vì dotnet-ef đang lỗi với Npgsql 10.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Bookings\" ADD COLUMN IF NOT EXISTS \"ReminderSent\" boolean NOT NULL DEFAULT false;");

        // Bảng lưu khoá DataProtection (cố định khoá qua các lần deploy).
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "DataProtectionKeys" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "FriendlyName" text NULL,
                "Xml" text NULL,
                CONSTRAINT "PK_DataProtectionKeys" PRIMARY KEY ("Id")
            );
            """);

        // Cột referral cho AspNetUsers — PHẢI thêm TRƯỚC seed vì seed có truy vấn AspNetUsers.
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"AspNetUsers\" ADD COLUMN IF NOT EXISTS \"ReferralCode\" text NULL;");
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"AspNetUsers\" ADD COLUMN IF NOT EXISTS \"HasBeenReferred\" boolean NOT NULL DEFAULT false;");

        await SeedData.SeedAllAsync(db, userManager, roleManager);

        await db.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('\"Subjects\"', 'Id'), (SELECT COALESCE(MAX(\"Id\"), 1) FROM \"Subjects\"));");

        // Tạo bảng Favorites nếu chưa có.
        // (Dùng SQL thay cho migration vì bộ công cụ dotnet-ef 10.0.8/10.0.9
        //  đang bị lỗi NullReference khi diff model với provider Npgsql.)
        // Tách từng câu lệnh riêng để tránh lỗi "multiple commands" của Npgsql.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Favorites" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "StudentId" text NOT NULL,
                "TutorProfileId" integer NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_Favorites" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Favorites_AspNetUsers_StudentId" FOREIGN KEY ("StudentId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_Favorites_TutorProfiles_TutorProfileId" FOREIGN KEY ("TutorProfileId") REFERENCES "TutorProfiles" ("Id") ON DELETE CASCADE
            );
            """);

        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Favorites_StudentId_TutorProfileId\" ON \"Favorites\" (\"StudentId\", \"TutorProfileId\");");

        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Favorites_TutorProfileId\" ON \"Favorites\" (\"TutorProfileId\");");

        logger.LogInformation("SeedData hoàn tất.");

        foreach (var role in new[] { "Admin", "Student", "Tutor" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var createRoleResult = await roleManager.CreateAsync(new IdentityRole(role));

                if (!createRoleResult.Succeeded)
                {
                    var errors = string.Join(", ", createRoleResult.Errors.Select(e => e.Description));
                    throw new Exception($"Không thể tạo role {role}: {errors}");
                }
            }
        }

        const string adminEmail = "admin@tutorplatform.com";

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);

        if (existingAdmin == null)
        {
            var admin = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Administrator",
                Role = "Admin",
                EmailConfirmed = true
            };

            var createAdminResult = await userManager.CreateAsync(admin, "Admin@123456");

            if (!createAdminResult.Succeeded)
            {
                var errors = string.Join(", ", createAdminResult.Errors.Select(e => e.Description));
                throw new Exception($"Không thể tạo tài khoản Admin mặc định: {errors}");
            }

            var addRoleResult = await userManager.AddToRoleAsync(admin, "Admin");

            if (!addRoleResult.Succeeded)
            {
                var errors = string.Join(", ", addRoleResult.Errors.Select(e => e.Description));
                throw new Exception($"Không thể gán role Admin cho tài khoản Admin mặc định: {errors}");
            }

            logger.LogInformation("Đã tạo tài khoản Admin mặc định.");
        }
        else
        {
            if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
            {
                var addRoleResult = await userManager.AddToRoleAsync(existingAdmin, "Admin");

                if (!addRoleResult.Succeeded)
                {
                    var errors = string.Join(", ", addRoleResult.Errors.Select(e => e.Description));
                    throw new Exception($"Không thể gán role Admin cho tài khoản Admin đã tồn tại: {errors}");
                }
            }

            logger.LogInformation("Tài khoản Admin mặc định đã tồn tại.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Lỗi khi migrate hoặc seed database: {Message}", ex.GetBaseException().Message);

        // Không nuốt lỗi nữa.
        // Nếu migration lỗi, Render Logs sẽ hiện lỗi thật.
        throw;
    }
}

// ======================================================
// Tạo bảng Payments (độc lập — không bị bỏ qua nếu migrate/seed lỗi)
// ======================================================
using (var paymentScope = app.Services.CreateScope())
{
    var pLogger = paymentScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var pDb = paymentScope.ServiceProvider.GetRequiredService<AppDbContext>();
        pLogger.LogInformation("Đang đảm bảo bảng Payments tồn tại...");

        await pDb.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Payments" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "BookingId" integer NOT NULL,
                "OrderCode" bigint NOT NULL,
                "Amount" numeric(18,2) NOT NULL,
                "Status" text NOT NULL DEFAULT 'Pending',
                "TransactionNo" text NULL,
                "ResponseCode" text NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "PaidAt" timestamp with time zone NULL,
                CONSTRAINT "PK_Payments" PRIMARY KEY ("Id")
            );
            """);

        await pDb.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Payments_OrderCode\" ON \"Payments\" (\"OrderCode\");");

        await pDb.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Payments_BookingId\" ON \"Payments\" (\"BookingId\");");

        await pDb.Database.ExecuteSqlRawAsync(
            "SELECT setval(pg_get_serial_sequence('\"Payments\"', 'Id'), (SELECT COALESCE(MAX(\"Id\"), 1) FROM \"Payments\"));");

        // Cột coupon cho Payments
        await pDb.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Payments\" ADD COLUMN IF NOT EXISTS \"CouponCode\" text NULL;");
        await pDb.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Payments\" ADD COLUMN IF NOT EXISTS \"DiscountAmount\" numeric(18,2) NOT NULL DEFAULT 0;");

        // Bảng Coupons (mã giảm giá)
        await pDb.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Coupons" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "Code" text NOT NULL,
                "DiscountType" text NOT NULL DEFAULT 'Amount',
                "DiscountValue" numeric(18,2) NOT NULL DEFAULT 0,
                "MaxDiscount" numeric(18,2) NULL,
                "MinOrder" numeric(18,2) NOT NULL DEFAULT 0,
                "ExpiresAt" timestamp with time zone NULL,
                "UsageLimit" integer NOT NULL DEFAULT 0,
                "UsedCount" integer NOT NULL DEFAULT 0,
                "IsActive" boolean NOT NULL DEFAULT true,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT "PK_Coupons" PRIMARY KEY ("Id")
            );
            """);
        await pDb.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Coupons_Code\" ON \"Coupons\" (\"Code\");");

        // Bảng Complaints (khiếu nại / tố cáo)
        await pDb.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Complaints" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "ReporterId" text NOT NULL,
                "TargetType" text NOT NULL DEFAULT 'Tutor',
                "TargetId" text NOT NULL,
                "TargetName" text NULL,
                "Reason" text NOT NULL,
                "Description" text NULL,
                "Status" text NOT NULL DEFAULT 'Pending',
                "AdminNote" text NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "HandledAt" timestamp with time zone NULL,
                CONSTRAINT "PK_Complaints" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Complaints_AspNetUsers_ReporterId" FOREIGN KEY ("ReporterId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
            );
            """);
        await pDb.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Complaints_Status\" ON \"Complaints\" (\"Status\");");

        // Bảng Assignments (bài tập)
        await pDb.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Assignments" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "BookingId" integer NOT NULL,
                "TutorId" text NOT NULL,
                "StudentId" text NOT NULL,
                "Title" text NOT NULL,
                "Description" text NULL,
                "DueDate" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "SubmissionText" text NULL,
                "SubmissionFileUrl" text NULL,
                "SubmittedAt" timestamp with time zone NULL,
                "Grade" text NULL,
                "Feedback" text NULL,
                "GradedAt" timestamp with time zone NULL,
                "Status" text NOT NULL DEFAULT 'Assigned',
                CONSTRAINT "PK_Assignments" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Assignments_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE CASCADE
            );
            """);
        await pDb.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Assignments_Student\" ON \"Assignments\" (\"StudentId\");");
        await pDb.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Assignments_Tutor\" ON \"Assignments\" (\"TutorId\");");

        // Bảng LessonNotes (ghi chú buổi học + AI tóm tắt)
        await pDb.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "LessonNotes" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "BookingId" integer NOT NULL,
                "TutorId" text NOT NULL,
                "StudentId" text NOT NULL,
                "Content" text NULL,
                "AiSummary" text NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_LessonNotes" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_LessonNotes_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE CASCADE
            );
            """);
        await pDb.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_LessonNotes_BookingId\" ON \"LessonNotes\" (\"BookingId\");");

        // Bảng LessonPackages (gói nhiều buổi do gia sư tạo)
        await pDb.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "LessonPackages" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TutorProfileId" integer NOT NULL,
                "Name" text NOT NULL,
                "SessionCount" integer NOT NULL,
                "Price" numeric(18,2) NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT true,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT "PK_LessonPackages" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_LessonPackages_TutorProfiles_TutorProfileId" FOREIGN KEY ("TutorProfileId") REFERENCES "TutorProfiles" ("Id") ON DELETE CASCADE
            );
            """);

        // Bảng PackagePurchases (lượt mua gói của học viên)
        await pDb.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PackagePurchases" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "LessonPackageId" integer NOT NULL,
                "StudentId" text NOT NULL,
                "TutorProfileId" integer NOT NULL,
                "TotalSessions" integer NOT NULL,
                "RemainingSessions" integer NOT NULL,
                "PricePaid" numeric(18,2) NOT NULL,
                "OrderCode" bigint NOT NULL,
                "Status" text NOT NULL DEFAULT 'Pending',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                "ActivatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_PackagePurchases" PRIMARY KEY ("Id")
            );
            """);
        await pDb.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_PackagePurchases_Student\" ON \"PackagePurchases\" (\"StudentId\");");

        // Bảng Referrals (cột AspNetUsers đã thêm ở scope trước, trước seed)
        await pDb.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Referrals" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "ReferrerId" text NOT NULL,
                "RefereeId" text NOT NULL,
                "ReferrerCouponCode" text NOT NULL,
                "RefereeCouponCode" text NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT "PK_Referrals" PRIMARY KEY ("Id")
            );
            """);

        // Bảng AuditLogs (nhật ký hoạt động admin)
        await pDb.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AuditLogs" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "ActorId" text NOT NULL,
                "ActorName" text NOT NULL,
                "Action" text NOT NULL,
                "TargetType" text NULL,
                "TargetName" text NULL,
                "Details" text NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
            );
            """);
        await pDb.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_AuditLogs_CreatedAt\" ON \"AuditLogs\" (\"CreatedAt\");");

        pLogger.LogInformation("Bảng Payments đã sẵn sàng.");
    }
    catch (Exception ex)
    {
        pLogger.LogError(ex, "Lỗi khi tạo bảng Payments: {Message}", ex.GetBaseException().Message);
    }
}

// ======================================================
// PIPELINE
// ======================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ======================================================
// HUBS
// ======================================================
app.MapHub<ChatHub>("/chatHub");
app.MapHub<WhiteboardHub>("/whiteboardHub");
app.MapHub<BattleHub>("/battleHub");

// ======================================================
// ROUTES
// ======================================================
app.MapControllers();   // <-- THÊM: map các API controller dùng attribute routing (api/...)

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();