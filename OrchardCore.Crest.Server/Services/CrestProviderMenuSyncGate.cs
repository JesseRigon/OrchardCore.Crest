namespace Crest.Services;

/// <summary>
/// Tracks whether the provider-menu import has already run for this shell.
/// </summary>
/// <remarks>
/// The import has to run once per shell, not once per request. Registered as a singleton, whose
/// lifetime in OrchardCore is the shell's: enabling or disabling a feature changes the shell
/// descriptor, which releases the shell (see <c>ShellDescriptorManager.ChangedAsync</c>), so the
/// next shell builds a new container with a fresh instance of this gate and the import runs
/// again - picking up exactly the features that just became available.
///
/// <para>
/// Two callers claim it. <see cref="CrestProviderMenuSyncTenantEvents"/> claims it right after
/// activation, in a deferred scope with a synthetic request context, so the import normally
/// completes before any admin request arrives. The request path
/// (<see cref="CrestProviderMenuSyncCoordinator"/> from the navigation and menu-editor
/// controllers) is the fallback for the case where an admin request reaches the menu first,
/// or the activation-time import failed and released the claim.
/// </para>
/// </remarks>
public sealed class CrestProviderMenuSyncGate
{
    private int _state;

    /// <summary>
    /// Returns true exactly once per shell, for the caller that should perform the import.
    /// Concurrent first requests race here so that only one of them syncs.
    /// </summary>
    public bool TryClaim() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;

    /// <summary>
    /// Releases the claim so the import is retried on a later request. Used when a sync attempt
    /// throws: a transient failure should not leave the shell permanently un-synced.
    /// </summary>
    public void Release() => Interlocked.Exchange(ref _state, 0);
}
