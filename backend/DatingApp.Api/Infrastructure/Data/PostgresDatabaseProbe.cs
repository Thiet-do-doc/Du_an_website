using DatingApp.Api.Modules.SystemHealth.Services;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.Api.Infrastructure.Data;

public sealed class PostgresDatabaseProbe(IServiceProvider services) : IDatabaseProbe
{
    public async Task<DatabaseProbeResult> CheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        try
        {
            var db = services.GetService<AppDbContext>();
            if (db is null)
                return new(false, "not_configured", "Chưa cấu hình kết nối PostgreSQL.");

            var ready = await db.Database.CanConnectAsync(timeout.Token);
            return ready
                ? new(true, "connected", "Đã kết nối PostgreSQL.")
                : new(false, "unavailable", "Chưa kết nối được PostgreSQL. Kiểm tra cấu hình local.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, "unavailable", "Kết nối PostgreSQL quá thời gian chờ.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            // Do not return connection strings, credentials, or exception details.
            return new(false, "unavailable", "Không kết nối được PostgreSQL. Kiểm tra cấu hình local.");
        }
    }
}
