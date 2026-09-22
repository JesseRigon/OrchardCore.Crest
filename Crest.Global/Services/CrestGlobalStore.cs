using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.MySql;
using YesSql.Provider.PostgreSql;
using YesSql.Provider.Sqlite;
using YesSql.Provider.SqlServer;
using YesSql.Serialization;
using YesSql.Sql;

namespace Crest.Global.Services;

/// <summary>
/// Host-level singleton owning the tenant-less store. Built the way OrchardCore.Data
/// builds a tenant's store (a hand-assembled YesSql <see cref="Configuration"/> switched
/// on the provider) but from <see cref="CrestGlobalStoreOptions"/> instead of ShellSettings,
/// so no shell is involved. Schema steps are discovered from every loaded module assembly
/// implementing <see cref="ICrestGlobalSchema"/> and applied once, versioned, on first use.
/// </summary>
// NOT IDisposable/IAsyncDisposable, deliberately. Orchard clones host singletons into
// each tenant container through a factory delegate, and MS DI disposes factory-produced
// disposables when a tenant container is torn down (feature toggle, shell reload) - which
// would dispose this process-wide store under every other tenant. The underlying IStore is
// released on application shutdown by the builder extension instead.
public sealed class CrestGlobalStore : ICrestGlobalStore
{
    public const string GlobalFolderName = "CrestGlobal";

    private readonly IServiceProvider _services;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private readonly IOptions<ShellOptions> _shellOptions;
    private readonly IOptions<DocumentJsonSerializerOptions> _serializerOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<CrestGlobalStore> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IStore? _store;
    private IReadOnlyList<ICrestGlobalSchema> _schemas = [];

    public CrestGlobalStore(
        IServiceProvider services,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        IOptions<ShellOptions> shellOptions,
        IOptions<DocumentJsonSerializerOptions> serializerOptions,
        ILoggerFactory loggerFactory)
    {
        _services = services;
        _configuration = configuration;
        _shellOptions = shellOptions;
        _serializerOptions = serializerOptions;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<CrestGlobalStore>();
    }

    public async Task<T> ReadAsync<T>(Func<ISession, CancellationToken, Task<T>> query, CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(cancellationToken);
        await using var session = store.CreateSession();
        return await query(session, cancellationToken);
    }

    public async Task WriteAsync(Func<ISession, CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(cancellationToken);
        await using var session = store.CreateSession();
        try
        {
            await work(session, cancellationToken);
            var state = await session.Query<CrestGlobalSchemaState>().FirstOrDefaultAsync(cancellationToken) ?? new CrestGlobalSchemaState();
            state.DataVersion++;
            await session.SaveAsync(state);
            await session.SaveChangesAsync();
        }
        catch
        {
            await session.CancelAsync();
            throw;
        }
    }

    public async Task<long> DataVersionAsync(CancellationToken cancellationToken = default)
    {
        var store = await GetStoreAsync(cancellationToken);
        await using var session = store.CreateSession();
        return (await session.Query<CrestGlobalSchemaState>().FirstOrDefaultAsync(cancellationToken))?.DataVersion ?? 0;
    }

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => GetStoreAsync(cancellationToken);

    private async Task<IStore> GetStoreAsync(CancellationToken cancellationToken)
    {
        if (_store is not null)
        {
            return _store;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_store is not null)
            {
                return _store;
            }

            var options = ReadOptions();
            var configuration = BuildConfiguration(options);
            _schemas = DiscoverSchemas();

            var store = await StoreFactory.CreateAndInitializeAsync(configuration);
            store.RegisterIndexes(_schemas.SelectMany(schema => schema.IndexProviders()).ToArray());

            await ApplySchemasAsync(store, cancellationToken);

            _store = store;
            _logger.LogInformation("Crest global store ready ({Provider}, prefix '{Prefix}', {Schemas} schema(s)).",
                options.DatabaseProvider ?? DatabaseProviderValue.Sqlite, options.TablePrefix, _schemas.Count);
            return store;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private CrestGlobalStoreOptions ReadOptions()
    {
        var options = Microsoft.Extensions.Configuration.ConfigurationBinder.Get<CrestGlobalStoreOptions>(
            _configuration.GetSection("OrchardCore").GetSection(CrestGlobalStoreOptions.SectionName)) ?? new CrestGlobalStoreOptions();

        options.TablePrefix ??= CrestGlobalStoreOptions.DefaultTablePrefix;
        return options;
    }

    private Configuration BuildConfiguration(CrestGlobalStoreOptions options)
    {
        var tableOptions = new DatabaseTableOptions
        {
            DocumentTable = "Document",
            TableNameSeparator = "_",
            IdentityColumnSize = nameof(IdentityColumnSize.Int64),
        };

        var configuration = new Configuration
        {
            TableNameConvention = new TableNameConventionFactory().Create(tableOptions),
            IdentityColumnSize = IdentityColumnSize.Int64,
            Logger = _loggerFactory.CreateLogger("YesSql.CrestGlobal"),
            ContentSerializer = new DefaultContentJsonSerializer(_serializerOptions.Value.SerializerOptions),
        };

        var provider = string.IsNullOrWhiteSpace(options.DatabaseProvider) ? DatabaseProviderValue.Sqlite : options.DatabaseProvider;
        switch (provider)
        {
            case DatabaseProviderValue.Sqlite:
                var folder = Path.Combine(_shellOptions.Value.ShellsApplicationDataPath, GlobalFolderName);
                Directory.CreateDirectory(folder);
                var connectionString = SqliteHelper.GetConnectionString(
                    new SqliteOptions(), folder,
                    string.IsNullOrWhiteSpace(options.DatabaseName) ? CrestGlobalStoreOptions.DefaultSqliteDatabaseName : options.DatabaseName,
                    SqliteOpenMode.ReadWriteCreate);
                configuration.UseSqLite(connectionString).UseDefaultIdGenerator();
                break;
            case DatabaseProviderValue.Postgres:
                configuration.UsePostgreSql(Require(options.ConnectionString, provider), schema: options.Schema).UseBlockIdGenerator();
                break;
            case DatabaseProviderValue.SqlConnection:
                configuration.UseSqlServer(Require(options.ConnectionString, provider), schema: options.Schema).UseBlockIdGenerator();
                break;
            case DatabaseProviderValue.MySql:
                configuration.UseMySql(Require(options.ConnectionString, provider), schema: options.Schema).UseBlockIdGenerator();
                break;
            default:
                throw new InvalidOperationException($"Unknown database provider '{provider}' for the Crest global store.");
        }

        if (!string.IsNullOrWhiteSpace(options.TablePrefix))
        {
            configuration.SetTablePrefix(options.TablePrefix.Trim() + tableOptions.TableNameSeparator);
        }

        return configuration;
    }

    private static string Require(string? connectionString, string provider) =>
        string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException($"The Crest global store needs a ConnectionString for provider '{provider}' (section OrchardCore:{CrestGlobalStoreOptions.SectionName}).")
            : connectionString;

    // Modules contribute global documents without the host naming each one: any loaded
    // assembly that references Crest.Global is scanned for ICrestGlobalSchema types.
    // Orchard loads every module assembly at startup, before any shell is built, so the
    // first store use sees them all.
    private IReadOnlyList<ICrestGlobalSchema> DiscoverSchemas()
    {
        var self = typeof(ICrestGlobalSchema).Assembly.GetName().Name;
        var schemas = new List<ICrestGlobalSchema>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            var references = assembly == typeof(ICrestGlobalSchema).Assembly
                || assembly.GetReferencedAssemblies().Any(reference => string.Equals(reference.Name, self, StringComparison.Ordinal));
            if (!references)
            {
                continue;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(type => type is not null).Select(type => type!).ToArray();
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || !typeof(ICrestGlobalSchema).IsAssignableFrom(type))
                {
                    continue;
                }

                if (ActivatorUtilities.CreateInstance(_services, type) is ICrestGlobalSchema schema)
                {
                    schemas.Add(schema);
                }
            }
        }

        return schemas.OrderBy(schema => schema.Name, StringComparer.Ordinal).ToArray();
    }

    private async Task ApplySchemasAsync(IStore store, CancellationToken cancellationToken)
    {
        await using var session = store.CreateSession();
        try
        {
            var state = await session.Query<CrestGlobalSchemaState>().FirstOrDefaultAsync() ?? new CrestGlobalSchemaState();
            var transaction = await session.BeginTransactionAsync();
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            var context = new SchemaContext(schemaBuilder, session, _services);

            foreach (var schema in _schemas)
            {
                state.Versions.TryGetValue(schema.Name, out var applied);
                if (applied >= schema.Version)
                {
                    continue;
                }

                _logger.LogInformation("Applying Crest global schema '{Schema}' {From} -> {To}.", schema.Name, applied, schema.Version);
                await schema.ApplyAsync(context, applied, cancellationToken);
                state.Versions[schema.Name] = schema.Version;
            }

            await session.SaveAsync(state);
            await session.SaveChangesAsync();
        }
        catch
        {
            await session.CancelAsync();
            throw;
        }
    }

    private sealed record SchemaContext(ISchemaBuilder SchemaBuilder, ISession Session, IServiceProvider Services) : ICrestGlobalSchemaContext;

    /// <summary>Releases the underlying store. Called once, at application shutdown - never by a tenant container.</summary>
    public void Shutdown()
    {
        _store?.Dispose();
        _store = null;
    }
}
