using Crest.Admin.Api;
using Crest.Admin.Theme;
using Crest.Icons;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.SignalR.Client;
using System.Reflection;

namespace Crest.Admin.DisplayManagement;

public sealed class DisplayManager(IApi api, CrestThemeEngine themeEngine, ClientIconRegistry iconRegistry, NavigationManager navigation)
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
    public ContentType[] ContentTypes { get; private set; } = [];
    public Role[] Roles { get; private set; } = [];
    public ContentItem? CurrentContentItem { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsBusy { get; private set; }
    public string? ErrorMessage { get; private set; }

    public bool IsAuthenticated => User.IsAuthenticated;

    // This is a navigation convenience only. The server independently
    // authorizes direct route requests and every Crest/Orchard data operation.
    public bool IsRouteAuthorized(string uri)
    {
        var path = new Uri(uri).AbsolutePath;
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
        }
        finally
        {
            _manifestLock.Release();
        }
    }

    private async Task StartPermissionRefreshAsync()
    {
        if (_permissionRefreshCancellation is not null)
        {
            return;
        }

        _permissionRefreshCancellation = new CancellationTokenSource();
        var cancellationToken = _permissionRefreshCancellation.Token;
        _permissionHub = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(navigation.BaseUri), "api/crest/permissions"))
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
        ContentTypes = [];
        Roles = [];
        CurrentContentItem = null;
    }

    private static DisplayMenu ToDisplayMenu(NavigationMenu menu) => new(
        menu.Name,
        menu.Items.Select(ToDisplayMenuItem).ToArray(),
        (menu.Separators ?? []).Select(ToDisplayMenuSeparator).ToArray(),
        ToDisplaySidebarSettings(menu.SidebarSettings));

    private static DisplaySidebarSettings ToDisplaySidebarSettings(AdminSidebarSettings? settings) => settings is null
        ? DisplaySidebarSettings.Default
        : new DisplaySidebarSettings
        {
            Collapsible = settings.Collapsible,
            ExpansionDurationMilliseconds = Math.Clamp(settings.ExpansionDurationMilliseconds, 100, 2000),
            TierIndents = NormalizeStrings(settings.TierIndents, DisplaySidebarSettings.Default.TierIndents, 4),
            TierBackgrounds = NormalizeStrings(settings.TierBackgrounds, DisplaySidebarSettings.Default.TierBackgrounds, 4),
            TierSeparators = NormalizeBools(settings.TierSeparators, DisplaySidebarSettings.Default.TierSeparators, 3),
            TierBaseSizes = settings.TierBaseSizes is { Count: > 0 }
                ? NormalizeStrings(settings.TierBaseSizes, DisplaySidebarSettings.Default.TierBaseSizes, 3)
                : NormalizeDoubles(settings.TierBaseRems, [1.0, 0.95, 0.9], 3)
                    .Select(value => $"{Math.Clamp(value, 0.5, 2.0):0.###}rem")
                    .ToArray(),
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
        item.Key,
        item.Id,
        item.Href,
        item.Url,
        item.Target,
        item.Position,
        ToDisplayIcon(item.Icon),
        item.Classes,
        item.Items.Select(ToDisplayMenuItem).ToArray());

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
