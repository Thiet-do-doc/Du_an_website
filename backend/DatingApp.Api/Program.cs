using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.SystemHealth.Services;
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
}
builder.Services.AddScoped<IDatabaseProbe, PostgresDatabaseProbe>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
// Local traffic goes through Vite's proxy; no wildcard CORS policy is required.
// There are no business endpoints yet. Add authentication + authorization BEFORE
// exposing profile, matching, admin, or chat endpoints.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.MapControllers();
app.Run();

// Entry point exposed for WebApplicationFactory integration tests.
public partial class Program { }
