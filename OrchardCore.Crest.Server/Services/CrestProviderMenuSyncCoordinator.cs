using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Crest.Services;

/// <summary>
/// Runs <see cref="CrestProviderMenuSyncService"/> once per shell, on the first request that
/// needs the admin menu.
/// </summary>
/// <remarks>
/// Call sites treat this as fire-and-forget bookkeeping: it is called before the menu is read so
/// that imported nodes are present, but a failure here must never take down the request that
/// happened to be first. A sync that throws releases the gate so the next request retries,
/// rather than leaving the shell permanently un-synced.
/// </remarks>
public sealed class CrestProviderMenuSyncCoordinator(
    CrestProviderMenuSyncGate gate,
    CrestProviderMenuSyncService syncService,
    ILogger<CrestProviderMenuSyncCoordinator> logger)
{
    public async Task EnsureSyncedAsync(ActionContext actionContext)
    {
        if (!gate.TryClaim())
        {
            return;
        }

        try
        {
            var result = await syncService.SyncAsync(actionContext);
            if (result.HasChanges)
            {
                logger.LogInformation(
                    "Imported provider navigation into the admin menu system: {Added} added, {Reenabled} re-enabled, {Disabled} disabled{Created}.",
                    result.Added,
                    result.Reenabled,
                    result.Disabled,
                    result.MenuCreated ? ", menu created" : string.Empty);
            }
        }
        catch (Exception e)
        {
            gate.Release();
            logger.LogError(e, "Failed to import provider navigation into the admin menu system.");
        }
    }
}
