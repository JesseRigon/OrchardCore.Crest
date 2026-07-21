using System.Text.RegularExpressions;

namespace Crest.Icons;

public sealed partial class SvgIconSanitizer
{
    public bool IsSafeSvg(string svg) =>
        svg.Contains("<svg", StringComparison.OrdinalIgnoreCase) &&
        !UnsafeSvgPattern().IsMatch(svg);

    [GeneratedRegex("<script|on[a-z]+\\s*=|javascript:|<foreignObject", RegexOptions.IgnoreCase)]
    private static partial Regex UnsafeSvgPattern();
}
