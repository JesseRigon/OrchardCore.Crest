using OrchardCore.AdminMenu;
using OrchardCore.AdminMenu.AdminNodes;
using OrchardCore.AdminMenu.Models;
using OrchardCore.AdminMenu.Services;
using OrchardCore.Localization.Data;
using OrchardCore.Navigation;

namespace Crest.Services;

/// <summary>
/// Enumerates the captions of admin menu nodes BELOW the root level into the Translations
/// editor, which upstream misses entirely.
/// </summary>
/// <remarks>
/// The stock <c>LinkAdminNodeDataLocalizationProvider</c> / <c>PlaceholderAdminNode...</c>
/// providers build their descriptors from <c>menu.MenuItems.OfType&lt;...&gt;()</c> - the root
/// level only, no recursion (see plans/upstream-orchard-proposals.md #2). Every child caption
/// is therefore invisible to the Translations editor, and - because the editor's Save replaces
/// a culture's whole list with what was enumerated (#3) - any stored translation for a child
/// caption is silently DELETED by any save of that culture. With the provider-menu import
/// seeding child captions per culture, that made every editor save destroy most of the seeds.
///
/// This provider closes both halves locally: child captions become visible (and editable) in
/// the editor, and being enumerated means their stored values round-trip through Save instead
/// of being dropped. Roots are deliberately skipped - upstream already enumerates those, and
/// yielding them twice would show duplicate rows in the editor.
///
/// Covers every admin menu, not just the imported "Primary Navigation" one: the upstream gap
/// applies equally to hand-built tenant menus.
/// </remarks>
public sealed class CrestAdminMenuChildCaptionDataLocalizationProvider(
    IAdminMenuService adminMenuService) : ILocalizationDataProvider
{
    public async Task<IEnumerable<DataLocalizedString>> GetDescriptorsAsync()
    {
        var list = await adminMenuService.GetAdminMenuListAsync();
        var descriptors = new List<DataLocalizedString>();

        foreach (var menu in list.AdminMenu)
        {
            var context = DataLocalizationContext.AdminMenu(menu.Name);

            // Pre-seeded with the ROOT captions: upstream already enumerates those, and a child
            // caption that repeats a root one (e.g. a root "Media" and a "Media" child under
            // Configuration) must not be yielded again - the editor would show a duplicate row,
            // its Save would store the entry twice, and GetStrings' ToDictionary over stored
            // entries then throws on every request, taking the whole editor down.
            var seen = menu.MenuItems
                .Select(GetCaption)
                .Where(caption => !string.IsNullOrWhiteSpace(caption))
                .Select(caption => caption!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var root in menu.MenuItems)
            {
                // Roots themselves are upstream's; only their subtrees are missing.
                CollectChildCaptions(root.Items, context, seen, descriptors);
            }
        }

        return descriptors;
    }

    private static string? GetCaption(MenuItem item) => item switch
    {
        LinkAdminNode link => link.LinkText,
        PlaceholderAdminNode placeholder => placeholder.LinkText,
        _ => null,
    };

    private static void CollectChildCaptions(
        IEnumerable<MenuItem> items,
        string context,
        HashSet<string> seen,
        List<DataLocalizedString> descriptors)
    {
        foreach (var item in items)
        {
            var caption = GetCaption(item);
            if (!string.IsNullOrWhiteSpace(caption) && seen.Add(caption))
            {
                descriptors.Add(new DataLocalizedString(context, caption, string.Empty));
            }

            CollectChildCaptions(item.Items, context, seen, descriptors);
        }
    }
}
