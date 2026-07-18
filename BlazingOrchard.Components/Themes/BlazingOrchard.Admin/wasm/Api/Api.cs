using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace BlazingOrchard.Admin.Api;

public interface IApi
{
    IBlazingArea Blazing { get; }
}

public interface IBlazingArea
{
    IRestApi Rest { get; }
}

public interface IRestApi
{
    IAuthApi Auth { get; }
    IAppApi App { get; }
    ISiteApi Site { get; }
    INavigationApi Navigation { get; }
    IContentApi Content { get; }
    IFeaturesApi Features { get; }
    IRolesApi Roles { get; }
    IThemeApi Theme { get; }
    IThemesApi Themes { get; }
    IAdminMenusApi AdminMenus { get; }
    IIconsApi Icons { get; }
}

public interface IAppApi
{
    Task<AppManifest?> GetManifestAsync();
}

public interface ISiteApi
{
    Task<SiteSettings> GetAsync();
    Task<SiteSettings?> UpdateAsync(SiteSettingsUpdate update);
}

public interface INavigationApi
{
    Task<NavigationMenu> GetAdminMenuAsync();
    Task<NavigationMenu> GetMenuAsync(string menuName);
}

public interface IContentApi
{
    IContentTypesApi Types { get; }
    IContentItemsApi Items { get; }
}

public interface IContentTypesApi
{
    Task<ContentType[]> ListAsync();
    Task<ContentType?> GetAsync(string contentType);
}

public interface IContentItemsApi
{
    Task<ContentItem?> GetByHandleAsync(string handle);
}

public interface IFeaturesApi
{
    Task<Feature[]> ListAsync();
}

public interface IRolesApi
{
    Task<Role[]> ListAsync();
}

public interface IAuthApi
{
    Task<AuthUser> MeAsync();
    Task<AuthUser?> LoginAsync(LoginModel model);
    Task<AuthUser> LogoutAsync();
}

public interface IThemeApi
{
    Task<BlazingThemeSettings> GetAsync();
    Task<BlazingThemeSettings?> UpdateAsync(BlazingThemeSettings settings);
}

public interface IThemesApi
{
    Task<ThemesState> ListAsync();
    Task<bool> SetCurrentAsync(string id);
    Task<bool> EnableAsync(string id);
    Task<bool> DisableAsync(string id);
    Task<bool> ResetSiteThemeAsync();
    Task<bool> ResetAdminThemeAsync();
}

public interface IIconsApi
{
    Task<IconSearchResult> SearchAsync(string? library = null, string? query = null, int skip = 0, int take = 200);
}

public interface IAdminMenusApi
{
    Task<AdminMenusState> ListAsync();
    Task<AdminMenuSummary?> GetAsync(string menuId);
    Task<AdminMenuSummary?> CreateMenuAsync(AdminMenuEditModel model);
    Task<AdminMenuSummary?> RenameMenuAsync(string menuId, AdminMenuEditModel model);
    Task<AdminMenuSummary?> ToggleMenuAsync(string menuId);
    Task<AdminMenuSummary?> DuplicateMenuAsync(string menuId);
    Task<bool> DeleteMenuAsync(string menuId);
    Task<AdminMenuSummary?> CreateNodeAsync(string menuId, AdminMenuNodeEditModel model);
    Task<AdminMenuSummary?> UpdateNodeAsync(string menuId, string nodeId, AdminMenuNodeEditModel model);
    Task<AdminMenuSummary?> RenameNodeAsync(string menuId, string nodeId, AdminMenuNodeRenameModel model);
    Task<AdminMenuSummary?> MoveNodeAsync(string menuId, string nodeId, AdminMenuNodeMoveModel model);
    Task<AdminMenuSummary?> ToggleNodeAsync(string menuId, string nodeId);
    Task<AdminMenuSummary?> DeleteNodeAsync(string menuId, string nodeId);
}

public sealed class Api(HttpClient http) : IApi
{
    public IBlazingArea Blazing { get; } = new BlazingArea(http);
}

public sealed class BlazingArea(HttpClient http) : IBlazingArea
{
    public IRestApi Rest { get; } = new RestApi(http);
}

public sealed class RestApi(HttpClient http) : IRestApi
{
    public IAuthApi Auth { get; } = new AuthApi(http);
    public IAppApi App { get; } = new AppApi(http);
    public ISiteApi Site { get; } = new SiteApi(http);
    public INavigationApi Navigation { get; } = new NavigationApi(http);
    public IContentApi Content { get; } = new ContentApi(http);
    public IFeaturesApi Features { get; } = new FeaturesApi(http);
    public IRolesApi Roles { get; } = new RolesApi(http);
    public IThemeApi Theme { get; } = new ThemeApi(http);
    public IThemesApi Themes { get; } = new ThemesApi(http);
    public IAdminMenusApi AdminMenus { get; } = new AdminMenusApi(http);
    public IIconsApi Icons { get; } = new IconsApi(http);
}

public sealed class AuthApi(HttpClient http) : IAuthApi
{
    public async Task<AuthUser> MeAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/blazing/auth/me")));
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength == 0)
        {
            return AuthUser.Anonymous;
        }

        return await response.Content.ReadFromJsonAsync<AuthUser>() ?? AuthUser.Anonymous;
    }

    public async Task<AuthUser?> LoginAsync(LoginModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, "api/blazing/auth/login")
        {
            Content = JsonContent.Create(model),
        }));

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AuthUser>()
            : null;
    }

    public async Task<AuthUser> LogoutAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, "api/blazing/auth/logout")));
        return await response.Content.ReadFromJsonAsync<AuthUser>() ?? AuthUser.Anonymous;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class AppApi(HttpClient http) : IAppApi
{
    public async Task<AppManifest?> GetManifestAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/blazing/app/manifest")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AppManifest>()
            : null;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class SiteApi(HttpClient http) : ISiteApi
{
    public async Task<SiteSettings> GetAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/blazing/site")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SiteSettings>() ?? SiteSettings.Default
            : SiteSettings.Default;
    }

    public async Task<SiteSettings?> UpdateAsync(SiteSettingsUpdate update)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Put, "api/blazing/site")
        {
            Content = JsonContent.Create(update),
        }));

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SiteSettings>()
            : null;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class NavigationApi(HttpClient http) : INavigationApi
{
    public async Task<NavigationMenu> GetAdminMenuAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/blazing/navigation/admin")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<NavigationMenu>() ?? NavigationMenu.Empty("admin")
            : NavigationMenu.Empty("admin");
    }

    public async Task<NavigationMenu> GetMenuAsync(string menuName)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, $"api/blazing/navigation/menus/{Uri.EscapeDataString(menuName)}")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<NavigationMenu>() ?? NavigationMenu.Empty(menuName)
            : NavigationMenu.Empty(menuName);
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class ContentApi(HttpClient http) : IContentApi
{
    public IContentTypesApi Types { get; } = new ContentTypesApi(http);
    public IContentItemsApi Items { get; } = new ContentItemsApi(http);
}

public sealed class ContentTypesApi(HttpClient http) : IContentTypesApi
{
    public async Task<ContentType[]> ListAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/blazing/content-types")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ContentType[]>() ?? []
            : [];
    }

    public async Task<ContentType?> GetAsync(string contentType)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, $"api/blazing/content-types/{Uri.EscapeDataString(contentType)}")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ContentType>()
            : null;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class ContentItemsApi(HttpClient http) : IContentItemsApi
{
    public async Task<ContentItem?> GetByHandleAsync(string handle)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, $"api/blazing/content-items/by-handle/{Uri.EscapeDataString(handle)}")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ContentItem>()
            : null;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class FeaturesApi(HttpClient http) : IFeaturesApi
{
    public async Task<Feature[]> ListAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/blazing/features")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Feature[]>() ?? []
            : [];
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class RolesApi(HttpClient http) : IRolesApi
{
    public async Task<Role[]> ListAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/blazing/roles")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Role[]>() ?? []
            : [];
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class ThemeApi(HttpClient http) : IThemeApi
{
    public async Task<BlazingThemeSettings> GetAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/blazing/theme")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<BlazingThemeSettings>() ?? BlazingThemeSettings.Default
            : BlazingThemeSettings.Default;
    }

    public async Task<BlazingThemeSettings?> UpdateAsync(BlazingThemeSettings settings)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Put, "api/blazing/theme")
        {
            Content = JsonContent.Create(settings),
        }));

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<BlazingThemeSettings>() : null;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class ThemesApi(HttpClient http) : IThemesApi
{
    public async Task<ThemesState> ListAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/blazing/themes")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ThemesState>() ?? ThemesState.Empty
            : ThemesState.Empty;
    }

    public Task<bool> SetCurrentAsync(string id) => PostAsync($"api/blazing/themes/{Uri.EscapeDataString(id)}/current");

    public Task<bool> EnableAsync(string id) => PostAsync($"api/blazing/themes/{Uri.EscapeDataString(id)}/enable");

    public Task<bool> DisableAsync(string id) => PostAsync($"api/blazing/themes/{Uri.EscapeDataString(id)}/disable");

    public Task<bool> ResetSiteThemeAsync() => PostAsync("api/blazing/themes/reset-site");

    public Task<bool> ResetAdminThemeAsync() => PostAsync("api/blazing/themes/reset-admin");

    private async Task<bool> PostAsync(string uri)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, uri)));
        return response.IsSuccessStatusCode;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class IconsApi(HttpClient http) : IIconsApi
{
    public async Task<IconSearchResult> SearchAsync(string? library = null, string? query = null, int skip = 0, int take = 200)
    {
        var url = new StringBuilder("api/blazing/icons?")
            .Append("skip=").Append(skip)
            .Append("&take=").Append(take);

        if (!string.IsNullOrWhiteSpace(library))
        {
            url.Append("&library=").Append(Uri.EscapeDataString(library));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            url.Append("&query=").Append(Uri.EscapeDataString(query));
        }

        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, url.ToString())));
        if (!response.IsSuccessStatusCode)
        {
            return IconSearchResult.Empty;
        }

        return await response.Content.ReadFromJsonAsync<IconSearchResult>() ?? IconSearchResult.Empty;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class AdminMenusApi(HttpClient http) : IAdminMenusApi
{
    public async Task<AdminMenusState> ListAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/blazing/admin-menus")));
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Admin menus API returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        return await response.Content.ReadFromJsonAsync<AdminMenusState>() ?? AdminMenusState.Empty;
    }

    public async Task<AdminMenuSummary?> GetAsync(string menuId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, $"api/blazing/admin-menus/{Uri.EscapeDataString(menuId)}")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> CreateMenuAsync(AdminMenuEditModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, "api/blazing/admin-menus")
        {
            Content = JsonContent.Create(model),
        }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> RenameMenuAsync(string menuId, AdminMenuEditModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/blazing/admin-menus/{Uri.EscapeDataString(menuId)}/rename")
        {
            Content = JsonContent.Create(model),
        }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> ToggleMenuAsync(string menuId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/blazing/admin-menus/{Uri.EscapeDataString(menuId)}/toggle")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> DuplicateMenuAsync(string menuId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/blazing/admin-menus/{Uri.EscapeDataString(menuId)}/duplicate")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<bool> DeleteMenuAsync(string menuId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Delete, $"api/blazing/admin-menus/{Uri.EscapeDataString(menuId)}")));
        return response.IsSuccessStatusCode;
    }

    public Task<AdminMenuSummary?> CreateNodeAsync(string menuId, AdminMenuNodeEditModel model) =>
        SendNodeAsync(HttpMethod.Post, $"api/blazing/admin-menus/{Uri.EscapeDataString(menuId)}/nodes", model);

    public Task<AdminMenuSummary?> UpdateNodeAsync(string menuId, string nodeId, AdminMenuNodeEditModel model) =>
        SendNodeAsync(HttpMethod.Put, $"api/blazing/admin-menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}", model);

    public Task<AdminMenuSummary?> RenameNodeAsync(string menuId, string nodeId, AdminMenuNodeRenameModel model) =>
        SendNodeAsync(HttpMethod.Post, $"api/blazing/admin-menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}/rename", model);

    public Task<AdminMenuSummary?> MoveNodeAsync(string menuId, string nodeId, AdminMenuNodeMoveModel model) =>
        SendNodeAsync(HttpMethod.Post, $"api/blazing/admin-menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}/move", model);

    public Task<AdminMenuSummary?> ToggleNodeAsync(string menuId, string nodeId) =>
        SendNodeAsync(HttpMethod.Post, $"api/blazing/admin-menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}/toggle", null);

    public async Task<AdminMenuSummary?> DeleteNodeAsync(string menuId, string nodeId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Delete, $"api/blazing/admin-menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    private async Task<AdminMenuSummary?> SendNodeAsync(HttpMethod method, string uri, object? model)
    {
        using var request = WithCredentials(new(method, uri));
        if (model is not null)
        {
            request.Content = JsonContent.Create(model);
        }

        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed record LoginModel(string UserName, string Password, bool RememberMe);

public sealed record AuthUser(bool IsAuthenticated, string? UserName, string[] Roles)
{
    public static AuthUser Anonymous { get; } = new(false, null, []);
}

public sealed record AppManifest(
    Tenant Tenant,
    Tenant[] Tenants,
    SiteSettings Site,
    AdminSettingsDto AdminSettings,
    AdminDescriptor Admin,
    int FeatureSerialNumber,
    string FeatureHash,
    Feature[] Features,
    NavigationMenu AdminMenu);

public sealed record Tenant(
    string Name,
    string TenantId,
    string State,
    string? RequestUrlHost,
    string[] RequestUrlHosts,
    string? RequestUrlPrefix);

public sealed record AdminDescriptor(string BasePath);

public sealed record AdminSettingsDto(
    bool DisplayThemeToggler,
    bool DisplayMenuFilter,
    bool DisplayNewMenu,
    bool DisplayTitlesInTopbar);

public sealed record SiteSettings(
    string SiteName,
    string PageTitleFormat,
    string BaseUrl,
    string TimeZoneId,
    string Calendar,
    int PageSize,
    int MaxPageSize,
    int MaxPagedCount)
{
    public static SiteSettings Default { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        10,
        100,
        0);
}

public sealed record SiteSettingsUpdate(
    string SiteName,
    string PageTitleFormat,
    string BaseUrl,
    string TimeZoneId,
    string Calendar,
    int PageSize,
    int MaxPageSize,
    int MaxPagedCount);

public sealed record NavigationMenu(string Name, NavigationItem[] Items)
{
    public static NavigationMenu Empty(string name) => new(name, []);
}

public sealed record NavigationIcon(string Library, string? Version, string Name, string? SvgMarkup);

public sealed record NavigationItem(
    string Text,
    string? Id,
    string? Href,
    string? Url,
    string? Target,
    string? Position,
    NavigationIcon? Icon,
    string[] Classes,
    NavigationItem[] Items)
{
    public string Key => !string.IsNullOrWhiteSpace(Id) ? Id : StableKey(Text, Link);
    public string? Link => !string.IsNullOrWhiteSpace(Href) ? Href : Url;

    private static string StableKey(string text, string? link)
    {
        var input = $"{text}|{link}";
        return "nav-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}

public sealed record ContentType(
    string Name,
    string DisplayName,
    JsonObject Settings,
    ContentTypePart[] Parts);

public sealed record ContentTypePart(
    string Name,
    JsonObject Settings,
    ContentPart Part);

public sealed record ContentPart(
    string Name,
    JsonObject Settings,
    ContentPartField[] Fields);

public sealed record ContentPartField(
    string Name,
    JsonObject Settings,
    ContentField Field);

public sealed record ContentField(string Name);

public sealed record ContentItem(
    string ContentItemId,
    string ContentItemVersionId,
    string ContentType,
    string DisplayText,
    bool Published,
    bool Latest,
    DateTime? CreatedUtc,
    DateTime? ModifiedUtc,
    DateTime? PublishedUtc,
    string Owner,
    string Author,
    JsonElement Content);

public sealed record Feature(
    string Id,
    string Name,
    string Category,
    string Description,
    string ExtensionId,
    string[] Dependencies,
    bool AlwaysEnabled);

public sealed record Role(string Name, string Description, bool IsAdmin, bool IsSystem);

public sealed record ThemesState(
    string? CurrentSiteThemeId,
    string? CurrentAdminThemeId,
    ThemeSummary? CurrentSiteTheme,
    ThemeSummary? CurrentAdminTheme,
    ThemeSummary[] Themes)
{
    public static ThemesState Empty { get; } = new(null, null, null, null, []);
}

public sealed record ThemeSummary(
    string Id,
    string Name,
    string Description,
    string Author,
    string Website,
    string Version,
    string ExtensionId,
    bool IsAdmin,
    bool IsCurrent,
    bool Enabled,
    string PreviewImageUrl);

public sealed record IconSearchResult(IconLibrary[] Libraries, IconCatalogItem[] Items, int Total, int Skip, int Take)
{
    public static IconSearchResult Empty { get; } = new([], [], 0, 0, 200);
}

public sealed record IconLibrary(string Id, string Name, string? Version);

public sealed record IconCatalogItem(string Library, string? Version, string Name, string IconClass, string? SvgMarkup);

public sealed record AdminMenusState(AdminMenuSummary[] Menus)
{
    public static AdminMenusState Empty { get; } = new([]);
}

public sealed record AdminMenuSummary(string Id, string Name, bool Enabled, bool IsDefault, AdminMenuNodeSummary[] Nodes);

public sealed record AdminMenuEditModel(string? Name, bool Enabled);

public sealed record AdminMenuNodeSummary(
    string Id,
    string Type,
    string Text,
    string? Url,
    string? IconClass,
    bool Enabled,
    int Priority,
    string? DisplayPosition,
    string? ParentId,
    int Depth,
    int Order,
    string[] PermissionNames,
    bool IsCustom,
    string? OriginalText,
    AdminMenuNodeSummary[] Items);

public sealed record AdminMenuNodeEditModel(
    string Type,
    string Text,
    string? Url,
    string? IconClass,
    bool Enabled,
    int Priority,
    string? DisplayPosition,
    string[]? PermissionNames,
    string? ParentNodeId,
    int? Position);

public sealed record AdminMenuNodeMoveModel(string? ParentNodeId, int? Position);

public sealed record AdminMenuNodeRenameModel(string? Text);

public sealed record BlazingThemeSettings(string RadzenTheme, Dictionary<string, string> Tokens)
{
    public static BlazingThemeSettings Default { get; } = new(
        "material-base",
        new Dictionary<string, string>
        {
            ["primary"] = "#2f6f4e",
            ["secondary"] = "#6d5d3f",
            ["surface"] = "#ffffff",
            ["background"] = "#f7f8f6",
            ["radius"] = "6px",
        });
}
