namespace BlazingOrchard.Admin.Options;

public sealed class BlazingRoutingOptions
{
    public string AdminPath { get; set; } = "/admin";
    public string LoginPath { get; set; } = "/login";
    public string? AdminHostPrefix { get; set; } = "admin";
}
