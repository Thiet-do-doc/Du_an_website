using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.SystemHealth.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DatingApp.Api.Modules.Auth.Services;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký controller, xử lý lỗi và tài liệu API.
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddDataProtection();

// Kết nối PostgreSQL bằng cấu hình trong User Secrets.
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Đăng ký dịch vụ quản lý tài khoản.
    builder.Services
        .AddIdentityCore<ApplicationUser>(options =>
        {
            // Không cho phép các tài khoản dùng trùng email.
            options.User.RequireUniqueEmail = true;

            // Mật khẩu tối thiểu 10 ký tự, có số,
            // chữ thường, chữ hoa và ký tự đặc biệt.
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;

            // Áp dụng khi sử dụng luồng đăng nhập SignInManager.
            options.SignIn.RequireConfirmedEmail = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();
    builder.Services.AddScoped<AuthService>();
}

// Dịch vụ kiểm tra kết nối database.
builder.Services.AddScoped<IDatabaseProbe, PostgresDatabaseProbe>();

var app = builder.Build();

// Xử lý lỗi HTTP.
app.UseExceptionHandler();
app.UseStatusCodePages();

// Chỉ cung cấp tài liệu OpenAPI trong môi trường Development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Frontend local gọi API qua Vite proxy.
// Sẽ bổ sung xác thực JWT và phân quyền trước khi mở các API cần đăng nhập.
app.MapControllers();

app.Run();

// Cho phép dự án kiểm thử sử dụng WebApplicationFactory.
public partial class Program { }