using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DatingApp.Api.Infrastructure.Data;
using DatingApp.Api.Modules.ChatNotifications.Services;
using DatingApp.Api.Modules.SafetyAdmin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DatingApp.Api.Tests;
public sealed partial class AuthFlowTests
{
    [PostgresFact]
    public async Task UserReportsPersistAuthenticatedReporterAndRejectInvalidTargetsAndInput()
    {
        await using var app = new AuthApp();
        using var a = app.Browser(); using var b = app.Browser();
        var alice = await RegisterConfirmed(app, a); var bob = await RegisterConfirmed(app, b);
        var at = await Token(await a.Post("login", new { alice.Email, Password }));
        var bt = await Token(await b.Post("login", new { bob.Email, Password }));
        var start = DateTime.UtcNow;
        var payload = new { targetType = "User", targetId = bob.Id.ToString().ToUpperInvariant(),
            reason = "  Quấy rối  ", description = "  Nội dung báo cáo kiểm thử  ", reporterUserId = bob.Id, status = "Actioned" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.WriteApi(HttpMethod.Post, "reports", payload, "")).StatusCode);
        var response = await a.WriteApi(HttpMethod.Post, $"reports?reporterUserId={bob.Id}", payload, at);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = report.GetProperty("id").GetGuid();
        Assert.Equal(alice.Id.ToString(), report.GetProperty("reporterUserId").GetString());
        Assert.Equal(bob.Id.ToString(), report.GetProperty("targetId").GetString());
        Assert.Equal("Pending", report.GetProperty("status").GetString());
        Assert.Equal("Quấy rối", report.GetProperty("reason").GetString());
        Assert.Equal("Nội dung báo cáo kiểm thử", report.GetProperty("description").GetString());
        Assert.InRange(report.GetProperty("createdAtUtc").GetDateTime(), start, DateTime.UtcNow);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.GetApi($"admin/reports/{id}", bt)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.GetApi("admin/reports", at)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.WriteApi(HttpMethod.Patch, $"admin/reports/{id}",
            new { status = "Dismissed", notes = "Không được tự xử lý" }, bt)).StatusCode);
        foreach (var (type, target, reason, description, expected) in new[] {
            ("User", alice.Id.ToString().ToUpperInvariant(), "Test", "Không thể tự báo cáo", HttpStatusCode.BadRequest),
            ("User", Guid.NewGuid().ToString(), "Test", "Người dùng không tồn tại", HttpStatusCode.NotFound),
            ("User", "invalid-id", "Test", "ID đối tượng không hợp lệ", HttpStatusCode.BadRequest),
            ("User", bob.Id.ToString(), "   ", "Mô tả vẫn đủ dài", HttpStatusCode.BadRequest),
            ("User", bob.Id.ToString(), "Test", "                      ", HttpStatusCode.BadRequest),
            ("User", bob.Id.ToString(), "Test", "ngắn", HttpStatusCode.BadRequest),
            ("User", bob.Id.ToString(), "Test", new string('x', 2001), HttpStatusCode.BadRequest),
            ("999", bob.Id.ToString(), "Test", "Loại đối tượng sai", HttpStatusCode.BadRequest),
            ("Post", Guid.NewGuid().ToString(), "Test", "Bài viết ngoài phạm vi", HttpStatusCode.NotImplemented)
        })
            Assert.Equal(expected, (await a.WriteApi(HttpMethod.Post, "reports", new
                { targetType = type, targetId = target, reason, description }, at)).StatusCode);
        using var scope = app.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Reports
            .Where(x => x.ReporterUserId == alice.Id.ToString()).ToListAsync();
        var saved = Assert.Single(stored);
        Assert.Equal(id, saved.Id); Assert.Equal(ReportStatus.Pending, saved.Status);
        Assert.Equal(bob.Id.ToString(), saved.TargetUserId); Assert.Null(saved.ReviewedByUserId);
    }

    [PostgresFact]
    public async Task MessageReportsRequireRecipientAndRemainAvailableAfterBlockAndUnmatch()
    {
        await using var app = new AuthApp();
        using var a = app.Browser(); using var b = app.Browser(); using var c = app.Browser();
        var alice = await RegisterConfirmed(app, a); var bob = await RegisterConfirmed(app, b);
        var outsider = await RegisterConfirmed(app, c);
        var at = await Token(await a.Post("login", new { alice.Email, Password }));
        var bt = await Token(await b.Post("login", new { bob.Email, Password }));
        var ct = await Token(await c.Post("login", new { outsider.Email, Password }));
        foreach (var (browser, token) in new[] { (a, at), (b, bt) })
            Assert.Equal(HttpStatusCode.OK, (await browser.PutApi("profiles/me", new {
                displayName = "Report test", city = "report-test", bio = "Test only", isDiscoverable = true,
                interestIds = new[] { SampleInterests[0] } }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.WriteApi(HttpMethod.Post, $"user-likes/{bob.Id}", new { }, at)).StatusCode);
        var matched = await b.WriteApi(HttpMethod.Post, $"user-likes/{alice.Id}", new { }, bt);
        Assert.Equal(HttpStatusCode.OK, matched.StatusCode);
        var match = await matched.Content.ReadFromJsonAsync<JsonElement>();
        var conversationId = match.GetProperty("conversationId").GetGuid();
        var matchId = match.GetProperty("matchId").GetGuid();
        Guid messageId;
        const string privateContent = "PRIVATE_MESSAGE_BODY_NOT_FOR_REPORT_RESPONSE";
        using (var scope = app.Services.CreateScope())
            messageId = (await scope.ServiceProvider.GetRequiredService<ChatService>()
                .SendMessageAsync(conversationId, alice.Id, privateContent, Guid.NewGuid())).Message.Id;
        object Payload(Guid id) => new { targetType = "Message", targetId = id, reason = "Quấy rối", description = "Kiểm thử quyền báo cáo", reporterUserId = bob.Id };
        var missing = await c.WriteApi(HttpMethod.Post, "reports", Payload(Guid.NewGuid()), ct);
        var forbidden = await c.WriteApi(HttpMethod.Post, "reports", Payload(messageId), ct);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        Assert.DoesNotContain(privateContent, await forbidden.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await a.WriteApi(HttpMethod.Post, "reports", Payload(messageId), at)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.WriteApi(HttpMethod.Post, $"blocks/{alice.Id}", new { }, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetApi($"conversations/{conversationId}/messages", bt)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await b.WriteApi(HttpMethod.Post, "reports", Payload(messageId), bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.WriteApi(HttpMethod.Delete, $"blocks/{alice.Id}", null, bt)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.WriteApi(HttpMethod.Delete, $"matches/{matchId}", null, at)).StatusCode);
        var accepted = await b.WriteApi(HttpMethod.Post, "reports", Payload(messageId), bt);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.DoesNotContain(privateContent, await accepted.Content.ReadAsStringAsync());
        using var check = app.Services.CreateScope();
        var rows = await check.ServiceProvider.GetRequiredService<AppDbContext>().Reports
            .Where(x => x.TargetId == messageId.ToString()).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => { Assert.Equal(bob.Id.ToString(), row.ReporterUserId);
            Assert.Equal(alice.Id.ToString(), row.TargetUserId); Assert.Equal(ReportStatus.Pending, row.Status); });
    }
}
