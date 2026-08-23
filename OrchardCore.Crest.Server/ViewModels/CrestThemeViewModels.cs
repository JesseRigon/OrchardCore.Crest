using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Settings;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Crest.ViewModels;

public sealed record CrestThemeSettings(string RadzenTheme, Dictionary<string, string> Tokens)
{
    public static CrestThemeSettings Default { get; } = new(
        "material-base",
        new Dictionary<string, string>
        {
            ["primary"] = "#2f6f4e",
            ["secondary"] = "#6d5d3f",
            ["surface"] = "#ffffff",
            ["background"] = "#f7f8f6",
            ["radius"] = "6px",
        });
}
