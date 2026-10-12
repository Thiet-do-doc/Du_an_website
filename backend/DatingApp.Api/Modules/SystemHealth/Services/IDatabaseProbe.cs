namespace DatingApp.Api.Modules.SystemHealth.Services;

public sealed record DatabaseProbeResult(bool Ready, string Status, string Message);

public interface IDatabaseProbe
{
    Task<DatabaseProbeResult> CheckAsync(CancellationToken cancellationToken);
}
