using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using OrchardCore.AdminMenu;
using OrchardCore.AdminMenu.AdminNodes;
using OrchardCore.AdminMenu.Models;
using OrchardCore.AdminMenu.Services;
using OrchardCore.Data.Documents;
using OrchardCore.DataLocalization.Models;
using OrchardCore.DataLocalization.Services;
using OrchardCore.Documents;
using OrchardCore.Localization;
using OrchardCore.Navigation;

namespace Crest.Services;

/// <summary>
/// Materializes the menu items contributed by <see cref="INavigationProvider"/> implementations
/// into the DB-backed admin menu system, so Crest's sidebar and menu editor only ever deal with
/// admin menu nodes.
/// </summary>
/// <remarks>
/// Provider items have no stable per-tenant identity of their own: many providers never call
/// <c>.Id(...)</c> (20 of 57 upstream admin menu providers do not), and the ones that do use
/// hand-written slugs that are only unique by convention. Nothing about them can carry a
/// tenant's icon overrides, ordering or renames reliably, and nothing about them can be
/// translated at runtime - their captions come from PO files, which are a deploy-time artifact.
///
/// Importing them as admin menu nodes fixes both: each becomes an <see cref="AdminNode"/> with a
/// <c>UniqueId</c> that the node navigation builders copy onto <c>MenuItem.Id</c>, giving it a
/// stable key, and each gains a <c>MenuName</c>, which is the context
/// <c>IDataLocalizer</c> needs to hold a tenant-level translation of its caption.
///
/// <para>
/// Items are matched across runs on <c>MenuItem.Text.Name</c> - the invariant literal a
/// provider passed to <c>S["..."]</c> (the localization key itself, English only by
/// convention), which is what OrchardCore's own
/// <c>NavigationManager.Merge</c> matches on and therefore does not vary by culture - qualified
/// by the item's position in the tree so that two identically-captioned items under different
/// parents stay distinct. The match key is held in this service's own document rather than on
/// <see cref="AdminNode"/>, keeping the upstream model untouched.
/// </para>
///
/// <para>
/// Disabled features cannot be imported ahead of time. A disabled feature's services are never
/// registered in the shell container (see <c>CompositionStrategy</c>, which composes the
/// container from the shell descriptor's enabled features only), so its
/// <see cref="INavigationProvider"/> cannot be constructed, let alone executed - several build
/// their items from live tenant data (<c>ContentTypesAdminNode</c> queries content definitions,
/// <c>ListsAdminNode</c> queries the session). Instead the sync runs on every shell start, and
/// enabling or disabling a feature releases the shell, so a feature's items are imported on the
/// first request after it becomes enabled - the first moment they can be known at all.
/// </para>
///
/// <para>
/// Items that disappear (their feature was disabled or uninstalled) are marked disabled rather
/// than deleted, so that re-enabling the feature restores the tenant's customizations against
/// the same <c>UniqueId</c> instead of regenerating a new one and orphaning every override
/// stored against the old one.
/// </para>
/// </remarks>
public sealed class CrestProviderMenuSyncService(
    IEnumerable<INavigationProvider> navigationProviders,
    IAdminMenuService adminMenuService,
    IDocumentManager<CrestProviderMenuSyncDocument> documents,
    IUrlHelperFactory urlHelperFactory,
    CrestIconSourceStore iconSourceStore,
    ILocalizationService localizationService,
    ILocalizationManager localizationManager,
    TranslationsManager translationsManager,
    ILogger<CrestProviderMenuSyncService> logger)
{
    private IUrlHelper? _urlHelper;

    /// <summary>
    /// The admin menu that imported provider items are written into. Held by name because the
    /// menu is created on first sync and then referenced by its own generated id thereafter.
    /// </summary>
    public const string ImportedMenuName = "Primary Navigation";

    /// <summary>
    /// Imports the current provider-contributed menu into the admin menu system. Idempotent:
    /// re-running matches existing nodes and leaves their captions, icons, ordering and any
    /// other tenant edits alone.
    /// </summary>
    /// <param name="actionContext">Supplies the <c>IUrlHelper</c> that resolves provider route
    /// values into hrefs.</param>
    /// <param name="reseedMissingTranslations">When <c>true</c> (the on-demand endpoint), a
    /// caption/culture pair whose translation is absent from the store is re-seeded from the PO
    /// catalog even if it was seeded before - an admin invoking the sync by hand is asking for
    /// restoration. The automatic per-shell pass leaves such pairs alone: an absent entry that
    /// was seen before means someone deleted it, and refilling it would overwrite that intent.
    /// </param>
    /// <returns>A summary of what changed, for logging and for the on-demand endpoint.</returns>
    public async Task<CrestProviderMenuSyncResult> SyncAsync(ActionContext actionContext, bool reseedMissingTranslations = false)
    {
        // Built from the navigation providers only. Once the import exists, the admin menu
        // coordinator contributes the imported nodes to the same "admin" menu, so reading the
        // merged tree here would re-import this service's own output on every pass.
        var built = await BuildProviderOnlyMenuAsync(actionContext);
        var state = await documents.GetOrCreateMutableAsync();
        var list = await adminMenuService.LoadAdminMenuListAsync();

        var menu = list.AdminMenu.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, ImportedMenuName, StringComparison.Ordinal));

        var created = false;
        if (menu is null)
        {
            menu = new OrchardCore.AdminMenu.Models.AdminMenu { Name = ImportedMenuName };
            list.AdminMenu.Add(menu);
            created = true;
        }

        var result = new CrestProviderMenuSyncResult { MenuCreated = created };

        // Every match key seen in this pass, so anything left over can be marked disabled below.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Every invariant caption imported in this pass, for the PO seeding below.
        var captions = new HashSet<string>(StringComparer.Ordinal);

        // The root level is the menu's own AdminNode list; every level below it is a node's
        // MenuItem list. Bridged with a temporary view here, then written back, so SyncLevel
        // itself only has to deal with one collection type.
        var rootItems = menu.MenuItems.Cast<MenuItem>().ToList();
        await SyncLevelAsync(built.ToList(), rootItems, parentKey: null, state, seen, captions, result);

        menu.MenuItems.Clear();
        menu.MenuItems.AddRange(rootItems.OfType<AdminNode>());

        // Nodes whose provider no longer contributes them: the feature was disabled or removed.
        // Kept, but disabled, so their UniqueId - and therefore every override, icon and rename
        // stored against it - survives the feature being re-enabled later.
        foreach (var (key, entry) in state.Entries)
        {
            if (seen.Contains(key) || !entry.Enabled)
            {
                continue;
            }

            var node = menu.GetMenuItemById(entry.UniqueId);
            if (node is not null && node.Enabled)
            {
                node.Enabled = false;
                result.Disabled++;
            }

            entry.Enabled = false;
        }

        // Runs on every pass, not only when the menu changed: adding a supported culture is
        // invisible to the menu diff above, yet is exactly when that culture's captions need
        // seeding. With the seen-pair tracking a pass that finds nothing new costs set lookups
        // only - no PO catalogs are loaded at all.
        var (seeded, seedStateChanged) = await SeedCaptionTranslationsAsync(captions, state, reseedMissingTranslations);
        result.SeededTranslations = seeded;

        if (result.HasChanges)
        {
            await adminMenuService.SaveAsync(menu);
        }

        if (result.HasChanges || seedStateChanged)
        {
            await documents.UpdateAsync(state);
        }

        return result;
    }

    /// <summary>
    /// Seeds the tenant translation store with the PO catalog's translation of each imported
    /// caption, for every supported culture - only where the store has no entry yet, so a
    /// tenant's own translations (edited or promoted) are never overwritten.
    /// </summary>
    /// <remarks>
    /// Importing a provider item moves its caption out of PO's reach: the rendered item carries
    /// the node's raw literal and resolves through <c>IDataLocalizer</c> (the tenant store), not
    /// through the provider's <c>S["..."]</c> resource. Without seeding, a tenant whose PO files
    /// translate "Content" to "Contenido" would render the invariant literal (the raw
    /// <c>S["..."]</c> key, English only by convention) until someone re-entered that
    /// translation by hand - the import is expected to carry the provider's translations along
    /// with its items.
    ///
    /// PO entries are keyed (context, message id) where the context is the contributing class's
    /// full name - unknowable here, since merging folds multiple contributors into one item. The
    /// lookup therefore scans the culture's whole catalog for records matching the caption
    /// regardless of context, walking the culture chain (es-ES, then es) the way the PO
    /// localizer itself falls back, and takes the most common translation when contexts
    /// disagree (ties broken ordinally, for determinism).
    ///
    /// Because seeding never overwrites, a later PO update only reaches cultures/captions that
    /// were never seeded - a seeded value is tenant data from the moment it is written, editable
    /// in the Translations editor and indistinguishable from a hand-entered one.
    ///
    /// <para>
    /// Every caption/culture pair this pass seeds - or finds already translated - is recorded in
    /// the sync document as seen. The automatic pass seeds only never-seen pairs, so a
    /// translation an admin deliberately deleted stays deleted across shell restarts instead of
    /// being refilled from PO; passing <c>force</c> (the on-demand endpoint) seeds any missing
    /// pair regardless, which is also what restores seeds lost to the Translations editor's
    /// wholesale save. The tracking doubles as the fast path: once every pair is seen, the pass
    /// loads no PO catalogs at all.
    /// </para>
    /// </remarks>
    private async Task<(int Seeded, bool StateChanged)> SeedCaptionTranslationsAsync(
        IReadOnlyCollection<string> captions,
        CrestProviderMenuSyncDocument state,
        bool force)
    {
        if (captions.Count == 0)
        {
            return (0, false);
        }

        var context = DataLocalizationContext.AdminMenu(ImportedMenuName);
        var cultures = await localizationService.GetSupportedCulturesAsync();
        var document = await translationsManager.GetTranslationsDocumentAsync();
        var seeded = 0;
        var stateChanged = false;

        foreach (var culture in cultures)
        {
            if (!state.SeededCaptions.TryGetValue(culture, out var seenList))
            {
                state.SeededCaptions[culture] = seenList = [];
            }

            var seen = seenList.ToHashSet(StringComparer.Ordinal);

            var existing = document.Translations.TryGetValue(culture, out var current)
                ? current.ToList()
                : [];

            var present = existing
                .Where(entry => string.Equals(entry.Context, context, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal);

            // A caption that already has a translation is the tenant's, however it got there -
            // marking it seen means its later deletion is respected exactly like a seeded one's.
            foreach (var caption in captions)
            {
                if (present.Contains(caption) && seen.Add(caption))
                {
                    seenList.Add(caption);
                    stateChanged = true;
                }
            }

            var wanted = captions
                .Where(caption => !present.Contains(caption) && (force || !seen.Contains(caption)))
                .ToHashSet(StringComparer.Ordinal);
            if (wanted.Count == 0)
            {
                continue;
            }

            var added = false;
            foreach (var (caption, translation) in ResolvePoTranslations(culture, wanted))
            {
                existing.Add(new Translation
                {
                    Context = context,
                    Key = caption,
                    Value = translation,
                });
                added = true;
                seeded++;

                if (seen.Add(caption))
                {
                    seenList.Add(caption);
                    stateChanged = true;
                }
            }

            if (added)
            {
                // Replaces the whole culture list, so the untouched entries were carried over
                // above - same contract CrestAdminMenuTranslationService documents.
                await translationsManager.UpdateTranslationAsync(culture, existing);
            }
        }

        if (seeded > 0)
        {
            logger.LogInformation("Seeded {Count} admin menu caption translations from the PO catalogs.", seeded);
        }

        return (seeded, stateChanged);
    }

    private List<(string Caption, string Translation)> ResolvePoTranslations(string cultureName, HashSet<string> wanted)
    {
        var results = new List<(string, string)>();

        // Specific culture first (es-ES), then its parents (es): a caption resolved at a more
        // specific level is removed from the wanted set so a parent catalog cannot override it.
        for (var culture = CultureInfo.GetCultureInfo(cultureName);
             wanted.Count > 0 && !string.IsNullOrEmpty(culture.Name);
             culture = culture.Parent)
        {
            var dictionary = localizationManager.GetDictionary(culture);
            var candidates = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

            // Translations directly, not the dictionary's own enumerator: that enumerator
            // rebuilds each record with the composite "context|messageid" string as the message
            // id (CultureDictionaryRecordKey's implicit string conversion), which would never
            // match a bare caption.
            foreach (var (key, translations) in dictionary.Translations)
            {
                var messageId = key.MessageId;
                if (!wanted.Contains(messageId))
                {
                    continue;
                }

                var value = translations is { Length: > 0 } ? translations[0] : null;
                if (string.IsNullOrWhiteSpace(value) || string.Equals(value, messageId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!candidates.TryGetValue(messageId, out var counts))
                {
                    candidates[messageId] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
                }

                counts[value] = counts.TryGetValue(value, out var count) ? count + 1 : 1;
            }

            foreach (var (caption, counts) in candidates)
            {
                var best = counts
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .First().Key;
                results.Add((caption, best));
                wanted.Remove(caption);
            }
        }

        return results;
    }

    /// <summary>
    /// Builds the "admin" menu from the navigation providers alone, excluding the admin menu
    /// coordinator that contributes DB-backed nodes.
    /// </summary>
    /// <remarks>
    /// This mirrors <c>NavigationManager.BuildMenuAsync</c> minus two of its stages, each for a
    /// reason. Authorization is skipped because the import wants the complete provider tree
    /// regardless of who is currently signed in - filtering by the importing admin's permissions
    /// would bake one user's view into the tenant's menu (the permissions themselves are copied
    /// onto the nodes instead, so the rendered menu is still filtered per user). Reduction is
    /// skipped because an item with no Href of its own may only exist to group children.
    ///
    /// Merge is still applied, because providers legitimately contribute into each other's
    /// branches (several modules each add to "Configuration"), and importing the unmerged list
    /// would create a separate root per contributor. Href computation is still applied because
    /// most providers declare their target as MVC route values, and the node needs the resolved
    /// URL - resolved by Orchard's own <c>IUrlHelper</c>, never assembled by hand.
    /// </remarks>
    private async Task<List<MenuItem>> BuildProviderOnlyMenuAsync(ActionContext actionContext)
    {
        var builder = new NavigationBuilder();

        foreach (var provider in navigationProviders)
        {
            try
            {
                await provider.BuildNavigationAsync("admin", builder);
            }
            catch (Exception e)
            {
                // Matches NavigationManager's own behaviour: one broken provider must not stop
                // the rest of the menu being imported.
                logger.LogError(e, "An exception occurred while building the admin menu for import.");
            }
        }

        var items = builder.Build();
        PruneAdminMenuNodeItems(items);
        MergeByCaption(items);
        ComputeHrefs(items, actionContext);
        return items;
    }

    /// <summary>
    /// Removes every item contributed from a DB-backed admin menu node, leaving only genuine
    /// provider items.
    /// </summary>
    /// <remarks>
    /// The DB-backed nodes reach the "admin" menu through <c>OrchardCore.AdminMenu</c>'s own
    /// <c>AdminMenu</c> navigation provider, which contributes its static items ("Tools" →
    /// "Admin Menus") and then internally invokes <c>AdminMenuNavigationProvidersCoordinator</c>
    /// for every admin menu document - including this service's own imported menu. The
    /// coordinator itself is never registered as an <c>INavigationProvider</c>, so it cannot be
    /// excluded at the provider level without also losing the wrapper's own legitimate items.
    /// What CAN distinguish the two is <c>MenuItem.MenuName</c>: only the admin node navigation
    /// builders set it (to the owning menu's name); genuine provider items never carry one.
    ///
    /// Skipping this pruning is not a fidelity nit - it ratchets. An imported node re-enters the
    /// build carrying its stored priority, merge hands that priority to the "provider" item, and
    /// the priority self-heal in <see cref="SyncLevelAsync"/> then raises the node above what is
    /// actually itself - one more on every sync, forever (observed live: priorities in the
    /// tenant document had climbed to 27 while no provider passes more than 1).
    /// </remarks>
    private static void PruneAdminMenuNodeItems(List<MenuItem> items)
    {
        items.RemoveAll(item => !string.IsNullOrEmpty(item.MenuName));

        foreach (var item in items)
        {
            PruneAdminMenuNodeItems(item.Items);
        }
    }

    // The same resolution NavigationManager.GetUrl performs (that method is private): route
    // values resolve through IUrlHelper against Orchard's registered routes, absolute and
    // app-relative urls pass through on the tenant's PathBase.
    private void ComputeHrefs(List<MenuItem> items, ActionContext actionContext)
    {
        foreach (var item in items)
        {
            if (item.RouteValues?.Count > 0)
            {
                _urlHelper ??= urlHelperFactory.GetUrlHelper(actionContext);
                item.Href = _urlHelper.RouteUrl(new UrlRouteContext { Values = item.RouteValues });
            }
            else if (!string.IsNullOrEmpty(item.Url))
            {
                if (item.Url[0] == '/' || item.Url.Contains("://"))
                {
                    item.Href = item.Url;
                }
                else
                {
                    var url = item.Url.StartsWith("~/", StringComparison.Ordinal) ? item.Url[2..] : item.Url;
                    item.Href = actionContext.HttpContext.Request.PathBase.Add($"/{url}").Value;
                }
            }

            ComputeHrefs(item.Items, actionContext);
        }
    }

    /// <summary>
    /// Folds sibling items sharing an invariant caption into one, the way
    /// <c>NavigationManager.Merge</c> does, so that providers contributing into a shared branch
    /// produce a single imported node rather than one per contributor.
    /// </summary>
    /// <remarks>
    /// Mirrors upstream <c>Merge</c>'s authority rule: the highest-priority contributor's
    /// values describe the merged item. Getting the priority right matters beyond fidelity -
    /// several providers pass <c>priority: 1</c> on their roots (Users' "Access Control",
    /// Settings, Contents, Themes, Tenants), and the imported node is created at the merged
    /// priority plus one so that it beats every contributor at render time. Capturing the first
    /// contributor's priority instead of the highest produced a node that only *tied* the
    /// highest-priority provider, and a tie does not transfer the node's UniqueId onto the
    /// rendered item.
    /// </remarks>
    private static void MergeByCaption(List<MenuItem> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var source = items[i];
            for (var j = items.Count - 1; j > i; j--)
            {
                var candidate = items[j];
                // OrdinalIgnoreCase to match upstream Merge exactly - two captions it would
                // fold together must become one imported node, not two.
                if (!string.Equals(source.Text?.Name, candidate.Text?.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                source.Items.AddRange(candidate.Items);

                if (candidate.Priority > source.Priority)
                {
                    // The more authoritative contributor describes the merged item, exactly as
                    // upstream Merge would resolve these two at render time.
                    source.Priority = candidate.Priority;
                    source.Position = candidate.Position;
                    source.Href = candidate.Href;
                    source.Url = candidate.Url;
                    source.Text = candidate.Text;

                    source.Permissions.Clear();
                    source.Permissions.AddRange(candidate.Permissions);

                    source.Classes.Clear();
                    foreach (var value in candidate.Classes)
                    {
                        source.Classes.Add(value);
                    }
                }
                else
                {
                    // The merged-away sibling may still carry the branch's only href or icon.
                    source.Href ??= candidate.Href;
                    source.Url ??= candidate.Url;
                    if (source.Classes.Count == 0)
                    {
                        foreach (var value in candidate.Classes)
                        {
                            source.Classes.Add(value);
                        }
                    }
                }

                items.RemoveAt(j);
            }

            MergeByCaption(source.Items);
        }
    }

    // targetNodes is the live child collection of the node being synced (List<MenuItem>, whose
    // members are AdminNode), so appends here actually persist.
    private async Task SyncLevelAsync(
        List<MenuItem> sourceItems,
        List<MenuItem> targetNodes,
        string? parentKey,
        CrestProviderMenuSyncDocument state,
        HashSet<string> seen,
        HashSet<string> captions,
        CrestProviderMenuSyncResult result)
    {
        foreach (var source in sourceItems)
        {
            // The "New" branch stays provider-owned: its children are regenerated live from the
            // creatable content types, Crest's layout logic locks it by exactly this key, and a
            // DB snapshot of it would both freeze a dynamic branch and replace the key the lock
            // matches on with a UniqueId.
            if (string.Equals(source.Id, CrestAdminMenuLayoutService.LockedNewItemKey, StringComparison.Ordinal))
            {
                continue;
            }

            // Text.Name is the invariant literal (the key); Text.Value is this request's translation.
            // Matching on the former is what keeps the import stable when the admin's culture
            // changes between two syncs.
            var matchKey = BuildMatchKey(parentKey, source);
            if (matchKey is null)
            {
                continue;
            }

            seen.Add(matchKey);
            captions.Add(source.Text!.Name);

            AdminNode? node = null;
            if (state.Entries.TryGetValue(matchKey, out var entry))
            {
                node = FindNode(targetNodes, entry.UniqueId);

                // Re-enable a node whose feature came back. Its UniqueId is unchanged, so every
                // override stored against it applies again without any further work.
                if (node is not null && !node.Enabled && !entry.Enabled)
                {
                    node.Enabled = true;
                    result.Reenabled++;
                }

                // Keep the node strictly more authoritative than its provider counterpart. A
                // provider can raise its priority between releases, and Merge only transfers
                // this node's UniqueId onto the rendered item while the node's priority is
                // strictly higher - a tie leaves the provider's slug as the item's Id, which
                // orphans every override stored against the UniqueId.
                if (node is not null && node.Priority <= source.Priority)
                {
                    node.Priority = source.Priority + 1;
                    result.Updated++;
                }

                // Follow the provider when ITS target moved: a module can change its route
                // between releases, and the node's URL is a snapshot resolved at import time.
                // Only applies while the node still carries the last-imported URL - once an
                // admin has edited it, the admin's value wins and provider drift is ignored.
                var resolvedUrl = !string.IsNullOrWhiteSpace(source.Href) ? source.Href : source.Url;
                if (node is LinkAdminNode linkNode
                    && !string.IsNullOrWhiteSpace(resolvedUrl)
                    && !string.Equals(resolvedUrl, entry.Url, StringComparison.Ordinal)
                    && string.Equals(linkNode.LinkUrl, entry.Url, StringComparison.Ordinal))
                {
                    linkNode.LinkUrl = resolvedUrl;
                    entry.Url = resolvedUrl;
                    result.Updated++;
                }

                // Backfill a default icon onto nodes imported before icons were resolved at
                // import time, without ever touching a node whose icon was set or changed since.
                if (node is not null && string.IsNullOrWhiteSpace(GetNodeIconClass(node)))
                {
                    var backfillIcon = ExtractIconClass(source)
                        ?? await iconSourceStore.ResolveNavigationItemIconClassAsync(source.Id, [.. source.Classes]);
                    if (!string.IsNullOrWhiteSpace(backfillIcon))
                    {
                        SetNodeIconClass(node, backfillIcon);
                        result.Updated++;
                    }
                }

                entry.Enabled = true;
            }

            // The default icon is resolved NOW, from the provider item's original slug Id and
            // classes, and persisted on the node. After the import wins the render-time merge
            // those lookup keys no longer exist on the rendered item (its Id is the UniqueId and
            // its classes are the node's), so an icon left to be resolved at render time would
            // simply be gone. Baked onto the node it becomes tenant data the editor can override.
            var iconClass = ExtractIconClass(source)
                ?? await iconSourceStore.ResolveNavigationItemIconClassAsync(source.Id, [.. source.Classes]);

            if (node is null)
            {
                node = CreateNode(source, iconClass);
                targetNodes.Add(node);
                state.Entries[matchKey] = new CrestProviderMenuSyncEntry
                {
                    UniqueId = node.UniqueId,
                    Enabled = true,
                    Url = (node as LinkAdminNode)?.LinkUrl,
                };
                result.Added++;
            }

            // node.Items itself, never a filtered copy: SyncLevel appends newly imported
            // children, and appending to a projection would silently discard them.
            await SyncLevelAsync(source.Items, node.Items, matchKey, state, seen, captions, result);
        }
    }

    /// <summary>
    /// The identity an imported item is matched by across syncs: its invariant caption,
    /// qualified by its parent's key so two items captioned the same under different parents do
    /// not collide. Returns <c>null</c> for an item with no usable literal, which is left
    /// un-imported rather than given an unstable key.
    /// </summary>
    private static string? BuildMatchKey(string? parentKey, MenuItem item)
    {
        var literal = item.Text?.Name;
        if (string.IsNullOrWhiteSpace(literal))
        {
            return null;
        }

        return parentKey is null ? literal : $"{parentKey}{literal}";
    }

    private static AdminNode CreateNode(MenuItem source, string? iconClass)
    {
        // An item with somewhere to go becomes a link; one that only groups children becomes a
        // placeholder, mirroring how the admin UI itself distinguishes the two.
        var href = !string.IsNullOrWhiteSpace(source.Href) ? source.Href : source.Url;

        // One above the provider item it was imported from. NavigationManager.Merge folds the
        // provider item and this node into a single item by caption, and its authority rule
        // gives the higher-priority side's values - including Id - to the survivor. This is what
        // makes the rendered item carry the node's UniqueId rather than the provider's slug,
        // regardless of which of the two happened to be built first.
        var priority = source.Priority + 1;

        // Merge's authority rule replaces the survivor's whole permission list with the
        // higher-priority side's, so the node must carry the provider's permissions or the
        // merged item would end up with none and become visible to every user.
        var permissionNames = source.Permissions.Select(permission => permission.Name).ToArray();

        if (string.IsNullOrWhiteSpace(href))
        {
            return new PlaceholderAdminNode
            {
                LinkText = source.Text?.Name ?? string.Empty,
                IconClass = iconClass,
                MenuName = ImportedMenuName,
                Position = source.Position,
                Priority = priority,
                PermissionNames = permissionNames,
            };
        }

        return new LinkAdminNode
        {
            LinkText = source.Text?.Name ?? string.Empty,
            LinkUrl = href,
            IconClass = iconClass,
            MenuName = ImportedMenuName,
            Position = source.Position,
            Priority = priority,
            PermissionNames = permissionNames,
        };
    }

    // Icon classes travel on the built item with the "icon-class-" prefix the shape templates
    // strip back off; AdminNode stores them unprefixed.
    private static string? ExtractIconClass(MenuItem item)
    {
        const string prefix = "icon-class-";
        var classes = item.Classes?
            .Where(value => value.StartsWith(prefix, StringComparison.Ordinal))
            .Select(value => value[prefix.Length..])
            .ToArray();

        return classes is { Length: > 0 } ? string.Join(' ', classes) : null;
    }

    private static string? GetNodeIconClass(AdminNode node) => node switch
    {
        LinkAdminNode link => link.IconClass,
        PlaceholderAdminNode placeholder => placeholder.IconClass,
        _ => null,
    };

    private static void SetNodeIconClass(AdminNode node, string iconClass)
    {
        switch (node)
        {
            case LinkAdminNode link:
                link.IconClass = iconClass;
                break;
            case PlaceholderAdminNode placeholder:
                placeholder.IconClass = iconClass;
                break;
        }
    }

    private static AdminNode? FindNode(IEnumerable<MenuItem> nodes, string uniqueId)
    {
        foreach (var item in nodes)
        {
            if (item is AdminNode node && string.Equals(node.UniqueId, uniqueId, StringComparison.Ordinal))
            {
                return node;
            }

            var child = FindNode(item.Items, uniqueId);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }
}

/// <summary>
/// Maps each imported item's culture-invariant match key to the admin node created for it.
/// Kept in Crest's own document so that <see cref="AdminNode"/> stays exactly as OrchardCore
/// defines it.
/// </summary>
public sealed class CrestProviderMenuSyncDocument : Document
{
    /// <summary>
    /// Match key (parent path + invariant caption) to the node it was imported as.
    /// </summary>
    public Dictionary<string, CrestProviderMenuSyncEntry> Entries { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Per culture: every caption whose translation the seeding pass has ever written or found
    /// already present. An absent store entry for a seen pair means someone deleted it, and the
    /// automatic pass will not refill it - see <c>SeedCaptionTranslationsAsync</c>.
    /// </summary>
    public Dictionary<string, List<string>> SeededCaptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CrestProviderMenuSyncEntry
{
    public string UniqueId { get; set; } = string.Empty;

    /// <summary>
    /// The URL as last imported from the provider. Comparing the node's current URL against
    /// this is what distinguishes provider drift (node still carries the imported value, so a
    /// changed provider target is followed) from an admin's deliberate edit (node differs from
    /// the imported value, so the admin's URL is left alone).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Whether the provider still contributes this item. False once its feature is disabled;
    /// the node itself is kept so re-enabling restores the tenant's overrides.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

public sealed class CrestProviderMenuSyncResult
{
    public bool MenuCreated { get; set; }
    public int Added { get; set; }
    public int Disabled { get; set; }
    public int Reenabled { get; set; }
    public int Updated { get; set; }

    /// <summary>
    /// Translations copied from the PO catalogs into the tenant translation store this pass.
    /// Deliberately not part of <see cref="HasChanges"/>: seeding writes its own document
    /// directly and must not force a menu save.
    /// </summary>
    public int SeededTranslations { get; set; }

    [JsonIgnore]
    public bool HasChanges => MenuCreated || Added > 0 || Disabled > 0 || Reenabled > 0 || Updated > 0;
}
