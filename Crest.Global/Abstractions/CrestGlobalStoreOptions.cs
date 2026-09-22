namespace Crest.Global;

/// <summary>
/// Where the tenant-less store lives. Configured under <c>OrchardCore:OrchardCore_Crest_Global</c>
/// with the same four fields Orchard's own <c>OrchardCore_Shells_Database</c> takes. When
/// no section is present the default is a SQLite file beside the tenants folder, which is
/// exactly what a per-tenant SQLite Orchard install would expect; a Postgres/SQL Server
/// operator sets the provider and connection string here, the same way they would for
/// the shells database.
/// </summary>
public sealed class CrestGlobalStoreOptions
{
    /// <summary>How often the cache re-reads the store's data version. Bounds how stale a cached global read can be across host processes.</summary>
    public int CacheVersionCheckSeconds { get; set; } = 5;

    public const string SectionName = "OrchardCore_Crest_Global";
    public const string DefaultTablePrefix = "CrestGlobal";
    public const string DefaultSqliteDatabaseName = "crest-global.db";

    /// <summary><c>Sqlite</c>, <c>Postgres</c>, <c>SqlConnection</c> or <c>MySql</c>. Null selects the SQLite default.</summary>
    public string? DatabaseProvider { get; set; }
    public string? ConnectionString { get; set; }
    public string? TablePrefix { get; set; } = DefaultTablePrefix;
    public string? Schema { get; set; }
    /// <summary>SQLite only: the file name inside the global folder.</summary>
    public string? DatabaseName { get; set; }
}
