using System.Globalization;
using Crest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DataLocalization.Models;
using OrchardCore.DataLocalization.Services;
using OrchardCore.Localization;
using OrchardCore.Localization.Data;

namespace Crest.ViewModels;

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
/// <param name="Fallback">For rows without a stored value: the shipped PO translation that
/// actually renders (resolution is store edit -> PO -> invariant literal), shown as the
/// input's placeholder. Deleting a stored value reveals this layer rather than the raw
/// literal; saving the key itself as the value pins the literal over it.</param>
public sealed record CrestTranslationString(string Context, string Key, string Value, bool Orphan, string? Fallback = null);

public sealed record CrestTranslationsSaveModel(string Culture, CrestTranslationSaveEntry[]? Translations);

public sealed record CrestTranslationSaveEntry(string Context, string Key, string? Value);
