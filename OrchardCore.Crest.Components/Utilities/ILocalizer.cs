using System.Globalization;

namespace Crest.Components.Primitives;

/// <summary>
/// Provides custom translations for Crest components. Implement this interface and register it
/// in the DI container to override the built-in English strings.
/// </summary>
public interface ILocalizer
{
    /// <summary>
    /// Returns a localized string for the specified key and culture, or <c>null</c> to fall back to the built-in default.
    /// </summary>
    /// <param name="key">The resource key. Use <c>nameof(CrestStrings.SomeKey)</c> for compile-time safety.</param>
    /// <param name="culture">The culture to localize for.</param>
    /// <returns>The localized string, or <c>null</c> to use the built-in default.</returns>
    string? Get(string key, CultureInfo culture);
}
