using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DataLocalization.Models;
using OrchardCore.DataLocalization.Services;
using OrchardCore.Localization;
using OrchardCore.Localization.Data;

namespace Crest.Controllers;

/// <summary>
/// The Crest translations editor's API - the same functionality as Orchard's stock
/// Translations page (Configuration → Localization → Translations), backed by the merge-correct
/// write discipline every other Crest writer already follows.
/// </summary>
/// <remarks>
/// The stock page's Save replaces a culture's whole translation list with whatever its editor
/// enumerated, silently deleting stored entries no provider currently enumerates (a disabled
/// feature's strings, an old key after a caption changed - see fruitful's
/// plans/upstream-orchard-proposals.md #3). This API differs on exactly the two points that
/// matter:
/// <list type="bullet">
/// <item>reads include ORPHANS - stored entries with no live descriptor - flagged as such, so
/// they are visible, editable and deletable instead of invisible;</item>
/// <item>the save merges: only the rows the client actually displayed are replaced (a blanked
/// displayed row deletes its entry), and every stored entry outside the displayed set is
/// carried over untouched.</item>
/// </list>
/// Per-culture authorization mirrors the stock page: ManageTranslations grants everything,
/// otherwise the per-culture dynamic permission decides edit rights, and
/// ViewDynamicTranslations grants read-only access.
/// </remarks>
[ApiController, AutoValidateAntiforgeryToken, Route("api/crest/translations")]
public sealed class CrestTranslationsController(
    IAuthorizationService authorization,
    ILocalizationService localizationService,
    IEnumerable<ILocalizationDataProvider> localizationDataProviders,
    TranslationsManager translationsManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestTranslations>> GetAsync(string? culture = null)
    {
        var cultures = await GetAllowedCulturesAsync();
        if (cultures.Length == 0)
        {
            return Forbid();
        }

        var selected = cultures.FirstOrDefault(candidate =>
            string.Equals(candidate.Value, culture, StringComparison.OrdinalIgnoreCase)) ?? cultures[0];

        return Ok(new CrestTranslations(
            cultures,
            selected.Value,
            selected.CanEdit,
            await BuildGroupsAsync(selected.Value)));
    }

    [HttpPut]
    public async Task<ActionResult<CrestTranslations>> SaveAsync(CrestTranslationsSaveModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Culture))
        {
            return BadRequest("Culture is required.");
        }

        var cultures = await GetAllowedCulturesAsync();
        var target = cultures.FirstOrDefault(candidate =>
            string.Equals(candidate.Value, model.Culture, StringComparison.OrdinalIgnoreCase));
        if (target is null || !target.CanEdit)
        {
            return Forbid();
        }

        // Merge, never replace-wholesale: the posted rows are the ones the editor DISPLAYED
        // (blank value = delete that entry); any stored entry outside the displayed set is
        // carried over untouched, so nothing the client never saw can be destroyed by saving.
        var displayed = model.Translations ?? [];
        var displayedKeys = displayed
            .Select(entry => MakeKey(entry.Context, entry.Key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var document = await translationsManager.GetTranslationsDocumentAsync();
        var carried = document.Translations.TryGetValue(target.Value, out var current)
            ? current.Where(entry => !displayedKeys.Contains(MakeKey(entry.Context, entry.Key))).ToList()
            : [];

        carried.AddRange(displayed
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.Context) &&
                !string.IsNullOrWhiteSpace(entry.Key) &&
                !string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => new Translation
            {
                Context = entry.Context,
                Key = entry.Key,
                Value = entry.Value!.Trim(),
            }));

        await translationsManager.UpdateTranslationAsync(target.Value, carried);

        // fresh: true - the immutable document is the CACHED copy, which does not reflect this
        // request's own update until the deferred save commits after the response is written.
        // Building the response from it would show the page its pre-save values (the same
        // one-behind trap AdminMenusController.GetDefaultMenuSummaryAsync documents).
        return Ok(new CrestTranslations(
            cultures,
            target.Value,
            target.CanEdit,
            await BuildGroupsAsync(target.Value, fresh: true)));
    }

    /// <summary>
    /// Every translatable string for the culture: the live descriptors from all providers,
    /// overlaid with stored values - plus every stored entry no descriptor covers, flagged as
    /// an orphan and slotted into its context's group.
    /// </summary>
    private async Task<CrestTranslationGroup[]> BuildGroupsAsync(string culture, bool fresh = false)
    {
        var document = fresh
            ? await translationsManager.LoadTranslationsDocumentAsync()
            : await translationsManager.GetTranslationsDocumentAsync();
        // First-wins on duplicate (context, key) pairs rather than ToDictionary's throw - a
        // store that somehow holds duplicates must degrade to showing one row, not 500 the
        // whole editor (the stock controller's exact failure mode; the merge-save then
        // de-duplicates the store on the next save since the pair lands in the displayed set).
        var stored = new Dictionary<string, Translation>(StringComparer.OrdinalIgnoreCase);
        if (document.Translations.TryGetValue(culture, out var entries))
        {
            foreach (var entry in entries)
            {
                stored.TryAdd(MakeKey(entry.Context, entry.Key), entry);
            }
        }

        var rows = new List<CrestTranslationString>();
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in localizationDataProviders)
        {
            foreach (var descriptor in await provider.GetDescriptorsAsync())
            {
                var key = MakeKey(descriptor.Context, descriptor.Name);
                if (!covered.Add(key))
                {
                    continue;
                }

                stored.TryGetValue(key, out var entry);
                rows.Add(new CrestTranslationString(descriptor.Context, descriptor.Name, entry?.Value ?? string.Empty, Orphan: false));
            }
        }

        foreach (var (key, entry) in stored)
        {
            if (!covered.Contains(key))
            {
                rows.Add(new CrestTranslationString(entry.Context, entry.Key, entry.Value, Orphan: true));
            }
        }

        return rows
            .GroupBy(row => row.Context, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CrestTranslationGroup(
                group.Key,
                group.OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToArray();
    }

    private async Task<CrestTranslationCulture[]> GetAllowedCulturesAsync()
    {
        var supported = await localizationService.GetSupportedCulturesAsync();
        var canManageAll = await authorization.AuthorizeAsync(User, DataLocalizationPermissions.ManageTranslations);
        var canView = await authorization.AuthorizeAsync(User, DataLocalizationPermissions.ViewDynamicTranslations);

        var cultures = new List<CrestTranslationCulture>();
        foreach (var name in supported)
        {
            var cultureInfo = CultureInfo.GetCultureInfo(name);
            var label = string.IsNullOrEmpty(cultureInfo.DisplayName) ? cultureInfo.NativeName : cultureInfo.DisplayName;

            var canEdit = canManageAll ||
                await authorization.AuthorizeAsync(User, DataLocalizationPermissions.CreateCulturePermission(name, label));

            if (canEdit || canView)
            {
                cultures.Add(new CrestTranslationCulture(name, label, canEdit));
            }
        }

        return [.. cultures];
    }

    private static string MakeKey(string context, string key) => $"{context}|{key}";
}

public sealed record CrestTranslations(
    CrestTranslationCulture[] Cultures,
    string Culture,
    bool CanEdit,
    CrestTranslationGroup[] Groups);

public sealed record CrestTranslationCulture(string Value, string Label, bool CanEdit);

public sealed record CrestTranslationGroup(string Name, CrestTranslationString[] Strings);

/// <param name="Orphan">A stored entry no provider currently enumerates - e.g. its feature is
/// disabled, or its source string changed. Still applied at render time by IDataLocalizer;
/// editable and deletable here like any other row.</param>
public sealed record CrestTranslationString(string Context, string Key, string Value, bool Orphan);

public sealed record CrestTranslationsSaveModel(string Culture, CrestTranslationSaveEntry[]? Translations);

public sealed record CrestTranslationSaveEntry(string Context, string Key, string? Value);
