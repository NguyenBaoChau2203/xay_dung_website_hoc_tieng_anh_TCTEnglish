using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
// 1. Cấu hình dịch vụ (Phải nằm TRƯỚC builder.Build)
builder.Services.AddControllersWithViews();
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
// Đăng ký kết nối Database
builder.Services.AddDbContext<DbflashcardContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cấu hình Authentication (Cookie + Social)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    // Nếu muốn mặc định khi challenge là Google thì set DefaultChallengeScheme, 
    // nhưng ở đây ta dùng link explicit nên để Cookie là default scheme.
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})

.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
})
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
})
.AddFacebook(options =>
{
    options.AppId = builder.Configuration["Authentication:Facebook:AppId"] ?? "dummy-app-id";
    options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"] ?? "dummy-app-secret";
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

// 3. Cấu hình trang chủ mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");

app.Run();