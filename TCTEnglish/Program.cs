using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Hubs;
using TCTVocabulary.Models;
using TCTVocabulary.Services;
using TCTVocabulary.Workers;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddRazorPages()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
// 1. Cấu hình dịch vụ (Phải nằm TRƯỚC builder.Build)
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
// Đăng ký kết nối Database
builder.Services.AddDbContext<DbflashcardContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            sqlOptions.CommandTimeout(60);
        }));

// Register email sender and background worker
builder.Services.AddSingleton<IAppEmailSender, SmtpAppEmailSender>();
builder.Services.AddScoped<IAvatarUploadService, AvatarUploadService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IYoutubeTranscriptService, YoutubeTranscriptService>();
builder.Services.AddHostedService<AutoUnlockWorker>();

// Cấu hình Authentication (Cookie + Social)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = "ExternalCookie";
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
})
.AddCookie("ExternalCookie")
.AddGoogle(options =>
{
    // Lấy từ appsettings.json hoặc User Secrets
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "dummy-client-id";
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "dummy-client-secret";

    // Request 'profile' scope so Google returns the picture field in UserInfo
    options.Scope.Add("profile");

    // Extract 'picture' from Google's UserInfo JSON and add it as a claim
    options.Events.OnCreatingTicket = ctx =>
    {
        var pictureUrl = ctx.User.GetProperty("picture").GetString();
        if (!string.IsNullOrEmpty(pictureUrl))
        {
            ctx.Identity?.AddClaim(new System.Security.Claims.Claim("picture", pictureUrl));
        }
        return System.Threading.Tasks.Task.CompletedTask;
    };
    options.Events.OnRemoteFailure = ctx =>
    {
        ctx.Response.Redirect("/Account/Login");
        ctx.HandleResponse();
        return System.Threading.Tasks.Task.CompletedTask;
    };
})
.AddFacebook(options =>
{
    options.AppId = builder.Configuration["Authentication:Facebook:AppId"] ?? "dummy-app-id";
    options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"] ?? "dummy-app-secret";
    options.Events.OnRemoteFailure = ctx =>
    {
        ctx.Response.Redirect("/Account/Login");
        ctx.HandleResponse();
        return System.Threading.Tasks.Task.CompletedTask;
    };
});

var app = builder.Build();

// 2. Cấu hình đường đi của dữ liệu (Middleware)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Thay vì MapStaticAssets, dùng UseStaticFiles để an toàn cho mọi phiên bản .NET
app.UseStaticFiles();

app.UseRouting();

// Authentication phải trước Authorization
app.UseAuthentication();
app.UseAuthorization();
app.MapHub<ClassChatHub>("/classChatHub");

// Legacy route compatibility for old admin speaking URL
app.MapControllerRoute(
    name: "admin-speaking-management-legacy",
    pattern: "Admin/SpeakingManagement/{action=Index}/{id?}",
    defaults: new { area = "Admin", controller = "SpeakingVideoManagement" });

// Area route (Admin Dashboard)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// 3. Cấu hình trang chủ mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");


// Seed từ vựng hệ thống từ file JSON (có try-catch riêng, web không sập nếu file JSON lỗi)
try
{
    await TCTVocabulary.Models.JsonVocabularySeeder.SeedFromJsonAsync(app.Services);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "JsonVocabularySeeder: Lỗi không mong đợi khi seed từ JSON.");
}

app.Run();