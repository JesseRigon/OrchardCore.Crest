using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Media;
using OrchardCore.Media.Core.Processing;
using OrchardCore.Media.Models;
using OrchardCore.Media.Services;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/media/profiles")]
public sealed class CrestMediaProfilesController(MediaProfilesManager manager, IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MediaProfileDto[]>> ListAsync()
    {
        if (!await CanManageAsync()) return Forbid();
        var document = await manager.GetMediaProfilesDocumentAsync();
        return Ok(document.MediaProfiles.OrderBy(profile => profile.Key, StringComparer.OrdinalIgnoreCase).Select(profile => MediaProfileDto.From(profile.Key, profile.Value)).ToArray());
    }

    [HttpPut("{name}")]
    public async Task<ActionResult<MediaProfileDto>> SaveAsync(string name, [FromBody] MediaProfileWriteRequest request)
    {
        if (!await CanManageAsync()) return Forbid();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("A profile name is required.");
        if (request.Width < 0 || request.Height < 0 || request.Quality is < 0 or > 100) return BadRequest("Dimensions must be positive and quality must be from 0 to 100.");
        var profile = new MediaProfile { Hint = request.Hint?.Trim(), Width = request.Width, Height = request.Height, Mode = request.Mode, Format = request.Format, Quality = request.Quality, BackgroundColor = request.BackgroundColor?.Trim(), AutoOrient = request.AutoOrient };
        await manager.UpdateMediaProfileAsync(name, profile);
        return Ok(MediaProfileDto.From(name, profile));
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> DeleteAsync(string name)
    {
        if (!await CanManageAsync()) return Forbid();
        var document = await manager.GetMediaProfilesDocumentAsync();
        if (!document.MediaProfiles.ContainsKey(name)) return NotFound();
        await manager.RemoveMediaProfileAsync(name);
        return NoContent();
    }

    private Task<bool> CanManageAsync() => authorizationService.AuthorizeAsync(User, MediaPermissions.ManageMediaProfiles);
}

public sealed record MediaProfileDto(string Name, string? Hint, int Width, int Height, ResizeMode Mode, Format Format, int Quality, string? BackgroundColor, bool AutoOrient)
{
    public static MediaProfileDto From(string name, MediaProfile profile) => new(name, profile.Hint, profile.Width, profile.Height, profile.Mode, profile.Format, profile.Quality, profile.BackgroundColor, profile.AutoOrient);
}
public sealed record MediaProfileWriteRequest(string? Hint, int Width, int Height, ResizeMode Mode, Format Format, int Quality, string? BackgroundColor, bool AutoOrient);
