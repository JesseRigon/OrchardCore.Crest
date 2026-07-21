namespace Crest.Admin.Options;

public sealed class CrestRoutingOptions
{
    public string AdminPath { get; set; } = "/admin";
    public string LoginPath { get; set; } = "/login";
    public string? AdminHostPrefix { get; set; } = "admin";
}
