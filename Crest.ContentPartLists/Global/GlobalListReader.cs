using Crest.Global;
using YesSql;

namespace Crest.Global.Lists;

/// <summary>Reads the standard lists from the global store through the host cache, with a per-request memo on top.</summary>
public interface IGlobalListReader
{
    Task<GlobalList?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlobalList>> ListAsync(CancellationToken cancellationToken = default);
}

public sealed class GlobalListReader(ICrestGlobalStore store, ICrestGlobalCache cache) : IGlobalListReader
{
    private readonly Dictionary<string, GlobalList?> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<GlobalList>? _all;

    public async Task<GlobalList?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_byKey.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var list = await cache.GetOrCreateAsync($"lists:{key}", ct => store.ReadAsync(
            (session, innerCt) => session.Query<GlobalList, GlobalListIndex>(index => index.Key == key).FirstOrDefaultAsync(innerCt),
            ct), cancellationToken);
        _byKey[key] = list;
        return list;
    }

    public async Task<IReadOnlyList<GlobalList>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (_all is not null)
        {
            return _all;
        }

        _all = await cache.GetOrCreateAsync<IReadOnlyList<GlobalList>>("lists:*", async ct =>
            (await store.ReadAsync((session, innerCt) => session.Query<GlobalList, GlobalListIndex>().ListAsync(innerCt), ct)).ToArray(), cancellationToken);
        foreach (var list in _all)
        {
            _byKey[list.Key] = list;
        }

        return _all;
    }
}
