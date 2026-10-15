using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.SystemHealth.Services;
using DatingApp.Api.Modules.DiscoveryMatching.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// The skeleton starts without PostgreSQL so the team can set up one layer at a time.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
    builder.Services.AddScoped<IMatchService, MatchService>();
    builder.Services.AddScoped<DiscoveryService>();
    builder.Services.AddSingleton(TimeProvider.System);
}
// Fail closed until the owning modules register real adapters. TV5's block lookup
// is required before discovery or likes can run; TV4's bridge is required to commit a match.
builder.Services.AddScoped<IDiscoveryProfileQuery, UnconfiguredDiscoveryProfileQuery>();
builder.Services.AddScoped<IBlockLookup, UnconfiguredBlockLookup>();
builder.Services.AddScoped<IMatchConversationBridge, UnconfiguredMatchConversationBridge>();
builder.Services.AddScoped<IDatabaseProbe, PostgresDatabaseProbe>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
// Local traffic goes through Vite's proxy; no wildcard CORS policy is required.
// Discovery endpoints derive UserId only from the authenticated ClaimsPrincipal.
// Auth must add its real authentication middleware before signed-in requests can succeed.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.MapControllers();
app.Run();

// Entry point exposed for WebApplicationFactory integration tests.
public partial class Program { }
