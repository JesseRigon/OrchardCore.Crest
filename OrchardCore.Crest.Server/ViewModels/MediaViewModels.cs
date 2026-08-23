using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Security.Permissions;

namespace Crest.ViewModels;

public sealed record MediaDirectoryResult(string Path, string? ParentPath, MediaEntry[] Entries);

public sealed record MediaEntry(string Path, string Name, bool IsDirectory, long Length, DateTimeOffset LastModifiedUtc, string? PublicUrl);

public sealed record MediaFolderRequest(string? ParentPath, string? Name);
