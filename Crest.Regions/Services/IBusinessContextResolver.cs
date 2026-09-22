using Crest.Regions.Models;
using OrchardCore.ContentManagement;

namespace Crest.Regions.Services;

public sealed record BusinessContextModel(
    string ContentItemId,
    string Key,
    string DisplayText,
    string? DefaultCountry,
    string? MeasurementSystem,
    string? TimeZone,
    string? Language,
    bool Enabled);

/// <summary>
/// Party → context. Reads <see cref="CrestBusinessContextReferencePart"/> off the given
/// item; absent or dangling means "no context", never an exception, so an item whose
/// owning module never attached the reference keeps working with tenant defaults.
/// </summary>
public interface IBusinessContextResolver
{
    Task<BusinessContextModel?> ResolveAsync(ContentItem? carrier, CancellationToken cancellationToken = default);
    Task<BusinessContextModel?> GetAsync(string contentItemId, CancellationToken cancellationToken = default);
    Task<BusinessContextModel?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessContextModel>> ListAsync(CancellationToken cancellationToken = default);
}
