using Microsoft.AspNetCore.Http;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Theming;

namespace BlazingOrchard;

public sealed class LegacyFrameThemeSelector(IHttpContextAccessor httpContextAccessor) : IThemeSelector
{
    public const string QueryParameter = "legacy-frame";
    public const string ThemeId = "BlazingOrchard.LegacyFrame";

    public Task<ThemeSelectorResult?> GetThemeAsync()
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null || !AdminAttribute.IsApplied(context) || !IsLegacyFrameRequest(context))
        {
            return Task.FromResult<ThemeSelectorResult?>(null);
        }

        return Task.FromResult<ThemeSelectorResult?>(new ThemeSelectorResult
        {
            Priority = 1000,
            ThemeName = ThemeId,
        });
    }

    public static bool IsLegacyFrameRequest(HttpContext context) =>
        string.Equals(context.Request.Query[QueryParameter], "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(context.Request.Query[QueryParameter], "true", StringComparison.OrdinalIgnoreCase);
}
