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

// Đọc DATABASE_URL từ Render (PostgreSQL)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var npgsqlConn = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
    builder.Configuration["ConnectionStrings:DefaultConnection"] = npgsqlConn;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

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

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// ✅ Đăng ký các dịch vụ Hệ thống & AI
builder.Services.AddScoped<BadgeService>();
builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<XpService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<TutorPlatform.Web.Services.CloudinaryService>();

var app = builder.Build();

// FIX LỖI 3: Migrate tự động + try/catch để dễ debug
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync(); // ← Chạy migration trước
        await SeedData.SeedAllAsync(db, userManager, roleManager); //................... quyét dữ liệu trong SeedData

        // ==============================================================
        // 🚀 TỰ ĐỘNG KHỞI TẠO ROLES VÀ TÀI KHOẢN ADMIN MẶC ĐỊNH
        // ==============================================================

        // Tạo các roles nếu chưa tồn tại
        foreach (var role in new[] { "Admin", "Student", "Tutor" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Tạo admin mặc định nếu chưa có
        const string adminEmail = "admin@tutorplatform.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Administrator",
                Role = "Admin",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, "Admin@123456");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ Lỗi khi migrate hoặc seed database (bao gồm khởi tạo Admin): {Message}", ex.Message);
    }
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<ChatHub>("/chatHub");
app.MapHub<WhiteboardHub>("/whiteboardHub");
app.MapHub<BattleHub>("/battleHub");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run(); // ← Ứng dụng đứng đợi ở đây để nhận Request, mọi code đặt phía dưới dòng này sẽ bị bỏ qua.
