using Microsoft.AspNetCore.Identity;
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
// MVC + SIGNALR
// ======================================================
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// ======================================================
// SERVICES
// ======================================================
builder.Services.AddScoped<BadgeService>();
builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<XpService>();
builder.Services.AddScoped<ReportService>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<CloudinaryService>();

var app = builder.Build();

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

        await SeedData.SeedAllAsync(db, userManager, roleManager);

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
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();