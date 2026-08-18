using OrchardCore.AdminMenu;
using OrchardCore.DataLocalization.Models;
using OrchardCore.DataLocalization.Services;

namespace Crest.Services;

/// <summary>
/// Promotes an admin menu rename from the Crest layout document into the tenant's translation
/// store, which is the same store <c>IDataLocalizer</c> reads at render time.
/// </summary>
/// <remarks>
/// A rename stored in the Crest layout only applies inside Crest's own Blazor admin: it is a
/// per-tenant display override Crest substitutes over the built menu. The tenant's translation
/// store is what Orchard's own Razor admin consults (see
/// <c>TheAdmin/Views/NavigationItemText.cshtml</c>, which resolves each caption through
/// <c>IDataLocalizer</c> keyed on the caption itself). Promoting a rename writes the same text
/// there, so both admins agree on the caption instead of Crest silently disagreeing with the
/// Razor admin for the same tenant and culture.
///
/// The key is the caption Orchard itself would look up, not the item's Crest key: the data
/// localizer is keyed on the source caption within a context, mirroring how
/// <c>IStringLocalizer</c> keys on the untranslated literal. Callers pass the pre-override
/// caption for that reason.
/// </remarks>
public sealed class CrestAdminMenuTranslationService(TranslationsManager translationsManager)
{
    /// <summary>
    /// Records <paramref name="translation"/> as the translation of <paramref name="sourceText"/>
    /// for <paramref name="culture"/>, under the admin menu context Orchard's own admin menu
    /// rendering looks up. Passing a blank <paramref name="translation"/> removes the entry
    /// rather than storing an empty caption.
    /// </summary>
    public async Task SetAsync(string culture, string menuName, string sourceText, string? translation)
    {
        ArgumentException.ThrowIfNullOrEmpty(culture);
        ArgumentException.ThrowIfNullOrEmpty(sourceText);

        var context = DataLocalizationContext.AdminMenu(menuName);
        var document = await translationsManager.GetTranslationsDocumentAsync();

        // UpdateTranslationAsync replaces the whole culture's list rather than merging into it,
        // so every other translation for this culture has to be carried over here or promoting
        // one menu rename would wipe the tenant's other data translations.
        var existing = document.Translations.TryGetValue(culture, out var current)
            ? current.ToList()
            : [];

        existing.RemoveAll(entry =>
            string.Equals(entry.Context, context, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.Key, sourceText, StringComparison.Ordinal));

        if (!string.IsNullOrWhiteSpace(translation))
        {
            existing.Add(new Translation
            {
                Context = context,
                Key = sourceText,
                Value = translation.Trim(),
            });
        }

        await translationsManager.UpdateTranslationAsync(culture, existing);
    }
}
