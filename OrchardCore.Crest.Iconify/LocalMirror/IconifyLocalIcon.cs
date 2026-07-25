namespace Crest.Iconify;

public sealed record IconifyLocalIcon(
    string Prefix,
    string Name,
    string SvgMarkup,
    string? Attribution,
    string? License);
