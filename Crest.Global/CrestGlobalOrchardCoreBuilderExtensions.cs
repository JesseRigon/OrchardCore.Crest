using Crest.Global.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OrchardCore.Modules;

namespace Crest.Global;

public static class CrestGlobalOrchardCoreBuilderExtensions
{
    /// <summary>
    /// Registers the tenant-less reference-data store at HOST level, the way Orchard's own
    /// <c>AddDatabaseShellsConfiguration</c> registers its shells database. Call from the
    /// host's <c>Program.cs</c>: <c>AddOrchardCms().AddCrestGlobalStore()</c>. Shell
    /// containers resolve host singletons, so every tenant's services see one store.
    /// </summary>
    public static OrchardCoreBuilder AddCrestGlobalStore(this OrchardCoreBuilder builder)
    {
        builder.ApplicationServices.TryAddSingleton<CrestGlobalStore>();
        builder.ApplicationServices.TryAddSingleton<ICrestGlobalStore>(sp => sp.GetRequiredService<CrestGlobalStore>());
        // One cache per host process; tenant containers see the same instance, so a write
        // through any tenant's Default-shell endpoint invalidates every tenant's reads.
        builder.ApplicationServices.TryAddSingleton<ICrestGlobalCache, CrestGlobalCache>();
        builder.ApplicationServices.AddHostedService<CrestGlobalStoreLifetime>();
        return builder;
    }
}

/// <summary>Releases the global store's connections when the application stops - the only disposal the store ever gets.</summary>
internal sealed class CrestGlobalStoreLifetime(CrestGlobalStore store) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        store.Shutdown();
        return Task.CompletedTask;
    }
}
