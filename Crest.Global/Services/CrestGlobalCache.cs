using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Crest.Global.Services;

public sealed class CrestGlobalCache(ICrestGlobalStore store, IOptions<CrestGlobalStoreOptions> options) : ICrestGlobalCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private long _version = -1;
    private DateTimeOffset _checkedAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _versionLock = new(1, 1);

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default)
    {
        var version = await CurrentVersionAsync(cancellationToken);
        if (_entries.TryGetValue(key, out var entry) && entry.Version == version && entry.Value is T cached)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        _entries[key] = new Entry(version, value);
        return value;
    }

    public void Clear()
    {
        _entries.Clear();
        _checkedAt = DateTimeOffset.MinValue;
    }

    private async Task<long> CurrentVersionAsync(CancellationToken cancellationToken)
    {
        var window = TimeSpan.FromSeconds(Math.Max(0, options.Value.CacheVersionCheckSeconds));
        if (DateTimeOffset.UtcNow - _checkedAt < window)
        {
            return _version;
        }

        await _versionLock.WaitAsync(cancellationToken);
        try
        {
            if (DateTimeOffset.UtcNow - _checkedAt < window)
            {
                return _version;
            }

            var latest = await store.DataVersionAsync(cancellationToken);
            if (latest != _version)
            {
                // Everything read at the old version lapses at once.
                _entries.Clear();
                _version = latest;
            }

            _checkedAt = DateTimeOffset.UtcNow;
            return _version;
        }
        finally
        {
            _versionLock.Release();
        }
    }

    private sealed record Entry(long Version, object? Value);
}
