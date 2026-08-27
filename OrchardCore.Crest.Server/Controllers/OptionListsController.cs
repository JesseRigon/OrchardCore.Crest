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

// The management API behind Option Lists. The SCREENS live in Crest.Admin's wasm
// pages; Fruitful modules only declare and consume sets, they never own these editors.
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/option-lists")]
public sealed class OptionListsController(
    ICrestOptionListService optionLists,
    IEnumerable<IOptionSourceProvider> sourceProviders,
    IContentDefinitionManager contentDefinitionManager,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestOptionListModel[]>> ListAsync()
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok((await optionLists.ListAsync(HttpContext.RequestAborted)).ToArray());
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<CrestOptionListModel>> GetAsync(string key)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        var list = await optionLists.GetAsync(key, HttpContext.RequestAborted);
        return list is null ? NotFound() : Ok(list);
    }

    /// <summary>The options an editor should offer: ordered, hidden ones dropped.</summary>
    [HttpGet("{key}/selectable")]
    public async Task<ActionResult<CrestOptionModel[]>> GetSelectableAsync(string key)
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok((await optionLists.GetSelectableAsync(key, HttpContext.RequestAborted)).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<CrestOptionListModel>> CreateAsync([FromBody] CreateOptionListRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        try
        {
            return Ok(await optionLists.CreateListAsync(request.Key, request.DisplayText, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{key}/options")]
    public async Task<ActionResult<CrestOptionModel>> AddOptionAsync(string key, [FromBody] AddOptionRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        try
        {
            return Ok(await optionLists.AddOptionAsync(key, request.Key, request.DisplayText, request.Position, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // The technical key is deliberately not updatable - it is the contract module
    // code matches on. Tenants relabel, reorder and hide instead.
    [HttpPut("{key}/options/{optionKey}")]
    public async Task<ActionResult<CrestOptionModel>> UpdateOptionAsync(string key, string optionKey, [FromBody] UpdateOptionRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        try
        {
            return Ok(await optionLists.UpdateOptionAsync(key, optionKey, request.DisplayText, request.Position, request.Hidden, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Rows for a picker: whichever columns the caller asks for, from whichever
    /// source the field is bound to. This is the endpoint the option picker component
    /// queries as the user types.
    /// </summary>
    [HttpPost("~/api/crest/option-sources/query")]
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

        var rows = await provider.QueryAsync(
            new OptionSourceQuery(
                qualifier,
                request.Columns ?? [],
                request.SearchText,
                resolution.Filters,
                request.SearchColumns,
                request.SortColumns,
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
    [HttpGet("~/api/crest/option-sources/dependencies")]
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
    [HttpPost("~/api/crest/option-sources/validate-selection")]
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
        var resolution = OptionFilterResolver.Resolve(settings?.Filters, ToEditorState(request.EditorState));
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

    private async Task<OptionFilterResolution> ResolveFiltersAsync(
        string? contentType,
        string? fieldName,
        IReadOnlyDictionary<string, string[]>? editorState)
    {
        var settings = await GetFieldSettingsAsync(contentType, fieldName);
        return settings is null
            ? OptionFilterResolution.Unrestricted
            : OptionFilterResolver.Resolve(settings.Filters, ToEditorState(editorState));
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

    /// <summary>Rows for already-selected ids, so a picker can render what is
    /// stored. Batched: one call for every id on the field.</summary>
    [HttpPost("~/api/crest/option-sources/resolve")]
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
    [HttpGet("~/api/crest/option-sources/{sourceKey}/columns")]
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
    [HttpGet("~/api/crest/option-sources")]
    public async Task<ActionResult<string[]>> ListSourcesAsync()
    {
        if (!await CanViewAsync())
        {
            return Forbid();
        }

        return Ok(sourceProviders.Select(provider => provider.Key).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray());
    }

    [HttpPost("{key}/attach")]
    public async Task<IActionResult> AttachAsync(string key, [FromBody] AttachOptionListRequest request)
    {
        if (!await CanManageAsync())
        {
            return Forbid();
        }

        try
        {
            await optionLists.AttachToContentTypeAsync(key, request.ContentType, request.FieldName, request.DisplayName, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private Task<bool> CanViewAsync() => IsAuthorizedAsync(CrestOptionListPermissions.ViewOptionLists);

    private Task<bool> CanManageAsync() => IsAuthorizedAsync(CrestOptionListPermissions.ManageOptionLists);

    private async Task<bool> IsAuthorizedAsync(OrchardCore.Security.Permissions.Permission permission) =>
        await authorizationService.AuthorizeAsync(User, permission);
}

public sealed record CreateOptionListRequest(string Key, string DisplayText);

public sealed record AddOptionRequest(string Key, string DisplayText, int Position = 0);

public sealed record UpdateOptionRequest(string? DisplayText, int? Position, bool? Hidden);

public sealed record AttachOptionListRequest(string ContentType, string FieldName, string? DisplayName);

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
