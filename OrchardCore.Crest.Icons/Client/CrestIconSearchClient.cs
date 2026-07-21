using System.Net.Http.Json;

namespace Crest.Icons;

public interface ICrestIconSearchClient
{
    Task<IconSearchResult> SearchAsync(
        string? library,
        string? query,
        int skip,
        int take,
        IEnumerable<IconSearchFilter>? filters = null,
        CancellationToken cancellationToken = default);
}

public sealed class CrestIconSearchClient(HttpClient httpClient) : ICrestIconSearchClient
{
    public async Task<IconSearchResult> SearchAsync(
        string? library,
        string? query,
        int skip,
        int take,
        IEnumerable<IconSearchFilter>? filters = null,
        CancellationToken cancellationToken = default)
    {
        var queryString = new List<string>
        {
            $"skip={Math.Max(0, skip)}",
            $"take={Math.Clamp(take, 1, 200)}",
        };

        AddQueryValue(queryString, "library", library);
        AddQueryValue(queryString, "query", query);

        foreach (var filter in filters ?? [])
        {
            AddQueryValue(queryString, "filter", $"{filter.Facet}:{filter.Value}");
        }

        return await httpClient.GetFromJsonAsync<IconSearchResult>(
            "api/crest/icons?" + string.Join("&", queryString),
            cancellationToken) ?? new IconSearchResult([], [], [], 0, Math.Max(0, skip), Math.Clamp(take, 1, 200));
    }

    private static void AddQueryValue(List<string> queryString, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        queryString.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
    }
}
