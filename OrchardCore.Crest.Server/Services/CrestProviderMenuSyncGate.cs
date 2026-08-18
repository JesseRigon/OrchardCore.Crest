namespace Crest.Services;

/// <summary>
/// Tracks whether the provider-menu import has already run for this shell.
/// </summary>
/// <remarks>
/// The import has to run once per shell, not once per request. Registered as a singleton, whose
/// lifetime in OrchardCore is the shell's: enabling or disabling a feature changes the shell
/// descriptor, which releases the shell (see <c>ShellDescriptorManager.ChangedAsync</c>), so the
/// next request builds a new container with a fresh instance of this gate and the import runs
/// again - picking up exactly the features that just became available.
///
/// <para>
/// It cannot run at <c>IModularTenantEvents.ActivatedAsync</c>, which would be the obvious
/// place: <c>INavigationManager.BuildMenuAsync</c> needs an <c>ActionContext</c> because it
/// resolves each item's Href through <c>IUrlHelper</c>, and there is no request - and therefore
/// no <c>HttpContext</c> - during activation. Deferring to the first request that needs the menu
/// is what makes a real <c>ActionContext</c> available.
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
