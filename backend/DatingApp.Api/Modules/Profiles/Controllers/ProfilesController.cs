using DatingApp.Api.Modules.Auth.Services;
using DatingApp.Api.Modules.Profiles.Contracts;
using DatingApp.Api.Modules.Profiles.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ImageMagick;

namespace DatingApp.Api.Modules.Profiles.Controllers;

[ApiController, Route("api/v1")]
public sealed class ProfilesController(ProfileService profiles, PhotoService photos, ICurrentUser currentUser) : ControllerBase
{
    [Authorize(Policy = "User"), HttpGet("profiles/me")]
    public async Task<IActionResult> Mine(CancellationToken ct) => Ok(await profiles.GetMineAsync(currentUser.UserId!.Value, ct));

    [AllowAnonymous, HttpGet("profiles/{id:guid}")]
    public async Task<IActionResult> Public(Guid id, CancellationToken ct)
    {
        var profile = await profiles.GetPublicAsync(id, ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [Authorize(Policy = "User"), HttpPut("profiles/me")]
    public async Task<IActionResult> Update(UpdateProfileRequest request, CancellationToken ct)
    {
        try { return Ok(await profiles.UpdateAsync(currentUser.UserId!.Value, request, ct)); }
        catch (ProfileValidationException e) { ModelState.AddModelError("profile", e.Message); return ValidationProblem(ModelState); }
        catch (ProfileUnavailableException) { return Conflict(new ProblemDetails { Status = 409, Title = "Tài khoản chưa thể tạo hồ sơ." }); }
    }

    [AllowAnonymous, HttpGet("interests")]
    public async Task<IActionResult> Interests(CancellationToken ct) => Ok(await profiles.GetInterestsAsync(ct));

    [Authorize(Policy = "User"), HttpPost("photos"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null) return BadRequest(new ProblemDetails { Status = 400, Title = "Chọn ảnh cần tải lên." });
        try
        {
            await using var stream = file.OpenReadStream();
            var photo = await photos.UploadAsync(currentUser.UserId!.Value, stream, file.ContentType, ct);
            return Created($"/api/v1/photos/{photo.Id}/content", new PhotoResponse(photo.Id,
                $"/api/v1/photos/{photo.Id}/content", photo.IsAvatar, photo.SortOrder));
        }
        catch (PhotoValidationException e) { ModelState.AddModelError("file", e.Message); return ValidationProblem(ModelState); }
        catch (ProfileUnavailableException) { return Conflict(new ProblemDetails { Status = 409, Title = "Hãy lưu hồ sơ trước khi tải ảnh." }); }
        catch (MagickException) { ModelState.AddModelError("file", "Tệp không phải ảnh JPEG, PNG hoặc WebP hợp lệ."); return ValidationProblem(ModelState); }
    }

    [Authorize(Policy = "User"), HttpPut("photos/{id:guid}/avatar")]
    public async Task<IActionResult> SetAvatar(Guid id, CancellationToken ct) =>
        await photos.SetAvatarAsync(currentUser.UserId!.Value, id, ct) ? NoContent() : NotFound();

    [Authorize(Policy = "User"), HttpPut("photos/order")]
    public async Task<IActionResult> Reorder(ReorderPhotosRequest request, CancellationToken ct) =>
        await photos.ReorderAsync(currentUser.UserId!.Value, request.PhotoIds, ct) ? NoContent() : BadRequest();

    [Authorize(Policy = "User"), HttpDelete("photos/{id:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid id, CancellationToken ct) =>
        await photos.DeleteAsync(currentUser.UserId!.Value, id, ct) ? NoContent() : NotFound();

    [AllowAnonymous, HttpGet("photos/{id:guid}/content")]
    public async Task<IActionResult> PhotoContent(Guid id, CancellationToken ct)
    {
        var photo = await photos.GetContentAsync(id, ct);
        return photo is null ? NotFound() : File(photo.Content, photo.ContentType);
    }
}
