using Microsoft.EntityFrameworkCore;
using TCTVocabulary.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình dịch vụ (Phải nằm TRƯỚC builder.Build)
builder.Services.AddControllersWithViews();

// Đăng ký kết nối Database
builder.Services.AddDbContext<DbflashcardContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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

app.UseAuthorization();

// 3. Cấu hình trang chủ mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");

app.Run();