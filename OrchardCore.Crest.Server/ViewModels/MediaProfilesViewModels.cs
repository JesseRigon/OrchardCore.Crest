using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Media;
using OrchardCore.Media.Core.Processing;
using OrchardCore.Media.Models;
using OrchardCore.Media.Services;

namespace Crest.ViewModels;

public sealed record MediaProfileDto(string Name, string? Hint, int Width, int Height, ResizeMode Mode, Format Format, int Quality, string? BackgroundColor, bool AutoOrient)
{
    public static MediaProfileDto From(string name, MediaProfile profile) => new(name, profile.Hint, profile.Width, profile.Height, profile.Mode, profile.Format, profile.Quality, profile.BackgroundColor, profile.AutoOrient);
}

public sealed record MediaProfileWriteRequest(string? Hint, int Width, int Height, ResizeMode Mode, Format Format, int Quality, string? BackgroundColor, bool AutoOrient);
