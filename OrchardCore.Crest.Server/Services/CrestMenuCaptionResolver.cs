using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.AdminMenu.Models;
using OrchardCore.AdminMenu.Services;
using OrchardCore.DataLocalization.Services;
using OrchardCore.Localization;
using OrchardCore.Navigation;

namespace Crest.Services;

/// <summary>
/// Resolves a menu caption against the tenant translation store for the request culture, with
/// hierarchical context fallback - the resolution Crest's sidebar and app manifest use in
/// place of a bare <c>IDataLocalizer</c> lookup.
/// </summary>
/// <remarks>
/// Two defects in the strict (key, context) lookup motivate this:
///
/// <para><b>Merge drops MenuName.</b> <c>NavigationManager.Merge</c> folds a provider item and
/// its imported admin menu node into one; the higher-priority node's values win, but the
/// SURVIVING INSTANCE is whichever came first in provider registration order, and Merge's copy
/// list does not include <c>MenuName</c> (plans/upstream-orchard-proposals.md #7). An item that
/// survived as the provider's instance therefore carries the node's caption and
/// <c>Id</c> (= <c>AdminNode.UniqueId</c>) but a null <c>MenuName</c> - and a lookup scoped by
/// menu name lands in the generic context, missing the store entry that sits under the owning
/// menu's context. The resolver restores the owning menu from the surviving Id before looking
/// anything up.</para>
///
/// <para><b>No default context.</b> Contexts are strict namespaces, so a translation stored
/// under one context is invisible to every other, and upstream falls back to the invariant
/// literal even when the request culture translates the same caption elsewhere. The resolver
/// instead walks outward: the exact menu context first, then its parents by stripping
/// <c>':'</c>-separated segments (<c>Admin Menus:Primary Navigation</c> → <c>Admin Menus</c>),
/// and finally the best entry for the caption anywhere in the culture - preferring contexts
/// under the menu root over unrelated ones (so a sibling menu's translation beats a content
/// type's), then the most common value, ordinally tie-broken for determinism. The invariant
/// literal renders only when the culture holds no translation of the caption at all. An
/// explicit entry in the exact context always wins, so pinning a caption in the Translations
/// editor overrides every fallback. Each step checks the specific culture before its parents
/// (es-ES, then es), mirroring the localizer's own culture fallback.</para>
///
/// <para>This also subsumes the previous special-cased "Content Types" fallback for the New
/// branch: a content type name stored under that context is simply the best alternative for an
/// ownerless caption no menu context translates.</para>
///
/// <para>Per-request: indexes are built once per instance (scoped) from the same documents the
/// localizer reads, and resolution itself is synchronous so the recursive item serialization
/// stays flat. Both dependencies are optional - with the data localization feature absent the
/// resolver leaves captions untouched, matching the previous null-localizer behavior.</para>
/// </remarks>
public sealed class CrestMenuCaptionResolver(IServiceProvider serviceProvider)
{
    private static readonly string RootContext = OrchardCore.AdminMenu.DataLocalizationContext.AdminMenu(null);

    // Most-specific culture first (es-ES, then es): caption -> its entries in that culture.
    private List<ILookup<string, (string Context, string Value)>>? _cultureIndexes;
    private List<Dictionary<string, List<(string Context, string Value)>>>? _poIndexes;
    private Dictionary<string, string>? _menuNameByNodeId;
    // PO tier 0 provenance: node UniqueId -> the declaring provider classes' full type
    // names, recorded at sync time in Merge's value-authority order.
    private Dictionary<string, List<string>>? _sourceContextsByNodeId;
    private bool _loaded;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        var translationsManager = serviceProvider.GetService<TranslationsManager>();
        if (translationsManager is not null)
        {
            var document = await translationsManager.GetTranslationsDocumentAsync();
            var indexes = new List<ILookup<string, (string, string)>>();
            for (var culture = CultureInfo.CurrentUICulture;
                 !string.IsNullOrEmpty(culture.Name);
                 culture = culture.Parent)
            {
                if (document.Translations.TryGetValue(culture.Name, out var entries))
                {
                    indexes.Add(entries
                        .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
                        .ToLookup(
                            entry => entry.Key,
                            entry => (entry.Context, entry.Value),
                            StringComparer.OrdinalIgnoreCase));
                }
            }

            _cultureIndexes = indexes;
        }

        var adminMenuService = serviceProvider.GetService<IAdminMenuService>();
        if (adminMenuService is not null)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var menu in (await adminMenuService.GetAdminMenuListAsync()).AdminMenu)
            {
                MapNodeIds(menu.MenuItems, menu.Name, map);
            }

            _menuNameByNodeId = map;
        }

        // The PO layer of the resolution hierarchy (store edit -> PO -> invariant
        // literal). The dictionaries are parsed once per shell and cached by the
        // manager, so this is index construction over cached data, not file IO.
        var localizationManager = serviceProvider.GetService<ILocalizationManager>();
        if (localizationManager is not null)
        {
            _poIndexes = CrestPoTranslationLookup.BuildCultureChainIndexes(
                localizationManager, CultureInfo.CurrentUICulture);
        }

        // Tier 0 provenance for the PO layer, recorded by the provider-menu sync.
        var syncDocuments = serviceProvider.GetService<OrchardCore.Documents.IDocumentManager<CrestProviderMenuSyncDocument>>();
        if (syncDocuments is not null)
        {
            var syncState = await syncDocuments.GetOrCreateImmutableAsync();
            var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var entry in syncState.Entries.Values)
            {
                if (!string.IsNullOrEmpty(entry.UniqueId) && entry.SourceContexts.Count > 0)
                {
                    map.TryAdd(entry.UniqueId, entry.SourceContexts);
                }
            }

            _sourceContextsByNodeId = map;
        }
    }

    /// <param name="caption">The displayed caption - the same key upstream's admin looks up.</param>
    /// <param name="menuName">The item's owning admin menu, when the built item still has one.</param>
    /// <param name="itemId">The item's Id, used to restore the owning menu when Merge dropped
    /// <paramref name="menuName"/> - for a merged pair this is the node's UniqueId.</param>
    public string Resolve(string caption, string? menuName, string? itemId)
    {
        if (string.IsNullOrEmpty(caption))
        {
            return caption;
        }

        if (string.IsNullOrEmpty(menuName)
            && !string.IsNullOrEmpty(itemId)
            && _menuNameByNodeId is not null
            && _menuNameByNodeId.TryGetValue(itemId, out var owningMenu))
        {
            menuName = owningMenu;
        }

        if (_cultureIndexes is { Count: > 0 } indexes)
        {
            // Exact context, then its parents by stripping ':'-separated segments.
            var context = OrchardCore.AdminMenu.DataLocalizationContext.AdminMenu(
                string.IsNullOrEmpty(menuName) ? null : menuName);
            while (true)
            {
                foreach (var index in indexes)
                {
                    foreach (var (entryContext, value) in index[caption])
                    {
                        if (string.Equals(entryContext, context, StringComparison.OrdinalIgnoreCase))
                        {
                            return value;
                        }
                    }
                }

                var separator = context.LastIndexOf(':');
                if (separator < 0)
                {
                    break;
                }

                context = context[..separator];
            }

            // The culture's best alternative, from the nearest culture that has any.
            foreach (var index in indexes)
            {
                var candidates = index[caption].ToArray();
                if (candidates.Length == 0)
                {
                    continue;
                }

                var preferred = candidates
                    .Where(candidate => candidate.Context.StartsWith(RootContext, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                return (preferred.Length > 0 ? preferred : candidates)
                    .GroupBy(candidate => candidate.Value, StringComparer.Ordinal)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.Ordinal)
                    .First().Key;
            }
        }

        // The PO layer: shipped baseline below the store, so a caption no tenant entry
        // covers still renders its catalog translation, and deleting a store entry
        // reverts to the shipped value rather than the invariant literal (delete walks
        // down the hierarchy - store edit -> PO -> literal). Tier 0 is the item's own
        // recorded declarers (via its UniqueId), then *.AdminMenu contexts, then flat -
        // see CrestPoTranslationLookup for the tiering.
        if (_poIndexes is { Count: > 0 } poIndexes)
        {
            List<string>? sourceContexts = null;
            if (!string.IsNullOrEmpty(itemId))
            {
                _sourceContextsByNodeId?.TryGetValue(itemId, out sourceContexts);
            }

            var poValue = CrestPoTranslationLookup.Resolve(
                poIndexes, caption, CrestPoTranslationLookup.AdminMenuContextSuffix, sourceContexts);
            if (poValue is not null)
            {
                return poValue;
            }
        }

        return caption;
    }

    private static void MapNodeIds(IEnumerable<MenuItem> items, string menuName, Dictionary<string, string> map)
    {
        foreach (var item in items)
        {
            if (item is AdminNode node && !string.IsNullOrEmpty(node.UniqueId))
            {
                map.TryAdd(node.UniqueId, menuName);
            }

            MapNodeIds(item.Items, menuName, map);
        }
    }
}
