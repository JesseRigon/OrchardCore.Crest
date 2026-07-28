using System.Text.RegularExpressions;
using Microsoft.JSInterop;
using Crest.Admin.Api;

namespace Crest.Admin.Theme;

public sealed partial class CrestThemeEngine(IJSRuntime js)
{
    private static readonly IReadOnlyDictionary<string, string> SemanticTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["primary"] = "--crest-color-accent-1",
        ["secondary"] = "--crest-color-accent-2",
        ["info"] = "--crest-color-accent-3",
        ["onPrimary"] = "--crest-color-on-accent-1",
        ["surface"] = "--crest-color-surface-1",
        ["background"] = "--crest-color-surface-2",
        ["primaryNavMenuSurface"] = "--crest-color-surface-3",
        ["text"] = "--crest-color-text-1",
        ["titleText"] = "--crest-color-text-2",
        ["mutedText"] = "--crest-color-text-muted",
        ["border"] = "--crest-color-border-1",
        ["radius"] = "--crest-radius-sm",
    };

    private static readonly HashSet<string> AllowedCrestVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "--crest-color-accent-1",
        "--crest-color-accent-2",
        "--crest-color-accent-3",
        "--crest-color-on-accent-1",
        "--crest-color-surface-1",
        "--crest-color-surface-2",
        "--crest-color-surface-3",
        "--crest-color-text-1",
        "--crest-color-text-2",
        "--crest-color-text-muted",
        "--crest-color-border-1",
        "--crest-color-shadow-1",
        "--crest-color-hover-surface-1",
        "--crest-color-hover-text-1",
        "--crest-color-active-surface-1",
        "--crest-color-button-hover-surface-1",
        "--crest-border-size-xs",
        "--crest-border-size-sm",
        "--crest-border-size-md",
        "--crest-radius-xs",
        "--crest-radius-sm",
        "--crest-radius-md",
        "--crest-radius-lg",
        "--crest-radius-pill",
        "--crest-space-2xs",
        "--crest-space-xs",
        "--crest-space-sm",
        "--crest-space-md",
        "--crest-space-lg",
        "--crest-space-xl",
        "--crest-space-2xl",
        "--crest-font-size-xs",
        "--crest-font-size-sm",
        "--crest-font-size-md",
        "--crest-font-size-lg",
        "--crest-font-size-xl",
        "--crest-shadow-sm",
        "--crest-shadow-md",
        "--crest-shadow-lg",
    };

    public async Task ApplyAsync(CrestThemeSettings settings)
    {
        var variables = Translate(settings.Tokens);
        await js.InvokeVoidAsync("crestTheme.apply", settings.RadzenTheme, variables);
    }

    private static Dictionary<string, string> Translate(IReadOnlyDictionary<string, string> tokens)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in tokens)
        {
            var variable = SemanticTokens.TryGetValue(key, out var semanticVariable)
                ? semanticVariable
                : key;

            if (!AllowedCrestVariables.Contains(variable) || !IsSafeCssValue(value))
            {
                continue;
            }

            variables[variable] = value;
        }

        return variables;
    }

    private static bool IsSafeCssValue(string value) =>
        value.Length <= 64 && SafeCssValueRegex().IsMatch(value);

    [GeneratedRegex(@"^#[0-9a-fA-F]{3,8}$|^[a-zA-Z][a-zA-Z0-9-]*$|^-?(\d+|\d*\.\d+)(px|rem|em|%)$|^rgba?\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*(,\s*(0|1|0?\.\d+))?\s*\)$")]
    private static partial Regex SafeCssValueRegex();
}
