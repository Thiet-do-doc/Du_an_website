using System.Security.Cryptography;
using System.Text;
using DatingApp.Api.Demo;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.ChatNotifications.Hubs;
using DatingApp.Api.Modules.ChatNotifications.Services;
using DatingApp.Api.Modules.SystemHealth.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 8 * 1024);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
}
builder.Services.AddScoped<IDatabaseProbe, PostgresDatabaseProbe>();

builder.Services.AddScoped<IChatPermissions, DemoChatPermissions>(); // TV1/3/5 replace on merge
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<MatchChatBridge>();

// The demo creates a per-startup key ONLY in Development/Testing when none is provided.
// Production never silently falls back to a known key. TV1 owns the real JWT module.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        jwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    else throw new InvalidOperationException("Configure Jwt:Key through secure settings before production.");
}
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key must have at least 32 UTF-8 bytes.");
var issuer = builder.Configuration["Jwt:Issuer"] ?? "DatingApp";
var audience = builder.Configuration["Jwt:Audience"] ?? "DatingApp-Frontend";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = issuer,
            ValidateAudience = true, ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(20)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/hubs/chat"))
                {
                    var token = ctx.Request.Query["access_token"].ToString();
                    if (!string.IsNullOrEmpty(token)) ctx.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

if (app.Environment.IsDevelopment())
{
    // Health still runs when DB is not configured. For the demo, apply migration
    // before running; seeding deliberately does NOT attempt to create the schema.
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        using var scope = app.Services.CreateScope();
        await DemoSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
    app.MapDevEndpoints(jwtKey, issuer, audience);
}

app.Run();
public partial class Program { }
