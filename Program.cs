using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.Auth.Entities;
using DatingApp.Api.Modules.Auth.Services;
using DatingApp.Api.Modules.SystemHealth.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var authConfigured = !string.IsNullOrWhiteSpace(connectionString);
if (authConfigured)
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
    }).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.Name = "DatingApp.Auth";
        options.Cookie.HttpOnly = true;
        // Local HTTP development needs SameAsRequest; use HTTPS in deployed environments.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
    });
    builder.Services.AddScoped<IEmailSender, DevelopmentEmailSender>();
}
builder.Services.AddScoped<IDatabaseProbe, PostgresDatabaseProbe>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
if (authConfigured)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.MapControllers();
app.Run();

// Entry point exposed for WebApplicationFactory integration tests.
public partial class Program { }
