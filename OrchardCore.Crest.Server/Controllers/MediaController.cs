using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Security.Permissions;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/media")]
public sealed class MediaController(
    IMediaFileStore mediaFileStore,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MediaDirectoryResult>> ListAsync([FromQuery] string? path = null)
    {
        if (!await CanManageAsync()) return Forbid();

        var normalizedPath = NormalizeDirectory(path);
        if (normalizedPath is null) return BadRequest("The media path is invalid.");

        var entries = new List<MediaEntry>();
        await foreach (var entry in mediaFileStore.GetDirectoryContentAsync(normalizedPath, includeSubDirectories: false))
        {
            entries.Add(new MediaEntry(
                entry.Path,
                entry.Name,
                entry.IsDirectory,
                entry.Length,
                entry.LastModifiedUtc,
                entry.IsDirectory ? null : mediaFileStore.MapPathToPublicUrl(entry.Path)));
        }

        return Ok(new MediaDirectoryResult(normalizedPath, ParentOf(normalizedPath), entries
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray()));
    }

    [HttpPost("folders")]
    public async Task<ActionResult<MediaDirectoryResult>> CreateFolderAsync([FromBody] MediaFolderRequest request)
    {
        if (!await CanManageAsync()) return Forbid();

        var parent = NormalizeDirectory(request.ParentPath);
        var name = NormalizeSegment(request.Name);
        if (parent is null || name is null) return BadRequest("A valid media folder name is required.");

        var path = string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}";
        if (!await mediaFileStore.TryCreateDirectoryAsync(path)) return Conflict("The folder could not be created.");
        return await ListAsync(parent);
    }

    [HttpPost("files")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<ActionResult<MediaDirectoryResult>> UploadAsync([FromForm] IFormFile file, [FromForm] string? path = null, [FromForm] bool overwrite = false)
    {
        if (!await CanManageAsync()) return Forbid();
        var directory = NormalizeDirectory(path);
        var name = NormalizeSegment(file.FileName);
        if (directory is null || name is null || file.Length == 0) return BadRequest("A non-empty file with a valid name is required.");

        await mediaFileStore.TryCreateDirectoryAsync(directory);
        await using var stream = file.OpenReadStream();
        await mediaFileStore.CreateFileFromStreamAsync(string.IsNullOrEmpty(directory) ? name : $"{directory}/{name}", stream, overwrite);
        return await ListAsync(directory);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAsync([FromQuery] string? path = null)
    {
        if (!await CanManageAsync()) return Forbid();
        var normalizedPath = NormalizeFile(path);
        if (normalizedPath is null) return BadRequest("The media path is invalid.");
        return await mediaFileStore.TryDeleteFileAsync(normalizedPath) ? NoContent() : NotFound();
    }

    private Task<bool> CanManageAsync() => authorizationService.AuthorizeAsync(User, MediaPermissions.ManageMedia);

    private static string? NormalizeDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var segments = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment => NormalizeSegment(segment) is null) ? null : string.Join('/', segments);
    }

    private static string? NormalizeFile(string? value)
    {
        var directory = NormalizeDirectory(value);
        return string.IsNullOrEmpty(directory) ? null : directory;
    }

    private static string? NormalizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var segment = value.Trim();
        return segment is "." or ".." || segment.Contains('/') || segment.Contains('\\') || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? null : segment;
    }

    private static string? ParentOf(string path)
    {
        var separator = path.LastIndexOf('/');
        return separator < 0 ? (string.IsNullOrEmpty(path) ? null : string.Empty) : path[..separator];
    }
}

public sealed record MediaDirectoryResult(string Path, string? ParentPath, MediaEntry[] Entries);
public sealed record MediaEntry(string Path, string Name, bool IsDirectory, long Length, DateTimeOffset LastModifiedUtc, string? PublicUrl);
public sealed record MediaFolderRequest(string? ParentPath, string? Name);
