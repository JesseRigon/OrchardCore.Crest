using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace Crest.Services;

/// <summary>
/// Imports the provider-contributed admin menu right after the shell activates, so the first
/// admin request already renders imported nodes - under the UniqueIds a layout import
/// pre-seeded - rather than the raw provider tree.
/// </summary>
/// <remarks>
/// Activation has no request of its own, and the import resolves each item's Href through
/// IUrlHelper. So the work is deferred to the scope that runs once the activation scope
/// completes - OrchardCore activates in a scope of its own, so that is still before whatever
/// triggered the activation (the first request, or the background service) goes on to do its
/// work - and given a synthetic request context built the way ModularBackgroundService builds
/// one: <see cref="ShellContextExtensions.CreateHttpContext"/> for a tenant-correct host and
/// PathBase, the tenant pipeline built first so the endpoint data sources IUrlHelper resolves
/// routes against exist, and a placeholder endpoint on the context so UrlHelperFactory hands
/// out its LinkGenerator-backed helper (without an endpoint it falls back to the legacy
/// router-based one, which throws on a context that never went through routing).
///
/// <para>
/// The synthetic context is passed explicitly and deliberately NOT assigned to
/// IHttpContextAccessor the way the background service does. Its setter clears the holder the
/// enclosing async flow shares before installing a new one in the current flow only - so an
/// assignment here would null out the real HttpContext of the request whose activation this
/// runs inside, and that request would then fail in the first thing that reads the accessor
/// (the admin theme selector). The import only needs the context it is handed.
/// </para>
///
/// <para>
/// The provider-only build the import works from calls each INavigationProvider directly and
/// never filters by user, so no principal is needed. Only a running tenant is imported: the
/// setup shell activates too, before site settings or the recipe's layout exist.
/// </para>
/// </remarks>
public sealed class CrestProviderMenuSyncTenantEvents(
    ShellSettings shellSettings,
    ILogger<CrestProviderMenuSyncTenantEvents> logger) : ModularTenantEvents
{
    public override Task ActivatedAsync()
    {
        if (!shellSettings.IsRunning())
        {
            return Task.CompletedTask;
        }

        ShellScope.AddDeferredTask(SyncAsync);
        return Task.CompletedTask;
    }

    private async Task SyncAsync(ShellScope scope)
    {
        try
        {
            var shellContext = scope.ShellContext;
            if (!shellContext.HasPipeline())
            {
                // Populates the endpoint data sources; without them every route-based Href
                // would resolve to null and be baked into the imported nodes as a dead link.
                await shellContext.BuildPipelineAsync();
            }

            var httpContext = shellContext.CreateHttpContext();
            httpContext.SetEndpoint(new Endpoint(requestDelegate: null, EndpointMetadataCollection.Empty, nameof(CrestProviderMenuSyncTenantEvents)));

            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            await scope.ServiceProvider.GetRequiredService<CrestProviderMenuSyncCoordinator>().EnsureSyncedAsync(actionContext);
        }
        catch (Exception e)
        {
            // The coordinator already contains sync failures; this only guards the context setup
            // above. Nothing here may fail activation - the request path imports on first use.
            logger.LogError(e, "Failed to import provider navigation after activation of tenant '{TenantName}'.", shellSettings.Name);
        }
    }
}
