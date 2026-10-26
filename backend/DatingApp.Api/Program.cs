using DatingApp.Api.Modules.SafetyAdmin.Services;
using System.Text.Json.Serialization;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.SystemHealth.Services;
using DatingApp.Api.Modules.DiscoveryMatching.Services;
using DatingApp.Api.Modules.ChatNotifications.Services;
using DatingApp.Api.Modules.ChatNotifications.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DatingApp.Api.Modules.Auth.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using DatingApp.Api.Modules.Profiles.Services;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký controller, xử lý lỗi và tài liệu API.
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi();
builder.Services.AddDataProtection().SetApplicationName("DatingApp");
builder.Services.AddExceptionHandler<AuthEmailExceptionHandler>();
if (builder.Configuration["DataProtection:KeyPath"] is string keyPath)
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AuthConnectionRegistry>();
builder.Services.AddSignalR(options => options.AddFilter<AccountHubFilter>());
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "datingapp.csrf";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(1));

// Kết nối PostgreSQL bằng cấu hình trong User Secrets.
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection");
JwtSettings? jwtSettings = null;

if (!string.IsNullOrWhiteSpace(connectionString))
{
    jwtSettings = new JwtSettings(builder.Configuration);
    builder.Services.AddSingleton(jwtSettings);
    var frontendUrl = builder.Configuration["Auth:FrontendUrl"];
    if (!Uri.TryCreate(frontendUrl, UriKind.Absolute, out var frontendUri)
        || (frontendUri.Scheme != "https" && !(builder.Environment.IsDevelopment() && frontendUri.Scheme == "http")))
        throw new InvalidOperationException("Auth:FrontendUrl phải là URL HTTPS (HTTP chỉ dùng cho Development).");
    if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing")
        && string.IsNullOrWhiteSpace(builder.Configuration["Email:SmtpHost"]))
        throw new InvalidOperationException("Production phải cấu hình Email:SmtpHost và thông tin SMTP bằng secret.");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
    // DB-backed modules must follow the same configuration boundary as AppDbContext.
    // Health and anonymous authorization still work on a fresh checkout without secrets.
    builder.Services.AddScoped<IBlockPolicy, BlockPolicy>();
    builder.Services.AddScoped<ChatService>();
    builder.Services.AddScoped<NotificationService>();
    builder.Services.AddScoped<IChatReportTargetAccess, ChatReportTargetAccess>();
    builder.Services.AddScoped<MatchChatBridge>();
    builder.Services.AddScoped<IMatchConversationBridge, MatchChatBridge>();
    builder.Services.AddScoped<IChatMatchAccess, IntegratedChatMatchAccess>();
    builder.Services.AddScoped<IChatBlockPolicy, IntegratedChatBlockPolicy>();
    builder.Services.AddScoped<IChatPermissions, ChatPermissions>();
    builder.Services.AddScoped<ProfileService>();
    builder.Services.AddScoped<PhotoService>();
    builder.Services.AddScoped<DatingApp.Api.Modules.Profiles.Services.IDiscoveryProfileQuery>(sp => sp.GetRequiredService<ProfileService>());
    builder.Services.AddScoped<DiscoveryProfileQueryAdapter>();
    builder.Services.AddScoped<IMatchService, MatchService>();
    builder.Services.AddScoped<DiscoveryService>();
    builder.Services.AddScoped<IBlockLookup, DiscoveryBlockLookup>();
    builder.Services.AddScoped<DatingApp.Api.Modules.DiscoveryMatching.Services.IDiscoveryProfileQuery, DiscoveryProfileQueryAdapter>();

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
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<IAuthEmailSender, AuthEmailSender>();
    builder.Services.AddScoped<IAccountAccess, AccountAccess>();
}

// Dịch vụ kiểm tra kết nối database.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Cho phép chạy health khi chưa cấu hình database.
        if (jwtSettings is null) return;
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments("/hubs") && context.Request.Query.TryGetValue("access_token", out var token))
                    context.Token = token;
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var access = context.HttpContext.RequestServices.GetRequiredService<IAccountAccess>();
                if (context.Principal is null || !await access.ValidateAsync(context.Principal, context.HttpContext.RequestAborted))
                    context.Fail("Phiên đăng nhập không còn hợp lệ.");
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = jwtSettings.SigningKey,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("User", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("Admin", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));
});

builder.Services.AddScoped<IDatabaseProbe, PostgresDatabaseProbe>();

var app = builder.Build();

// Chỉ cấp Admin cho tài khoản đã xác nhận bằng lệnh local có kiểm soát.
if (builder.Configuration["grant-admin"] is string adminEmail)
{
    using var scope = app.Services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var user = await users.FindByEmailAsync(adminEmail);
    if (user is null || !user.EmailConfirmed || !user.IsActive)
        throw new InvalidOperationException("Chỉ cấp Admin cho tài khoản đang hoạt động và đã xác nhận email.");
    if (!await roles.RoleExistsAsync("Admin"))
    {
        var roleResult = await roles.CreateAsync(new IdentityRole<Guid>("Admin"));
        if (!roleResult.Succeeded) throw new InvalidOperationException("Không tạo được vai trò Admin.");
    }
    var result = await users.AddToRoleAsync(user, "Admin");
    if (!result.Succeeded && !await users.IsInRoleAsync(user, "Admin"))
        throw new InvalidOperationException("Không cấp được vai trò Admin.");
    return;
}

// Xử lý lỗi HTTP.
app.UseExceptionHandler();
app.UseStatusCodePages();

// Chỉ cung cấp tài liệu OpenAPI trong môi trường Development.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Frontend local gọi API qua Vite proxy.
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat", options => options.CloseOnAuthenticationExpiration = true);

app.Run();

// Cho phép dự án kiểm thử sử dụng WebApplicationFactory.
public partial class Program { }
