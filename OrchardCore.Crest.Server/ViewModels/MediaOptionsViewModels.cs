using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Media;

namespace Crest.ViewModels;

public sealed record MediaOptionsDto(int[] SupportedSizes, IEnumerable<string> AllowedFileExtensions, int MaxBrowserCacheDays, int MaxSecureFilesBrowserCacheDays, int MaxCacheDays, long MaxFileSize, int? MaxUploadChunkSize, string CdnBaseUrl, string AssetsRequestPath, string AssetsPath, string AssetsUsersFolder, bool UseTokenizedQueryString);
