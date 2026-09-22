using YesSql;

namespace Crest.Global;

/// <summary>
/// The tenant-less YesSql store. Every use is a unit of work: outside a shell scope nothing
/// auto-commits, so <see cref="WriteAsync"/> saves and the store never hands out a raw
/// session. Reads and writes from any tenant's code go through this; only the loader and
/// the Default tenant hold write paths (plans/global.md).
/// </summary>
public interface ICrestGlobalStore
{
    Task<T> ReadAsync<T>(Func<ISession, CancellationToken, Task<T>> query, CancellationToken cancellationToken = default);

    /// <summary>A write is a unit of work and bumps the store's data version, which invalidates <see cref="ICrestGlobalCache"/> everywhere.</summary>
    Task WriteAsync(Func<ISession, CancellationToken, Task> work, CancellationToken cancellationToken = default);

    /// <summary>The store's data version: incremented by every write, by any host process. Cheap to read.</summary>
    Task<long> DataVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds the store and applies every registered schema step. Idempotent; called lazily by the first read or write.</summary>
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A versioned schema contribution to the global store: index tables and their loaders.
/// Discovered by scanning module assemblies, so a module declares its global documents
/// without the host naming it. Migrations must be FRESH-INSTALL REPEATABLE: <see cref="ApplyAsync"/>
/// receives the currently applied version (0 on a new store) and brings the store to
/// <see cref="Version"/>; superseded steps are deleted, not accumulated.
/// </summary>
public interface ICrestGlobalSchema
{
    /// <summary>Stable name recorded with the applied version.</summary>
    string Name { get; }

    int Version { get; }

    /// <summary>Index providers this schema's documents need registered on the store.</summary>
    IEnumerable<YesSql.Indexes.IIndexProvider> IndexProviders();

    Task ApplyAsync(ICrestGlobalSchemaContext context, int fromVersion, CancellationToken cancellationToken);
}

public interface ICrestGlobalSchemaContext
{
    YesSql.Sql.ISchemaBuilder SchemaBuilder { get; }
    ISession Session { get; }
    IServiceProvider Services { get; }
}

/// <summary>One row per schema: what version has been applied.</summary>
public sealed class CrestGlobalSchemaState
{
    public long Id { get; set; }
    public Dictionary<string, int> Versions { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Bumped by every write; the cache key every host process checks against.</summary>
    public long DataVersion { get; set; }
}

/// <summary>
/// Host-level cache over global reads, version-keyed (plans/global.md phase 7). An entry
/// is valid while the store's data version is the one it was read at; a write anywhere -
/// this process, another host on the same database - moves the version and every entry
/// lapses. The version is re-read from the store at most once per <see cref="CrestGlobalStoreOptions.CacheVersionCheckSeconds"/>,
/// so a hot read costs nothing and a stale read is bounded by that window.
/// </summary>
public interface ICrestGlobalCache
{
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default);

    /// <summary>Drops every entry now, without waiting for the version check.</summary>
    void Clear();
}
