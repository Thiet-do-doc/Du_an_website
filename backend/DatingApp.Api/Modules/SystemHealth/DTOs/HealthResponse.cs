namespace DatingApp.Api.Modules.SystemHealth.DTOs;

public sealed record HealthResponse(string Status, string Message, DateTimeOffset CheckedAt);
