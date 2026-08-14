using Crest.Admin.Api;
using Crest.Admin.Theme;
using Crest.Components.Primitives;
using Crest.Components.Theme;
using Crest.Icons;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Reflection;

namespace Crest.Admin.DisplayManagement;

public sealed class DisplayManager(IApi api, CrestThemeEngine themeEngine, ClientIconRegistry iconRegistry, NavigationManager navigation, CrestApiLocalizer apiLocalizer, IJSRuntime js, ICrestCultureCookieWriter cultureCookieWriter) : IAsyncDisposable
{
    private readonly Lazy<IReadOnlyDictionary<string, Type>> _shapeBindings = new(BuildShapeBindings);
    private readonly SemaphoreSlim _manifestLock = new(1, 1);
    private CancellationTokenSource? _permissionRefreshCancellation;
    private HubConnection? _permissionHub;

    public event Action? Changed;

    public AuthUser User { get; private set; } = AuthUser.Anonymous;
    public CrestThemeSettings Theme { get; private set; } = CrestThemeSettings.Default;
    public AppManifest? Manifest { get; private set; }
    public SiteSettings? Site { get; private set; }
    public DisplayMenu? AdminMenu { get; private set; }
    public DisplayMenu? ProfileMenu { get; private set; }
    public string? ResolvedCulture { get; private set; }
    public ContentType[] ContentTypes { get; private set; } = [];
    public Role[] Roles { get; private set; } = [];
    public ContentItem? CurrentContentItem { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsBusy { get; private set; }
    public string? ErrorMessage { get; private set; }

    public bool IsAuthenticated => User.IsAuthenticated;

    // This is a navigation convenience only. The server independently
    // authorizes direct route requests and every Crest/Orchard data operation.
    // AuthorizedRoutes' templates are canonical ("/Features", "/Themes", ... -
    // matching this app's own @page directives, and matching the real, MVC-resolved
    // shape of stock Orchard admin URLs once AdminUrlPrefix is substituted in) and
    // Blazor's Router itself resolves @page routes relative to BaseUri (see
    // BlazorAdminThemeMiddleware.TryServeIndexHtmlAsync), so a base-relative path IS
    // already the canonical path minus its leading slash (e.g. "/backoffice/Features"
    // -> ToBaseRelativePath -> "Features", which is exactly what "@page "/Features""
    // resolves to under that base) - just needs the slash back. See the same fix in
    // BlazorAdminThemeMiddleware's server-side CanAccessAsync call.
    public bool IsRouteAuthorized(string uri)
    {
        var path = "/" + navigation.ToBaseRelativePath(uri).TrimStart('/');
        return Manifest?.AuthorizedRoutes?.Any(route => RouteMatches(route.Template, path)) == true;
    }

    public Shape NewShape(string type, Action<Shape>? configure = null)
    {
        var shape = new Shape(type);
        configure?.Invoke(shape);
        return shape;
    }

    public RenderFragment RenderShape(Shape shape) =>
        builder => RenderShape(builder, shape);

    public async Task EnsureInitializedAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        await RunAsync(async () =>
        {
            Theme = await api.Crest.Rest.Theme.GetAsync();
            await themeEngine.ApplyAsync(Theme);
            User = await api.Crest.Rest.Auth.MeAsync();

            if (User.IsAuthenticated)
            {
                await LoadAdminStateAsync();
            }

            ErrorMessage = null;
            IsInitialized = true;
            return true;
        });
    }

    public async Task<bool> LoginAsync(string userName, string password, bool rememberMe)
    {
        return await RunAsync(async () =>
        {
            var user = await api.Crest.Rest.Auth.LoginAsync(new(userName, password, rememberMe));
            if (user is null)
            {
                User = AuthUser.Anonymous;
                ErrorMessage = "Login failed";
                return false;
            }

            User = user;
            await LoadAdminStateAsync();
            ErrorMessage = null;
            IsInitialized = true;
            return true;
        });
    }

    public async Task LogoutAsync()
    {
        await RunAsync(async () =>
        {
            User = await api.Crest.Rest.Auth.LogoutAsync();
            await StopPermissionRefreshAsync();
            ClearAdminState();
            ErrorMessage = null;
            IsInitialized = true;
            return true;
        });
    }

    public async Task RefreshAdminStateAsync()
    {
        await RunAsync(async () =>
        {
            await LoadAdminStateAsync();
            ErrorMessage = null;
            return true;
        });
    }

    public async Task RefreshAdminMenuAsync()
    {
        await RunAsync(async () =>
        {
            var menu = await api.Crest.Rest.Navigation.GetAdminMenuAsync();
            iconRegistry.Register(menu.Icons);
            if (Manifest is not null)
            {
                Manifest = Manifest with { AdminMenu = menu };
            }

            AdminMenu = ToDisplayMenu(menu);
            ErrorMessage = null;
            return true;
        });
    }

    public async Task RefreshProfileMenuAsync()
    {
        await RunAsync(async () =>
        {
            var menu = await api.Crest.Rest.Navigation.GetProfileMenuAsync();
            iconRegistry.Register(menu.Icons);
            if (Manifest is not null)
            {
                Manifest = Manifest with { ProfileMenu = menu };
            }

            ProfileMenu = ToDisplayMenu(menu);
            ErrorMessage = null;
            return true;
        });
    }

    public async Task<CrestThemeSettings?> UpdateThemeAsync(CrestThemeSettings settings)
    {
        return await RunAsync(async () =>
        {
            var saved = await api.Crest.Rest.Theme.UpdateAsync(settings);
            if (saved is not null)
            {
                Theme = saved;
                await themeEngine.ApplyAsync(saved);
            }

            ErrorMessage = saved is null ? "Unable to save design system settings." : null;
            return saved;
        });
    }

    public async Task<SiteSettings?> UpdateSiteAsync(SiteSettingsUpdate update)
    {
        return await RunAsync(async () =>
        {
            var saved = await api.Crest.Rest.Site.UpdateAsync(update);
            if (saved is not null)
            {
                Site = saved;
                if (Manifest is not null)
                {
                    Manifest = Manifest with { Site = saved };
                }
            }

            ErrorMessage = saved is null ? "Unable to save site settings." : null;
            return saved;
        });
    }

    public async Task<ContentItem?> LoadContentItemByHandleAsync(string handle)
    {
        return await RunAsync(async () =>
        {
            CurrentContentItem = await api.Crest.Rest.Content.Items.GetByHandleAsync(handle);
            ErrorMessage = CurrentContentItem is null ? "Content item not found." : null;
            return CurrentContentItem;
        });
    }

    public void ClearError()
    {
        ErrorMessage = null;
        NotifyChanged();
    }

    private async Task LoadAdminStateAsync()
    {
        await RefreshManifestAsync();
        ContentTypes = await api.Crest.Rest.Content.Types.ListAsync();
        Roles = await api.Crest.Rest.Roles.ListAsync();
        await StartPermissionRefreshAsync();
    }

    private async Task RefreshManifestAsync()
    {
        await _manifestLock.WaitAsync();
        try
        {
            Manifest = await api.Crest.Rest.App.GetManifestAsync();
            iconRegistry.Register(Manifest?.AdminMenu.Icons);
            Site = Manifest?.Site ?? await api.Crest.Rest.Site.GetAsync();
            AdminMenu = ToDisplayMenu(Manifest?.AdminMenu ?? await api.Crest.Rest.Navigation.GetAdminMenuAsync());
            ProfileMenu = ToDisplayMenu(Manifest?.ProfileMenu ?? await api.Crest.Rest.Navigation.GetProfileMenuAsync());

            var cultureSelector = Manifest?.CultureSelector;
            if (cultureSelector is not null)
            {
                var resolved = await ResolveCultureAsync(cultureSelector);
                ResolvedCulture = resolved;
                var resolvedCultureInfo = System.Globalization.CultureInfo.GetCultureInfo(resolved);
                await apiLocalizer.LoadAsync(resolvedCultureInfo);

                // Every date/number ToString()/format-string call in the WASM app that relies
                // on the ambient culture (CrestDataGridColumn, CrestScheduler's calendar views,
                // ContentItems.razor's "Modified" timestamp, etc.) reads CultureInfo.CurrentCulture
                // - it is never set anywhere else in this process, so without this it silently
                // stays whatever the browser initialized the runtime with (navigator.language at
                // startup), not the culture DisplayManager just resolved. Set on the current
                // thread AND as the process default, since Blazor WASM is single-threaded but
                // async continuations aren't guaranteed to stay on the exact thread that started
                // them.
                System.Globalization.CultureInfo.CurrentCulture = resolvedCultureInfo;
                System.Globalization.CultureInfo.CurrentUICulture = resolvedCultureInfo;
                System.Globalization.CultureInfo.DefaultThreadCurrentCulture = resolvedCultureInfo;
                System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = resolvedCultureInfo;

                // Write the fully-resolved value on every refresh, not just when a cookie is
                // missing - the client is the sole source of truth for this decision (see
                // plans/user-localization.md's "Resolution architecture" section), so the
                // server-side cookie must always reflect exactly what was just resolved here,
                // never something left over from a previous, possibly-stale resolution.
                // Prerender-safe: cookie writeback is browser-only; the interactive phase
                // re-runs the manifest refresh and writes it for real. Server-side the
                // cookie is read (RequestLocalizationOptions), never written.
                await js.TryInvokeVoidAsync("crestTheme.setAdminCulture", cultureSelector.CookieName, cultureSelector.CookiePath, resolved);

                // Also hand the resolved-culture inputs to CrestAntiforgeryHandler so it can
                // independently re-resolve and rewrite the cookie immediately before every
                // subsequent outgoing request, not only here on manifest refresh - see
                // plans/user-localization.md phase 15 and CrestAntiforgeryHandler.RewriteCultureCookie.
                cultureCookieWriter.SetCultureCookieContext(new CultureCookieContext(User.UserName, cultureSelector, IsUnderAdminPath()));
            }
        }
        finally
        {
            _manifestLock.Release();
        }
    }

    // Client-side priority chain (plans/user-localization.md's "Resolution architecture"):
    // 1. session override (explicit titlebar pick, this browser only, never persisted)
    // 2. user's stored default (UserLocalizationSettings.Culture, via the manifest)
    // 3. admin default culture, only consulted when the current route is under the admin
    //    path prefix (CrestLocalizationSettings.AdminDefaultCulture - a Crest-owned tenant
    //    setting, distinct from the general tenant default)
    // 4. browser locale (navigator.language), only if the tenant actually supports it
    // 5. tenant default culture
    // Every candidate is validated against the tenant's supported-culture list before
    // being accepted, so a stale/foreign value from any source can never "win" and get
    // written back into the cookie.
    private async Task<string> ResolveCultureAsync(CultureSelector cultureSelector)
    {
        // Keyed by user name (sessionStorage, per-tab) - see crest.theme.js's
        // setSessionCultureOverride/getSessionCultureOverride and plans/user-localization.md's
        // "Per-tab and per-user override scoping" section. Switching signed-in identity in
        // this tab looks up that identity's own override, never carries the previous user's.
        // Prerender-safe: sessionStorage/navigator are browser-only; during prerender both
        // return null and ResolveCulture falls through to the manifest/tenant chain -
        // matching what the server itself resolved via the culture cookie pipeline.
        var sessionOverride = await js.TryInvokeAsync<string?>("crestTheme.getSessionCultureOverride", null, User.UserName);
        var browserLocale = await js.TryInvokeAsync<string?>("crestTheme.getBrowserLocale");

        return ResolveCulture(cultureSelector, sessionOverride, browserLocale, IsUnderAdminPath());
    }

    // Pure priority-chain logic (plans/user-localization.md's "Resolution architecture"),
    // isolated from JS interop/NavigationManager so it's directly unit-testable:
    // 1. session override, 2. user's stored default, 3. admin default culture (admin-path
    // only), 4. browser locale, 5. tenant default. Every candidate must be one of the
    // tenant's supported cultures or it's skipped, never allowed to "win".
    internal static string ResolveCulture(CultureSelector cultureSelector, string? sessionOverride, string? browserLocale, bool isUnderAdminPath)
    {
        var supported = new HashSet<string>(cultureSelector.Cultures.Select(culture => culture.Value), StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(sessionOverride) && supported.Contains(sessionOverride))
        {
            return sessionOverride;
        }

        if (!string.IsNullOrWhiteSpace(cultureSelector.UserDefaultCulture) && supported.Contains(cultureSelector.UserDefaultCulture))
        {
            return cultureSelector.UserDefaultCulture;
        }

        if (!string.IsNullOrWhiteSpace(cultureSelector.AdminDefaultCulture) && supported.Contains(cultureSelector.AdminDefaultCulture) && isUnderAdminPath)
        {
            return cultureSelector.AdminDefaultCulture;
        }

        if (!string.IsNullOrWhiteSpace(browserLocale) && supported.Contains(browserLocale))
        {
            return browserLocale;
        }

        return cultureSelector.TenantDefaultCulture;
    }

    private bool IsUnderAdminPath() => IsUnderAdminPath(Manifest?.Admin.BasePath, navigation.Uri);

    internal static bool IsUnderAdminPath(string? basePath, string uri)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return false;
        }

        var path = new Uri(uri).AbsolutePath;
        return path.Equals(basePath, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task StartPermissionRefreshAsync()
    {
        if (_permissionRefreshCancellation is not null)
        {
            return;
        }

        _permissionRefreshCancellation = new CancellationTokenSource();
        var cancellationToken = _permissionRefreshCancellation.Token;
        // Api endpoints live at the origin root regardless of which shell base
        // (AdminPath/LoginPath) is currently loaded - combining against
        // navigation.BaseUri directly would nest this under that base instead (e.g.
        // "/backoffice/api/crest/permissions"), same class of bug CrestAntiforgeryHandler
        // had before it was given an explicit origin-root BaseAddress.
        var origin = new Uri(navigation.BaseUri).GetLeftPart(UriPartial.Authority) + "/";
        _permissionHub = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(origin), "api/crest/permissions"))
            .WithAutomaticReconnect()
            .Build();
        _permissionHub.On("permissionsInvalidated", async () => await RefreshAfterPermissionChangeAsync());

        try
        {
            await _permissionHub.StartAsync(cancellationToken);
        }
        catch
        {
            // The periodic refresh remains the reliable fallback.
        }

        _ = RefreshManifestPeriodicallyAsync(cancellationToken);
    }

    private async Task RefreshManifestPeriodicallyAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshAfterPermissionChangeAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshAfterPermissionChangeAsync()
    {
        if (!IsAuthenticated)
        {
            return;
        }

        try
        {
            await RefreshManifestAsync();
            ErrorMessage = null;
            if (!IsRouteAuthorized(navigation.Uri))
            {
                // Do not disrupt a permitted page. If a role or permission
                // change removed the current page, reload it so the server
                // supplies its authoritative 403 response.
                navigation.NavigateTo(navigation.Uri, forceLoad: true);
                return;
            }
        }
        catch
        {
            // Preserve the last known UI state. Orchard remains authoritative
            // for every subsequent server request.
        }
        finally
        {
            NotifyChanged();
        }
    }

    // Phase 8: DI-scope-tied cleanup. Under the Blazor Web App model this scoped
    // service is constructed per prerender request / per circuit / per WASM app - the
    // permission hub connection and the 15-minute refresh timer must die with their
    // scope, or every authenticated admin-page prerender leaks both until process end.
    public async ValueTask DisposeAsync() => await StopPermissionRefreshAsync();

    private async Task StopPermissionRefreshAsync()
    {
        var cancellation = Interlocked.Exchange(ref _permissionRefreshCancellation, null);
        if (cancellation is not null)
        {
            await cancellation.CancelAsync();
            cancellation.Dispose();
        }

        if (_permissionHub is not null)
        {
            await _permissionHub.DisposeAsync();
            _permissionHub = null;
        }
    }

    private void ClearAdminState()
    {
        Manifest = null;
        Site = null;
        AdminMenu = null;
        ProfileMenu = null;
        ResolvedCulture = null;
        ContentTypes = [];
        Roles = [];
        CurrentContentItem = null;
        cultureCookieWriter.SetCultureCookieContext(null);
    }

    private static DisplayMenu ToDisplayMenu(NavigationMenu menu) => new(
        menu.Name,
        menu.Items.Where(item => !string.IsNullOrWhiteSpace(item.Key)).Select(ToDisplayMenuItem).ToArray(),
        (menu.Separators ?? []).Select(ToDisplayMenuSeparator).ToArray(),
        ToDisplayPrimaryNavMenuSettings(menu.PrimaryNavMenuSettings));

    private static DisplayPrimaryNavMenuSettings ToDisplayPrimaryNavMenuSettings(AdminPrimaryNavMenuSettings? settings) => settings is null
        ? DisplayPrimaryNavMenuSettings.Default
        : new DisplayPrimaryNavMenuSettings
        {
            Collapsible = settings.Collapsible,
            ExpansionDurationMilliseconds = Math.Clamp(settings.ExpansionDurationMilliseconds, 100, 2000),
            TierIndents = NormalizeStrings(settings.TierIndents, DisplayPrimaryNavMenuSettings.Default.TierIndents, 4),
            TierBackgrounds = NormalizeStrings(settings.TierBackgrounds, DisplayPrimaryNavMenuSettings.Default.TierBackgrounds, 4),
            TierSeparators = NormalizeBools(settings.TierSeparators, DisplayPrimaryNavMenuSettings.Default.TierSeparators, 3),
            TierBaseSizes = settings.TierBaseSizes is { Count: > 0 }
                ? NormalizeStrings(settings.TierBaseSizes, DisplayPrimaryNavMenuSettings.Default.TierBaseSizes, 3)
                : NormalizeDoubles(settings.TierBaseRems, [1.0, 0.95, 0.9], 3)
                    .Select(value => $"{Math.Clamp(value, 0.5, 2.0):0.###}rem")
                    .ToArray(),
            CollapseIconPosition = (Crest.Admin.DisplayManagement.PrimaryNavMenuCollapseIconPosition)(int)settings.CollapseIconPosition,
        };

    private static DisplayMenuSeparator ToDisplayMenuSeparator(NavigationSeparator separator) => new(
        separator.Key,
        separator.ParentKey,
        separator.Order);

    private static string[] NormalizeStrings(IReadOnlyList<string>? values, IReadOnlyList<string> defaults, int length)
    {
        var result = new string[length];
        for (var index = 0; index < length; index++)
        {
            var value = values is not null && index < values.Count ? values[index] : defaults[Math.Min(index, defaults.Count - 1)];
            result[index] = string.IsNullOrWhiteSpace(value) ? defaults[Math.Min(index, defaults.Count - 1)] : value;
        }

        return result;
    }

    private static bool[] NormalizeBools(IReadOnlyList<bool>? values, IReadOnlyList<bool> defaults, int length)
    {
        var result = new bool[length];
        for (var index = 0; index < length; index++)
        {
            result[index] = values is not null && index < values.Count ? values[index] : defaults[Math.Min(index, defaults.Count - 1)];
        }

        return result;
    }

    private static double[] NormalizeDoubles(IReadOnlyList<double>? values, IReadOnlyList<double> defaults, int length)
    {
        var result = new double[length];
        for (var index = 0; index < length; index++)
        {
            result[index] = values is not null && index < values.Count ? values[index] : defaults[Math.Min(index, defaults.Count - 1)];
        }

        return result;
    }

    private static DisplayMenuItem ToDisplayMenuItem(NavigationItem item) => new(
        item.Text,
        item.Key!,
        item.Id,
        item.Href,
        item.Url,
        item.Target,
        item.Position,
        ToDisplayIcon(item.Icon),
        item.Classes,
        item.Items.Where(child => !string.IsNullOrWhiteSpace(child.Key)).Select(ToDisplayMenuItem).ToArray());

    private static DisplayIcon? ToDisplayIcon(NavigationIcon? icon) => icon is null
        ? null
        : new DisplayIcon(icon.Key, icon.Library, icon.Version, icon.Style, icon.Name, icon.SvgMarkup);

    private static bool RouteMatches(string template, string path)
    {
        var templateSegments = template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (templateSegments.Length != pathSegments.Length)
        {
            return false;
        }

        for (var index = 0; index < templateSegments.Length; index++)
        {
            var segment = templateSegments[index];
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                continue;
            }

            if (!string.Equals(segment, pathSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        IsBusy = true;
        NotifyChanged();

        try
        {
            return await action();
        }
        catch
        {
            ErrorMessage = "Something went wrong";
            throw;
        }
        finally
        {
            IsBusy = false;
            NotifyChanged();
        }
    }

    private void NotifyChanged() => Changed?.Invoke();

    private void RenderShape(RenderTreeBuilder builder, Shape shape)
    {
        var componentType = ResolveComponentType(shape);
        var sequence = 0;

        builder.OpenComponent(sequence++, componentType);

        if (typeof(ShapeTemplate).IsAssignableFrom(componentType))
        {
            builder.AddAttribute(sequence++, nameof(ShapeTemplate.Model), shape);
        }

        foreach (var property in shape.Properties)
        {
            var componentProperty = componentType.GetProperty(property.Key);
            if (componentProperty?.GetCustomAttribute<ParameterAttribute>() is not null)
            {
                builder.AddAttribute(sequence++, property.Key, property.Value);
            }
        }

        builder.CloseComponent();
    }

    private Type ResolveComponentType(Shape shape)
    {
        foreach (var shapeType in shape.Metadata.Alternates.Reverse().Append(shape.Metadata.Type))
        {
            if (_shapeBindings.Value.TryGetValue(shapeType, out var componentType))
            {
                return componentType;
            }
        }

        throw new InvalidOperationException($"No component binding found for shape '{shape.Metadata.Type}'.");
    }

    private static IReadOnlyDictionary<string, Type> BuildShapeBindings()
    {
        var componentTypes = typeof(DisplayManager).Assembly
            .GetExportedTypes()
            .Where(type => !type.IsAbstract && typeof(IComponent).IsAssignableFrom(type));

        var bindings = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var componentType in componentTypes)
        {
            bindings[componentType.Name] = componentType;

            foreach (var attribute in componentType.GetCustomAttributes<ShapeAttribute>())
            {
                bindings[attribute.ShapeType] = componentType;
            }
        }

        return bindings;
    }
}
