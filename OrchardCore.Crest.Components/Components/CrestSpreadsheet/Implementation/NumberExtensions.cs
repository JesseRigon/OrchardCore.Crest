using System.Globalization;

namespace Crest.Components.Primitives;

/// <summary>
/// Provides extension methods for numeric types to convert them to pixel values.
/// </summary>
public static class NumberExtensions
{
    /// <summary>
    /// Converts a double value to a string representation in pixels (px).
    /// </summary>
    public static string ToPx(this double value)
    {
        return $"{value.ToString(CultureInfo.InvariantCulture)}px";
    }
}