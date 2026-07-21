using System.Text.RegularExpressions;
using Microsoft.JSInterop;
using BlazingOrchard.Admin.Api;

namespace BlazingOrchard.Admin.Theme;

public sealed partial class BlazingThemeEngine(IJSRuntime js)
{
    private static readonly IReadOnlyDictionary<string, string> SemanticTokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["primary"] = "--rz-primary",
        ["primaryLight"] = "--rz-primary-light",
        ["primaryLighter"] = "--rz-primary-lighter",
        ["primaryDark"] = "--rz-primary-dark",
        ["primaryDarker"] = "--rz-primary-darker",
        ["secondary"] = "--rz-secondary",
        ["secondaryLight"] = "--rz-secondary-light",
        ["secondaryLighter"] = "--rz-secondary-lighter",
        ["secondaryDark"] = "--rz-secondary-dark",
        ["secondaryDarker"] = "--rz-secondary-darker",
        ["surface"] = "--rz-base-background-color",
        ["background"] = "--rz-body-background-color",
        ["text"] = "--rz-text-color",
        ["titleText"] = "--rz-text-title-color",
        ["radius"] = "--rz-border-radius",
    };

    private static readonly HashSet<string> AllowedRadzenVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "--rz-primary",
        "--rz-primary-light",
        "--rz-primary-lighter",
        "--rz-primary-dark",
        "--rz-primary-darker",
        "--rz-on-primary",
        "--rz-secondary",
        "--rz-secondary-light",
        "--rz-secondary-lighter",
        "--rz-secondary-dark",
        "--rz-secondary-darker",
        "--rz-on-secondary",
        "--rz-base-background-color",
        "--rz-body-background-color",
        "--rz-text-color",
        "--rz-text-title-color",
        "--rz-text-secondary-color",
        "--rz-border-radius",
        "--rz-border-radius-1",
        "--rz-border-radius-2",
        "--rz-border-radius-3",
        "--rz-border-radius-4",
    };

    public async Task ApplyAsync(BlazingThemeSettings settings)
    {
        var variables = Translate(settings.Tokens);
        await js.InvokeVoidAsync("blazingTheme.apply", settings.RadzenTheme, variables);
    }

    private static Dictionary<string, string> Translate(IReadOnlyDictionary<string, string> tokens)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in tokens)
        {
            var variable = SemanticTokens.TryGetValue(key, out var semanticVariable)
                ? semanticVariable
                : key;

            if (!AllowedRadzenVariables.Contains(variable) || !IsSafeCssValue(value))
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
