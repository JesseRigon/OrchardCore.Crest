using Crest.Models;
using Crest.Permissions;
using Crest.Services;
using Crest.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace Crest.Controllers;

// The provider-generic half of the picker API: rows, columns, dependencies and
// selection validation for WHATEVER source a field is bound to (content part lists, users,
// content items, ...). Lives in Crest.Server with the provider abstraction itself;
// the Option List management CRUD lives in the Crest.ContentPartLists module.
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/option-sources")]
public sealed class OptionSourcesController(
    IEnumerable<IOptionSourceProvider> sourceProviders,
    IContentDefinitionManager contentDefinitionManager,
    OptionParentValueResolver parentValues,
    IAuthorizationService authorizationService) : ControllerBase
{
    /// <summary>
    /// Rows for a picker: whichever columns the caller asks for, from whichever
    /// source the field is bound to. This is the endpoint the option picker component
    /// queries as the user types.
    /// </summary>
    [HttpPost("query")]
    public async Task<ActionResult<OptionRow[]>> QuerySourceAsync([FromBody] OptionSourceQueryRequest request)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var (providerKey, qualifier) = CrestOptionSourceKeys.Split(request.SourceKey);
        var provider = sourceProviders.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, providerKey, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return Problem($"There is no option source provider named '{providerKey}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Filters come from the ATTACHMENT'S SETTINGS, resolved here against the
        // editor state the caller supplies - never from filters the caller sends.
        // A client that could post its own filters could query any source with any
        // predicate, including paths the dropdown never displays.
        var resolution = await ResolveFiltersAsync(request.ContentType, request.FieldName, request.EditorState);

        // A required parent that has not been chosen means NOTHING to offer. Returning
        // the unfiltered list here would defeat the whole point of requiring it.
        if (resolution.HasUnmetDependencies)
        {
            return Ok(Array.Empty<OptionRow>());
        }

        // Sort is an INSTANCE parameter: the request's explicit sort wins, else the
        // attachment's configured SortColumns, else the provider's natural order.
        // Sort columns AND filter paths are unioned into the requested columns so
        // their values are materialized for the comparison, whether or not the
        // picker displays them - a filter matched against a column the rows never
        // carried would silently exclude everything.
        var settings = await GetFieldSettingsAsync(request.ContentType, request.FieldName);
        var sortColumns = request.SortColumns is { Count: > 0 }
            ? request.SortColumns
            : settings?.SortColumns ?? [];
        var columns = (request.Columns ?? [])
            .Union(sortColumns, StringComparer.OrdinalIgnoreCase)
            .Union(FilterPaths(settings), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = await provider.QueryAsync(
            new OptionSourceQuery(
                qualifier,
                columns,
                request.SearchText,
                resolution.Filters,
                request.SearchColumns,
                sortColumns,
                request.Skip,
                Math.Clamp(request.Take, 1, 200)),
            HttpContext.RequestAborted);

        return Ok(rows.ToArray());
    }

    /// <summary>
    /// Which fields a picker must watch, and whether it is required because its parent
    /// is. The editor asks once when it renders, then re-queries when a watched field
    /// COMMITS - not on every keystroke.
    /// </summary>
    [HttpGet("dependencies")]
    public async Task<ActionResult<OptionDependencyModel>> GetDependenciesAsync(
        [FromQuery] string contentType,
        [FromQuery] string fieldName)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var settings = await GetFieldSettingsAsync(contentType, fieldName);
        if (settings is null)
        {
            return NotFound();
        }

        var partDefinition = await contentDefinitionManager.GetPartDefinitionAsync(contentType);

        return Ok(new OptionDependencyModel(
            OptionFilterResolver.DependencyPaths(settings.Filters),
            settings.Filters.FirstOrDefault(filter => filter.IsDependent)?.OnParentChange
                ?? OptionParentChangeBehaviors.WarnThenClear,
            // Derived, not configured: a child whose parent is required is required too,
            // or the document could be completed with the pair half-filled.
            OptionFilterResolver.IsRequiredBecauseParentIs(settings.Filters, path =>
                IsFieldRequired(partDefinition, path))));
    }

    /// <summary>
    /// Whether the currently-selected ids survive the editor's new state - the check
    /// behind clearing a child when its parent changes. Answered server-side because
    /// only the server knows the attachment's filters.
    /// </summary>
    [HttpPost("validate-selection")]
    public async Task<ActionResult<OptionSelectionValidationModel>> ValidateSelectionAsync(
        [FromBody] OptionSelectionValidationRequest request)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var (providerKey, qualifier) = CrestOptionSourceKeys.Split(request.SourceKey);
        var provider = sourceProviders.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, providerKey, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return Problem($"There is no option source provider named '{providerKey}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        var settings = await GetFieldSettingsAsync(request.ContentType, request.FieldName);
        var resolution = settings is null
            ? OptionFilterResolution.Unrestricted
            : await ResolveTranslatedAsync(request.ContentType!, settings, request.EditorState);
        var ids = request.SelectedIds ?? [];

        // Resolve by id rather than by query: a stale child may well be absent from
        // the filtered list, which is precisely what makes it stale.
        var rows = ids.Count == 0
            ? []
            : await provider.GetByIdsAsync(qualifier, ids, FilterPaths(settings), HttpContext.RequestAborted);

        var stillValid = OptionFilterResolver.SelectionStillValid(resolution, ids, rows);

        return Ok(new OptionSelectionValidationModel(
            stillValid,
            settings?.Filters.FirstOrDefault(filter => filter.IsDependent)?.OnParentChange
                ?? OptionParentChangeBehaviors.WarnThenClear));
    }

    /// <summary>Rows for already-selected ids, so a picker can render what is
    /// stored. Batched: one call for every id on the field.</summary>
    [HttpPost("resolve")]
    public async Task<ActionResult<OptionRow[]>> ResolveSourceAsync([FromBody] OptionSourceResolveRequest request)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var (providerKey, qualifier) = CrestOptionSourceKeys.Split(request.SourceKey);
        var provider = sourceProviders.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, providerKey, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return Problem($"There is no option source provider named '{providerKey}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        var rows = await provider.GetByIdsAsync(qualifier, request.Ids ?? [], request.Columns ?? [], HttpContext.RequestAborted);
        return Ok(rows.ToArray());
    }

    /// <summary>The columns a source can offer - drives the column picker in the
    /// field's settings editor, so paths are never typed blind.</summary>
    [HttpGet("{sourceKey}/columns")]
    public async Task<ActionResult<OptionSourceColumnDescriptor[]>> DescribeColumnsAsync(string sourceKey)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var (providerKey, qualifier) = CrestOptionSourceKeys.Split(sourceKey);
        var provider = sourceProviders.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, providerKey, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return NotFound();
        }

        return Ok((await provider.DescribeColumnsAsync(qualifier, HttpContext.RequestAborted)).ToArray());
    }

    /// <summary>The registered sources a field can be bound to.</summary>
    [HttpGet]
    public async Task<ActionResult<string[]>> ListSourcesAsync()
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(sourceProviders.Select(provider => provider.Key).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray());
    }

    private async Task<OptionFilterResolution> ResolveFiltersAsync(
        string? contentType,
        string? fieldName,
        IReadOnlyDictionary<string, string[]>? editorState)
    {
        var settings = await GetFieldSettingsAsync(contentType, fieldName);
        return settings is null
            ? OptionFilterResolution.Unrestricted
            : await ResolveTranslatedAsync(contentType!, settings, editorState);
    }

    // Dependent filters compare MACHINE data, but a picker parent's editor state
    // carries stored ids - translate id lists into the configured parent column
    // (Key by default) through the parent's own source before resolving.
    private async Task<OptionFilterResolution> ResolveTranslatedAsync(
        string contentType,
        OptionPickerFieldSettings settings,
        IReadOnlyDictionary<string, string[]>? editorState)
    {
        var (filters, state) = await OptionDependentFilterTranslator.TranslateAsync(
            settings.Filters,
            ToEditorState(editorState),
            (path, column, raw) => parentValues.ResolveAsync(contentType, path, column, raw, HttpContext.RequestAborted));

        return OptionFilterResolver.Resolve(filters, state);
    }

    private async Task<OptionPickerFieldSettings?> GetFieldSettingsAsync(string? contentType, string? fieldName)
    {
        if (string.IsNullOrWhiteSpace(contentType) || string.IsNullOrWhiteSpace(fieldName))
        {
            return null;
        }

        var partDefinition = await contentDefinitionManager.GetPartDefinitionAsync(contentType);
        var field = partDefinition?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        return field?.GetSettings<OptionPickerFieldSettings>();
    }

    private static bool IsFieldRequired(ContentPartDefinition? partDefinition, string path)
    {
        // Dependency paths are "FieldName" within the same part, or "Part.Field".
        var fieldName = path.Contains('.') ? path[(path.LastIndexOf('.') + 1)..] : path;
        var field = partDefinition?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));

        // Read through the typed accessor rather than indexing the settings JSON by
        // hand: a hand-written key that does not exist reads as false forever, which
        // would make the derived-required rule silently never fire.
        return field?.GetSettings<OptionPickerFieldSettings>()?.Required ?? false;
    }

    private static IReadOnlyList<string> FilterPaths(OptionPickerFieldSettings? settings) =>
        settings is null ? [] : [.. settings.Filters.Select(filter => filter.Path).Distinct(StringComparer.OrdinalIgnoreCase)];

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? ToEditorState(IReadOnlyDictionary<string, string[]>? state) =>
        state?.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<string>)entry.Value, StringComparer.OrdinalIgnoreCase);

    private Task<bool> CanViewAsync() => IsAuthorizedAsync(CrestContentPartListPermissions.ViewContentPartLists);

    private async Task<bool> IsAuthorizedAsync(OrchardCore.Security.Permissions.Permission permission) =>
        await authorizationService.AuthorizeAsync(User, permission);
}

/// <summary>
/// A picker's request for rows.
/// </summary>
/// <remarks>
/// There is deliberately NO filter list here. Restrictions are read from the
/// attachment's settings server-side and resolved against <paramref name="EditorState"/>;
/// a client that could post its own filters could query any source with any predicate.
/// The client says what the USER has entered, never what the SERVER should allow.
/// </remarks>
public sealed record OptionSourceQueryRequest(
    string SourceKey,
    IReadOnlyList<string>? Columns,
    string? SearchText = null,
    string? ContentType = null,
    string? FieldName = null,
    IReadOnlyDictionary<string, string[]>? EditorState = null,
    IReadOnlyList<string>? SearchColumns = null,
    IReadOnlyList<string>? SortColumns = null,
    int Skip = 0,
    int Take = 50);

public sealed record OptionSourceResolveRequest(
    string SourceKey,
    IReadOnlyList<string>? Ids,
    IReadOnlyList<string>? Columns);

/// <summary>What a picker must watch, and what to do when it changes.</summary>
public sealed record OptionDependencyModel(
    IReadOnlyList<string> DependsOn,
    string OnParentChange,
    bool RequiredBecauseParentIs);

public sealed record OptionSelectionValidationRequest(
    string SourceKey,
    string? ContentType,
    string? FieldName,
    IReadOnlyList<string>? SelectedIds,
    IReadOnlyDictionary<string, string[]>? EditorState);

public sealed record OptionSelectionValidationModel(bool StillValid, string OnParentChange);
