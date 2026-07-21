using Crest.Icons;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Crest.Services;

public sealed class IconifyCacheRefreshService(
    IIconifyLocalMirrorStore localMirrorStore,
    ILogger<IconifyCacheRefreshService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await localMirrorStore.GetStatusAsync(stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not initialize the Iconify App_Data cache from the bundled seed.");
        }

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        using var timer = new PeriodicTimer(RefreshInterval);
        do
        {
            try
            {
                var status = await localMirrorStore.SyncAsync(stoppingToken);
                if (status.IsAvailable)
                {
                    logger.LogInformation("Iconify App_Data cache refreshed. Version: {Version}; prefixes: {PrefixCount}; icons: {IconCount}.", status.Version, status.PrefixCount, status.IconCount);
                }
                else if (!string.IsNullOrWhiteSpace(status.LastError))
                {
                    logger.LogWarning("Iconify App_Data cache refresh failed: {Error}", status.LastError);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Iconify App_Data cache refresh failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
