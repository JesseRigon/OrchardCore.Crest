using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Crest.Admin.Api;

public interface IApi
{
    ICrestArea Crest { get; }
}

public interface ICrestArea
{
    IRestApi Rest { get; }
}

public interface IRestApi
{
    IAuthApi Auth { get; }
    IAppApi App { get; }
    ISiteApi Site { get; }
    IAdminSettingsApi AdminSettings { get; }
    ITitleBarSettingsApi TitleBarSettings { get; }
    INavigationApi Navigation { get; }
    IContentApi Content { get; }
    IFeaturesApi Features { get; }
    IRolesApi Roles { get; }
    IThemeApi Theme { get; }
    IThemesApi Themes { get; }
    IAdminMenusApi AdminMenus { get; }
    IStandardMenusApi Menus { get; }
    IMediaApi Media { get; }
    IMediaProfilesApi MediaProfiles { get; }
    IMediaOptionsApi MediaOptions { get; }
    ITemplatesApi Templates { get; }
    ISecurityHeadersApi SecurityHeaders { get; }
    ILoginSettingsApi LoginSettings { get; }
    IUsersApi Users { get; }
    IRecipesApi Recipes { get; }
    ILocalizationApi Localization { get; }
    ITranslationsApi Translations { get; }
    IIndexesApi Indexes { get; }
    IQueriesApi Queries { get; }
    ITenantsApi Tenants { get; }
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

public interface IAdminSettingsApi
{
    Task<AdminSettingsDto> GetAsync();
    Task<AdminSettingsDto?> UpdateAsync(AdminSettingsUpdate update);
}

public interface ITitleBarSettingsApi
{
    Task<CrestTitleBarSettingsDto> GetAsync();
    Task<CrestTitleBarSettingsDto?> UpdateAsync(CrestTitleBarSettingsUpdate update);
}

public interface INavigationApi
{
    Task<NavigationMenu> GetAdminMenuAsync();
    Task<NavigationMenu> GetProfileMenuAsync();
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
    Task<ContentItemListResult> ListAsync(string? contentType = null, string? status = null, string? search = null, int page = 1, int pageSize = 20);
    Task<ContentItem?> GetAsync(string contentItemId);
    Task<ContentItem?> GetByHandleAsync(string handle);
    Task<ContentItem?> CreateAsync(ContentItemWriteRequest request);
    Task<ContentItem?> UpdateAsync(string contentItemId, ContentItemWriteRequest request);
    Task<bool> PublishAsync(string contentItemId);
    Task<bool> UnpublishAsync(string contentItemId);
    Task<bool> DeleteAsync(string contentItemId);
}

public interface IFeaturesApi
{
    Task<Feature[]> ListAsync();
    Task<bool> EnableAsync(string id);
    Task<bool> DisableAsync(string id);
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
    Task<CrestThemeSettings> GetAsync();
    Task<CrestThemeSettings?> UpdateAsync(CrestThemeSettings settings);
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
    Task<IconSearchResult> SearchAsync(string? library = null, string? query = null, int skip = 0, int take = 200, IEnumerable<IconSearchFilter>? filters = null);
    Task<CrestIconProvidersSettings> GetProvidersAsync();
    Task<CrestIconProvidersSettings?> UpdateProvidersAsync(CrestIconProvidersSettings settings);
    Task<IconifyLocalMirrorStatus> GetIconifyLocalStatusAsync();
    Task<TenantIconSummary[]> ListTenantAsync();
    Task<TenantIconSummary?> UploadTenantAsync(string fileName, Stream stream, bool overwrite = true);
    Task<bool> DeleteTenantAsync(string name);
}

public interface IMediaApi
{
    Task<MediaDirectoryResult> ListAsync(string? path = null);
    Task<MediaDirectoryResult?> CreateFolderAsync(string? parentPath, string name);
    Task<MediaDirectoryResult?> UploadAsync(string? path, string fileName, Stream stream, bool overwrite = false);
    Task<bool> DeleteAsync(string path);
}

public interface IMediaProfilesApi
{
    Task<MediaProfile[]> ListAsync();
    Task<MediaProfile?> SaveAsync(string name, MediaProfileWriteRequest request);
    Task<bool> DeleteAsync(string name);
}
public interface IMediaOptionsApi { Task<CrestMediaOptions?> GetAsync(); }
public interface ITemplatesApi { Task<CrestTemplate[]> ListAsync(); Task<CrestTemplate?> SaveAsync(string name, CrestTemplateWrite request); Task<bool> DeleteAsync(string name); }
public interface ISecurityHeadersApi { Task<CrestSecurityHeaders?> GetAsync(); Task<CrestSecurityHeaders?> SaveAsync(CrestSecurityHeaders value); }
public interface ILoginSettingsApi { Task<CrestLoginSettings?> GetAsync(); Task<CrestLoginSettings?> SaveAsync(CrestLoginSettings value); }
public interface IUsersApi { Task<CrestUserList> ListAsync(string? search = null, string? status = null); Task<CrestUser?> GetAsync(string id); Task<CrestUser?> CreateAsync(CrestUserWrite value); Task<CrestUser?> SaveAsync(string id, CrestUserWrite value); Task<CrestUser?> SetEnabledAsync(string id, bool enabled); Task<bool> DeleteAsync(string id); }
public interface IRecipesApi { Task<CrestRecipe[]> ListAsync(); Task<bool> ExecuteAsync(CrestRecipeKey value); }
public interface ILocalizationApi { Task<CrestLocalization?> GetAsync(); Task<CrestLocalization?> SaveAsync(CrestLocalization value); Task<CrestUserCulture?> GetMyCultureAsync(); Task<CrestUserCulture?> SetMyCultureAsync(CrestUserCulture value); Task<Dictionary<string, string>?> GetStringsAsync(string culture); }
public interface ITranslationsApi { Task<CrestTranslations?> GetAsync(string? culture = null); Task<CrestTranslations?> SaveAsync(CrestTranslationsSaveModel value); }
public interface IIndexesApi { Task<CrestIndex[]> ListAsync(); Task<CrestIndex?> RebuildAsync(string id); }
public interface IQueriesApi { Task<CrestQueryCatalog> ListAsync(string? search = null); Task<CrestQuery?> CreateAsync(CrestQueryWrite value); Task<CrestQuery?> SaveAsync(string name, CrestQueryWrite value); Task<bool> DeleteAsync(string name); Task<bool> DeleteManyAsync(string[] names); }
public interface ITenantsApi { Task<CrestTenantCatalog> ListAsync(string? search = null, string? category = null, string? state = null, string? orderBy = null); Task<CrestTenant?> EnableAsync(string name); Task<CrestTenant?> DisableAsync(string name); Task<bool> ReloadAsync(string name); Task<CrestTenant[]> BulkAsync(string action, string[] names); Task<bool> RemoveAsync(string name); }

public interface IStandardMenusApi
{
    Task<StandardMenusState> ListAsync();
    Task<StandardMenuSummary?> CreateMenuAsync(StandardMenuEditModel model);
    Task<StandardMenuSummary?> RenameMenuAsync(string menuId, StandardMenuEditModel model);
    Task<StandardMenuSummary?> ToggleMenuAsync(string menuId);
    Task<StandardMenuSummary?> DuplicateMenuAsync(string menuId);
    Task<bool> DeleteMenuAsync(string menuId);
    Task<StandardMenuSummary?> CreateNodeAsync(string menuId, StandardMenuNodeEditModel model);
    Task<StandardMenuSummary?> UpdateNodeAsync(string menuId, string nodeId, StandardMenuNodeEditModel model);
    Task<StandardMenuSummary?> MoveNodeAsync(string menuId, string nodeId, StandardMenuNodeMoveModel model);
    Task<StandardMenuSummary?> DuplicateNodeAsync(string menuId, string nodeId);
    Task<StandardMenuSummary?> DeleteNodeAsync(string menuId, string nodeId);
}

public interface IAdminMenusApi
{
    Task<AdminMenusState> ListAsync();
    Task<AdminMenuSummary?> GetAsync(string menuId);
    Task<AdminMenuSummary?> CreateMenuAsync(AdminMenuEditModel model);
    Task<AdminMenuSummary?> RenameMenuAsync(string menuId, AdminMenuEditModel model);
    Task<AdminMenuSummary?> ConvertMenuAsync(string menuId, ConvertMenuModel model);
    Task<AdminMenuSummary?> ToggleMenuAsync(string menuId);
    Task<AdminMenuSummary?> DuplicateMenuAsync(string menuId);
    Task<bool> DeleteMenuAsync(string menuId);
    Task<AdminMenuSummary?> CreateNodeAsync(string menuId, AdminMenuNodeEditModel model);
    Task<AdminMenuSummary?> UpdateNodeAsync(string menuId, string nodeId, AdminMenuNodeEditModel model);
    Task<AdminMenuSummary?> RenameNodeAsync(string menuId, string nodeId, AdminMenuNodeRenameModel model);
    Task<AdminMenuSummary?> PromoteNodeRenameAsync(string menuId, string nodeId);
    Task<AdminMenuSummary?> MoveNodeAsync(string menuId, string nodeId, AdminMenuNodeMoveModel model);
    Task<AdminMenuSummary?> ToggleNodeAsync(string menuId, string nodeId);
    Task<AdminMenuSummary?> DeleteNodeAsync(string menuId, string nodeId);
    Task<AdminMenuSummary?> CreateSeparatorAsync(string menuId, AdminMenuSeparatorEditModel model);
    Task<AdminMenuSummary?> MoveSeparatorAsync(string menuId, string separatorId, AdminMenuSeparatorEditModel model);
    Task<AdminMenuSummary?> DeleteSeparatorAsync(string menuId, string separatorId);
    Task<AdminMenuSummary?> UpdatePrimaryNavMenuSettingsAsync(string menuId, AdminPrimaryNavMenuSettings settings);
    Task<AdminMenuLayoutExportResult?> ExportLayoutAsync(string? fileName = null);
    Task<AdminMenuSummary?> PruneOverridesAsync(string menuId);
}

public sealed class Api(HttpClient http, ICrestAntiforgeryTokenStore antiforgery) : IApi
{
    public ICrestArea Crest { get; } = new CrestArea(http, antiforgery);
}

public sealed class CrestArea(HttpClient http, ICrestAntiforgeryTokenStore antiforgery) : ICrestArea
{
    public IRestApi Rest { get; } = new RestApi(http, antiforgery);
}

public sealed class RestApi(HttpClient http, ICrestAntiforgeryTokenStore antiforgery) : IRestApi
{
    public IAuthApi Auth { get; } = new AuthApi(http, antiforgery);
    public IAppApi App { get; } = new AppApi(http);
    public ISiteApi Site { get; } = new SiteApi(http);
    public IAdminSettingsApi AdminSettings { get; } = new AdminSettingsApi(http);
    public ITitleBarSettingsApi TitleBarSettings { get; } = new TitleBarSettingsApi(http);
    public INavigationApi Navigation { get; } = new NavigationApi(http);
    public IContentApi Content { get; } = new ContentApi(http);
    public IFeaturesApi Features { get; } = new FeaturesApi(http);
    public IRolesApi Roles { get; } = new RolesApi(http);
    public IThemeApi Theme { get; } = new ThemeApi(http);
    public IThemesApi Themes { get; } = new ThemesApi(http);
    public IAdminMenusApi AdminMenus { get; } = new AdminMenusApi(http);
    public IStandardMenusApi Menus { get; } = new StandardMenusApi(http);
    public IMediaApi Media { get; } = new MediaApi(http);
    public IMediaProfilesApi MediaProfiles { get; } = new MediaProfilesApi(http);
    public IMediaOptionsApi MediaOptions { get; } = new MediaOptionsApi(http);
    public ITemplatesApi Templates { get; } = new TemplatesApi(http);
    public ISecurityHeadersApi SecurityHeaders { get; } = new SecurityHeadersApi(http);
    public ILoginSettingsApi LoginSettings { get; } = new LoginSettingsApi(http);
    public IUsersApi Users { get; } = new UsersApi(http);
    public IRecipesApi Recipes { get; } = new RecipesApi(http);
    public ILocalizationApi Localization { get; } = new LocalizationApi(http);
    public ITranslationsApi Translations { get; } = new TranslationsApi(http);
    public IIndexesApi Indexes { get; } = new IndexesApi(http);
    public IQueriesApi Queries { get; } = new QueriesApi(http);
    public ITenantsApi Tenants { get; } = new TenantsApi(http);
    public IIconsApi Icons { get; } = new IconsApi(http);
}

public sealed class AuthApi(HttpClient http, ICrestAntiforgeryTokenStore antiforgery) : IAuthApi
{
    public async Task<AuthUser> MeAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/auth/me")));
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength == 0)
        {
            return AuthUser.Anonymous;
        }

        return await response.Content.ReadFromJsonAsync<AuthUser>() ?? AuthUser.Anonymous;
    }

    public async Task<AuthUser?> LoginAsync(LoginModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, "api/crest/auth/login")
        {
            Content = JsonContent.Create(model),
        }));

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        // Orchard antiforgery tokens are user-bound. Renew after sign-in.
        antiforgery.Clear();
        return await response.Content.ReadFromJsonAsync<AuthUser>();
    }

    public async Task<AuthUser> LogoutAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, "api/crest/auth/logout")));
        var user = await response.Content.ReadFromJsonAsync<AuthUser>() ?? AuthUser.Anonymous;
        antiforgery.Clear();
        return user;
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
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/app/manifest")));
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
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/site")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SiteSettings>() ?? SiteSettings.Default
            : SiteSettings.Default;
    }

    public async Task<SiteSettings?> UpdateAsync(SiteSettingsUpdate update)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Put, "api/crest/site")
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

public sealed class AdminSettingsApi(HttpClient http) : IAdminSettingsApi
{
    public async Task<AdminSettingsDto> GetAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/admin-settings")));
        if (!response.IsSuccessStatusCode)
        {
            return AdminSettingsDto.Default;
        }

        return await response.Content.ReadFromJsonAsync<AdminSettingsDto>() ?? AdminSettingsDto.Default;
    }

    public async Task<AdminSettingsDto?> UpdateAsync(AdminSettingsUpdate update)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Put, "api/crest/admin-settings")
        {
            Content = JsonContent.Create(update),
        }));

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<AdminSettingsDto>()
            : null;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class TitleBarSettingsApi(HttpClient http) : ITitleBarSettingsApi
{
    public async Task<CrestTitleBarSettingsDto> GetAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/title-bar-settings")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CrestTitleBarSettingsDto>() ?? CrestTitleBarSettingsDto.Default
            : CrestTitleBarSettingsDto.Default;
    }

    public async Task<CrestTitleBarSettingsDto?> UpdateAsync(CrestTitleBarSettingsUpdate update)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Put, "api/crest/title-bar-settings") { Content = JsonContent.Create(update) }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestTitleBarSettingsDto>() : null;
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
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/navigation/admin")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<NavigationMenu>() ?? NavigationMenu.Empty("admin")
            : NavigationMenu.Empty("admin");
    }

    public async Task<NavigationMenu> GetProfileMenuAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/navigation/profile")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<NavigationMenu>() ?? NavigationMenu.Empty("profile")
            : NavigationMenu.Empty("profile");
    }

    public async Task<NavigationMenu> GetMenuAsync(string menuName)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, $"api/crest/navigation/menus/{Uri.EscapeDataString(menuName)}")));
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
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/content-types")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ContentType[]>() ?? []
            : [];
    }

    public async Task<ContentType?> GetAsync(string contentType)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, $"api/crest/content-types/{Uri.EscapeDataString(contentType)}")));
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
    public async Task<ContentItemListResult> ListAsync(string? contentType = null, string? status = null, string? search = null, int page = 1, int pageSize = 20)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(contentType)) query.Add($"contentType={Uri.EscapeDataString(contentType)}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, $"api/crest/content-items?{string.Join('&', query)}")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ContentItemListResult>() ?? ContentItemListResult.Empty
            : ContentItemListResult.Empty;
    }

    public async Task<ContentItem?> GetByHandleAsync(string handle)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, $"api/crest/content-items/by-handle/{Uri.EscapeDataString(handle)}")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ContentItem>()
            : null;
    }

    public async Task<ContentItem?> GetAsync(string contentItemId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, $"api/crest/content-items/{Uri.EscapeDataString(contentItemId)}")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ContentItem>() : null;
    }

    public Task<ContentItem?> CreateAsync(ContentItemWriteRequest request) => WriteAsync(HttpMethod.Post, "api/crest/content-items", request);
    public Task<ContentItem?> UpdateAsync(string contentItemId, ContentItemWriteRequest request) => WriteAsync(HttpMethod.Put, $"api/crest/content-items/{Uri.EscapeDataString(contentItemId)}", request);

    public Task<bool> PublishAsync(string contentItemId) => SendActionAsync(HttpMethod.Post, $"api/crest/content-items/{Uri.EscapeDataString(contentItemId)}/publish");
    public Task<bool> UnpublishAsync(string contentItemId) => SendActionAsync(HttpMethod.Post, $"api/crest/content-items/{Uri.EscapeDataString(contentItemId)}/unpublish");
    public Task<bool> DeleteAsync(string contentItemId) => SendActionAsync(HttpMethod.Delete, $"api/crest/content-items/{Uri.EscapeDataString(contentItemId)}");

    private async Task<bool> SendActionAsync(HttpMethod method, string uri)
    {
        using var response = await http.SendAsync(WithCredentials(new(method, uri)));
        return response.IsSuccessStatusCode;
    }

    private async Task<ContentItem?> WriteAsync(HttpMethod method, string uri, ContentItemWriteRequest payload)
    {
        using var request = WithCredentials(new(method, uri));
        request.Content = JsonContent.Create(payload);
        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ContentItem>() : null;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class MediaApi(HttpClient http) : IMediaApi
{
    public async Task<MediaDirectoryResult> ListAsync(string? path = null)
    {
        var url = string.IsNullOrWhiteSpace(path) ? "api/crest/media" : $"api/crest/media?path={Uri.EscapeDataString(path)}";
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, url)));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<MediaDirectoryResult>() ?? MediaDirectoryResult.Empty
            : MediaDirectoryResult.Empty;
    }

    public async Task<MediaDirectoryResult?> CreateFolderAsync(string? parentPath, string name)
    {
        using var request = WithCredentials(new(HttpMethod.Post, "api/crest/media/folders"));
        request.Content = JsonContent.Create(new MediaFolderRequest(parentPath, name));
        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MediaDirectoryResult>() : null;
    }

    public async Task<MediaDirectoryResult?> UploadAsync(string? path, string fileName, Stream stream, bool overwrite = false)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(stream), "file", fileName);
        form.Add(new StringContent(path ?? string.Empty), "path");
        form.Add(new StringContent(overwrite ? "true" : "false"), "overwrite");
        using var request = WithCredentials(new(HttpMethod.Post, "api/crest/media/files"));
        request.Content = form;
        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MediaDirectoryResult>() : null;
    }

    public async Task<bool> DeleteAsync(string path)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Delete, $"api/crest/media?path={Uri.EscapeDataString(path)}")));
        return response.IsSuccessStatusCode;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class MediaProfilesApi(HttpClient http) : IMediaProfilesApi
{
    public async Task<MediaProfile[]> ListAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/media/profiles")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MediaProfile[]>() ?? [] : [];
    }
    public async Task<MediaProfile?> SaveAsync(string name, MediaProfileWriteRequest request)
    {
        using var message = WithCredentials(new(HttpMethod.Put, $"api/crest/media/profiles/{Uri.EscapeDataString(name)}"));
        message.Content = JsonContent.Create(request);
        using var response = await http.SendAsync(message);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MediaProfile>() : null;
    }
    public async Task<bool> DeleteAsync(string name)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Delete, $"api/crest/media/profiles/{Uri.EscapeDataString(name)}")));
        return response.IsSuccessStatusCode;
    }
    private static HttpRequestMessage WithCredentials(HttpRequestMessage request) { request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include); return request; }
}
public sealed class MediaOptionsApi(HttpClient http) : IMediaOptionsApi
{
    public async Task<CrestMediaOptions?> GetAsync() { using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/media/options"))); return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestMediaOptions>() : null; }
    private static HttpRequestMessage WithCredentials(HttpRequestMessage request) { request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include); return request; }
}
public sealed class TemplatesApi(HttpClient http) : ITemplatesApi
{
 public async Task<CrestTemplate[]> ListAsync(){using var r=await http.SendAsync(C(new(HttpMethod.Get,"api/crest/templates")));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestTemplate[]>()??[]:[];}
 public async Task<CrestTemplate?> SaveAsync(string name,CrestTemplateWrite x){using var q=C(new(HttpMethod.Put,$"api/crest/templates/{Uri.EscapeDataString(name)}"));q.Content=JsonContent.Create(x);using var r=await http.SendAsync(q);return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestTemplate>():null;}
 public async Task<bool> DeleteAsync(string name){using var r=await http.SendAsync(C(new(HttpMethod.Delete,$"api/crest/templates/{Uri.EscapeDataString(name)}")));return r.IsSuccessStatusCode;}
 static HttpRequestMessage C(HttpRequestMessage r){r.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);return r;}
}
public sealed class SecurityHeadersApi(HttpClient http) : ISecurityHeadersApi { public async Task<CrestSecurityHeaders?> GetAsync(){using var r=await http.SendAsync(C(new(HttpMethod.Get,"api/crest/security-headers")));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestSecurityHeaders>():null;}public async Task<CrestSecurityHeaders?> SaveAsync(CrestSecurityHeaders x){using var q=C(new(HttpMethod.Put,"api/crest/security-headers"));q.Content=JsonContent.Create(x);using var r=await http.SendAsync(q);return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestSecurityHeaders>():null;}static HttpRequestMessage C(HttpRequestMessage r){r.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);return r;}}
public sealed class LoginSettingsApi(HttpClient http) : ILoginSettingsApi { public async Task<CrestLoginSettings?> GetAsync(){using var r=await http.SendAsync(C(new(HttpMethod.Get,"api/crest/settings/login")));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestLoginSettings>():null;}public async Task<CrestLoginSettings?> SaveAsync(CrestLoginSettings x){using var q=C(new(HttpMethod.Put,"api/crest/settings/login"));q.Content=JsonContent.Create(x);using var r=await http.SendAsync(q);return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestLoginSettings>():null;}static HttpRequestMessage C(HttpRequestMessage r){r.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);return r;}}
public sealed class UsersApi(HttpClient http) : IUsersApi { public async Task<CrestUserList> ListAsync(string? search=null,string? status=null){var q=string.Join('&',new[]{string.IsNullOrWhiteSpace(search)?null:$"search={Uri.EscapeDataString(search)}",string.IsNullOrWhiteSpace(status)?null:$"status={Uri.EscapeDataString(status)}"}.Where(x=>x is not null));using var r=await http.SendAsync(C(new(HttpMethod.Get,$"api/crest/users{(q.Length>0?"?"+q:"")}")));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestUserList>()??new(0,[]):new(0,[]);} public async Task<CrestUser?> GetAsync(string id){using var r=await http.SendAsync(C(new(HttpMethod.Get,$"api/crest/users/{Uri.EscapeDataString(id)}")));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestUser>():null;} public Task<CrestUser?> CreateAsync(CrestUserWrite x)=>Write(new(HttpMethod.Post,"api/crest/users"),x); public Task<CrestUser?> SaveAsync(string id,CrestUserWrite x)=>Write(new(HttpMethod.Put,$"api/crest/users/{Uri.EscapeDataString(id)}"),x); public async Task<CrestUser?> SetEnabledAsync(string id,bool enabled)=>await Write(new(HttpMethod.Post,$"api/crest/users/{Uri.EscapeDataString(id)}/enabled"),new CrestUserEnabled(enabled)); public async Task<bool> DeleteAsync(string id){using var r=await http.SendAsync(C(new(HttpMethod.Delete,$"api/crest/users/{Uri.EscapeDataString(id)}")));return r.IsSuccessStatusCode;} async Task<CrestUser?> Write(HttpRequestMessage q,object x){using(q){q.Content=JsonContent.Create(x);using var r=await http.SendAsync(C(q));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestUser>():null;}} static HttpRequestMessage C(HttpRequestMessage r){r.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);return r;}}
public sealed class RecipesApi(HttpClient http) : IRecipesApi { public async Task<CrestRecipe[]> ListAsync(){using var r=await http.SendAsync(C(new(HttpMethod.Get,"api/crest/recipes")));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestRecipe[]>()??[]:[];} public async Task<bool> ExecuteAsync(CrestRecipeKey x){using var q=C(new(HttpMethod.Post,"api/crest/recipes/execute"));q.Content=JsonContent.Create(x);using var r=await http.SendAsync(q);return r.IsSuccessStatusCode;} static HttpRequestMessage C(HttpRequestMessage r){r.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);return r;}}
public sealed class LocalizationApi(HttpClient http) : ILocalizationApi { public async Task<CrestLocalization?> GetAsync(){using var r=await http.SendAsync(C(new(HttpMethod.Get,"api/crest/localization")));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestLocalization>():null;} public async Task<CrestLocalization?> SaveAsync(CrestLocalization x){using var q=C(new(HttpMethod.Put,"api/crest/localization"));q.Content=JsonContent.Create(x);using var r=await http.SendAsync(q);return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestLocalization>():null;} public async Task<CrestUserCulture?> GetMyCultureAsync(){using var r=await http.SendAsync(C(new(HttpMethod.Get,"api/crest/localization/me")));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestUserCulture>():null;} public async Task<CrestUserCulture?> SetMyCultureAsync(CrestUserCulture x){using var q=C(new(HttpMethod.Put,"api/crest/localization/me"));q.Content=JsonContent.Create(x);using var r=await http.SendAsync(q);return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestUserCulture>():null;} public async Task<Dictionary<string,string>?> GetStringsAsync(string culture){using var r=await http.SendAsync(C(new(HttpMethod.Get,$"api/crest/localization/strings?culture={Uri.EscapeDataString(culture)}")));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<Dictionary<string,string>>():null;} static HttpRequestMessage C(HttpRequestMessage r){r.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);return r;}}
public sealed class TranslationsApi(HttpClient http) : ITranslationsApi { public async Task<CrestTranslations?> GetAsync(string? culture = null){var url = culture is null ? "api/crest/translations" : $"api/crest/translations?culture={Uri.EscapeDataString(culture)}";using var r=await http.SendAsync(C(new(HttpMethod.Get,url)));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestTranslations>():null;} public async Task<CrestTranslations?> SaveAsync(CrestTranslationsSaveModel x){using var q=C(new(HttpMethod.Put,"api/crest/translations"));q.Content=JsonContent.Create(x);using var r=await http.SendAsync(q);return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestTranslations>():null;} static HttpRequestMessage C(HttpRequestMessage r){r.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);return r;}}
public sealed class IndexesApi(HttpClient http) : IIndexesApi { public async Task<CrestIndex[]> ListAsync(){using var r=await http.SendAsync(C(new(HttpMethod.Get,"api/crest/indexes")));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestIndex[]>()??[]:[];}public async Task<CrestIndex?> RebuildAsync(string id){using var r=await http.SendAsync(C(new(HttpMethod.Post,$"api/crest/indexes/{Uri.EscapeDataString(id)}/rebuild")));return r.IsSuccessStatusCode?await r.Content.ReadFromJsonAsync<CrestIndex>():null;}static HttpRequestMessage C(HttpRequestMessage r){r.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);return r;}}
public sealed class QueriesApi(HttpClient http) : IQueriesApi
{
    public async Task<CrestQueryCatalog> ListAsync(string? search = null) { var uri = string.IsNullOrWhiteSpace(search) ? "api/crest/queries" : $"api/crest/queries?search={Uri.EscapeDataString(search)}"; using var response = await http.SendAsync(C(new(HttpMethod.Get, uri))); return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestQueryCatalog>() ?? CrestQueryCatalog.Empty : CrestQueryCatalog.Empty; }
    public Task<CrestQuery?> CreateAsync(CrestQueryWrite value) => SendAsync(new(HttpMethod.Post, "api/crest/queries"), value);
    public Task<CrestQuery?> SaveAsync(string name, CrestQueryWrite value) => SendAsync(new(HttpMethod.Put, $"api/crest/queries/{Uri.EscapeDataString(name)}"), value);
    public async Task<bool> DeleteAsync(string name) { using var response = await http.SendAsync(C(new(HttpMethod.Delete, $"api/crest/queries/{Uri.EscapeDataString(name)}"))); return response.IsSuccessStatusCode; }
    public async Task<bool> DeleteManyAsync(string[] names) { using var request = C(new(HttpMethod.Post, "api/crest/queries/delete")); request.Content = JsonContent.Create(new CrestQueryNames(names)); using var response = await http.SendAsync(request); return response.IsSuccessStatusCode; }
    private async Task<CrestQuery?> SendAsync(HttpRequestMessage request, CrestQueryWrite value) { using (request) { request.Content = JsonContent.Create(value); using var response = await http.SendAsync(C(request)); return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestQuery>() : null; } }
    private static HttpRequestMessage C(HttpRequestMessage request) { request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include); return request; }
}
public sealed class TenantsApi(HttpClient http) : ITenantsApi
{
    public async Task<CrestTenantCatalog> ListAsync(string? search = null, string? category = null, string? state = null, string? orderBy = null) { var values = new[] { Q("search", search), Q("category", category), Q("state", state), Q("orderBy", orderBy) }.Where(value => value is not null); var uri = $"api/crest/tenants{(values.Any() ? "?" + string.Join('&', values!) : string.Empty)}"; using var response = await http.SendAsync(C(new(HttpMethod.Get, uri))); return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestTenantCatalog>() ?? CrestTenantCatalog.Empty : CrestTenantCatalog.Empty; }
    public Task<CrestTenant?> EnableAsync(string name) => ChangeAsync(name, "enable");
    public Task<CrestTenant?> DisableAsync(string name) => ChangeAsync(name, "disable");
    public async Task<bool> ReloadAsync(string name) { using var response = await http.SendAsync(C(new(HttpMethod.Post, $"api/crest/tenants/{Uri.EscapeDataString(name)}/reload"))); return response.IsSuccessStatusCode; }
    public async Task<CrestTenant[]> BulkAsync(string action, string[] names) { using var request = C(new(HttpMethod.Post, "api/crest/tenants/bulk")); request.Content = JsonContent.Create(new CrestTenantBulkAction(action, names)); using var response = await http.SendAsync(request); return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestTenant[]>() ?? [] : []; }
    public async Task<bool> RemoveAsync(string name) { using var response = await http.SendAsync(C(new(HttpMethod.Delete, $"api/crest/tenants/{Uri.EscapeDataString(name)}"))); return response.IsSuccessStatusCode; }
    private async Task<CrestTenant?> ChangeAsync(string name, string action) { using var response = await http.SendAsync(C(new(HttpMethod.Post, $"api/crest/tenants/{Uri.EscapeDataString(name)}/{action}"))); return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestTenant>() : null; }
    private static string? Q(string name, string? value) => string.IsNullOrWhiteSpace(value) ? null : $"{name}={Uri.EscapeDataString(value)}";
    private static HttpRequestMessage C(HttpRequestMessage request) { request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include); return request; }
}

public sealed class FeaturesApi(HttpClient http) : IFeaturesApi
{
    public async Task<Feature[]> ListAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/features")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Feature[]>() ?? []
            : [];
    }

    public Task<bool> EnableAsync(string id) => SetStateAsync(id, "enable");

    public Task<bool> DisableAsync(string id) => SetStateAsync(id, "disable");

    private async Task<bool> SetStateAsync(string id, string action)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/features/{Uri.EscapeDataString(id)}/{action}")));
        return response.IsSuccessStatusCode;
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
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/roles")));
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
    public async Task<CrestThemeSettings> GetAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/theme")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CrestThemeSettings>() ?? CrestThemeSettings.Default
            : CrestThemeSettings.Default;
    }

    public async Task<CrestThemeSettings?> UpdateAsync(CrestThemeSettings settings)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Put, "api/crest/theme")
        {
            Content = JsonContent.Create(settings),
        }));

        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CrestThemeSettings>() : null;
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
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/themes")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ThemesState>() ?? ThemesState.Empty
            : ThemesState.Empty;
    }

    public Task<bool> SetCurrentAsync(string id) => PostAsync($"api/crest/themes/{Uri.EscapeDataString(id)}/current");

    public Task<bool> EnableAsync(string id) => PostAsync($"api/crest/themes/{Uri.EscapeDataString(id)}/enable");

    public Task<bool> DisableAsync(string id) => PostAsync($"api/crest/themes/{Uri.EscapeDataString(id)}/disable");

    public Task<bool> ResetSiteThemeAsync() => PostAsync("api/crest/themes/reset-site");

    public Task<bool> ResetAdminThemeAsync() => PostAsync("api/crest/themes/reset-admin");

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
    public async Task<IconSearchResult> SearchAsync(string? library = null, string? query = null, int skip = 0, int take = 200, IEnumerable<IconSearchFilter>? filters = null)
    {
        var url = new StringBuilder("api/crest/icons?")
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

        foreach (var filter in filters ?? [])
        {
            if (!string.IsNullOrWhiteSpace(filter.Facet) && !string.IsNullOrWhiteSpace(filter.Value))
            {
                url.Append("&filter=").Append(Uri.EscapeDataString($"{filter.Facet}:{filter.Value}"));
            }
        }

        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, url.ToString())));
        if (!response.IsSuccessStatusCode)
        {
            return IconSearchResult.Empty;
        }

        return await response.Content.ReadFromJsonAsync<IconSearchResult>() ?? IconSearchResult.Empty;
    }

    public async Task<CrestIconProvidersSettings> GetProvidersAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/icons/providers")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CrestIconProvidersSettings>() ?? CrestIconProvidersSettings.Default
            : CrestIconProvidersSettings.Default;
    }

    public async Task<CrestIconProvidersSettings?> UpdateProvidersAsync(CrestIconProvidersSettings settings)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Put, "api/crest/icons/providers")
        {
            Content = JsonContent.Create(settings),
        }));

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CrestIconProvidersSettings>()
            : null;
    }

    public async Task<IconifyLocalMirrorStatus> GetIconifyLocalStatusAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/icons/providers/iconify/local")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<IconifyLocalMirrorStatus>() ?? IconifyLocalMirrorStatus.Empty
            : IconifyLocalMirrorStatus.Empty;
    }

    public async Task<TenantIconSummary[]> ListTenantAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/icons/tenant")));
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TenantIconSummary[]>() ?? []
            : [];
    }

    public async Task<TenantIconSummary?> UploadTenantAsync(string fileName, Stream stream, bool overwrite = true)
    {
        using var content = new MultipartFormDataContent
        {
            { new StreamContent(stream), "file", fileName },
            { new StringContent(overwrite.ToString()), "overwrite" }
        };

        using var request = WithCredentials(new(HttpMethod.Post, "api/crest/icons/tenant")
        {
            Content = content
        });

        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TenantIconSummary>()
            : null;
    }

    public async Task<bool> DeleteTenantAsync(string name)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Delete, $"api/crest/icons/tenant/{Uri.EscapeDataString(name)}")));
        return response.IsSuccessStatusCode;
    }

    private static HttpRequestMessage WithCredentials(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}

public sealed class StandardMenusApi(HttpClient http) : IStandardMenusApi
{
    public async Task<StandardMenusState> ListAsync()
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/menus")));
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Menus API returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        return await response.Content.ReadFromJsonAsync<StandardMenusState>() ?? StandardMenusState.Empty;
    }

    public async Task<StandardMenuSummary?> CreateMenuAsync(StandardMenuEditModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, "api/crest/menus") { Content = JsonContent.Create(model) }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<StandardMenuSummary>() : null;
    }

    public async Task<StandardMenuSummary?> RenameMenuAsync(string menuId, StandardMenuEditModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/menus/{Uri.EscapeDataString(menuId)}/rename") { Content = JsonContent.Create(model) }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<StandardMenuSummary>() : null;
    }

    public async Task<StandardMenuSummary?> ToggleMenuAsync(string menuId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/menus/{Uri.EscapeDataString(menuId)}/toggle")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<StandardMenuSummary>() : null;
    }

    public async Task<StandardMenuSummary?> DuplicateMenuAsync(string menuId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/menus/{Uri.EscapeDataString(menuId)}/duplicate")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<StandardMenuSummary>() : null;
    }

    public async Task<bool> DeleteMenuAsync(string menuId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Delete, $"api/crest/menus/{Uri.EscapeDataString(menuId)}")));
        return response.IsSuccessStatusCode;
    }

    public async Task<StandardMenuSummary?> CreateNodeAsync(string menuId, StandardMenuNodeEditModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/menus/{Uri.EscapeDataString(menuId)}/nodes") { Content = JsonContent.Create(model) }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<StandardMenuSummary>() : null;
    }

    public async Task<StandardMenuSummary?> UpdateNodeAsync(string menuId, string nodeId, StandardMenuNodeEditModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Put, $"api/crest/menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}") { Content = JsonContent.Create(model) }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<StandardMenuSummary>() : null;
    }

    public async Task<StandardMenuSummary?> MoveNodeAsync(string menuId, string nodeId, StandardMenuNodeMoveModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}/move") { Content = JsonContent.Create(model) }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<StandardMenuSummary>() : null;
    }

    public async Task<StandardMenuSummary?> DuplicateNodeAsync(string menuId, string nodeId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}/duplicate")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<StandardMenuSummary>() : null;
    }

    public async Task<StandardMenuSummary?> DeleteNodeAsync(string menuId, string nodeId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Delete, $"api/crest/menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<StandardMenuSummary>() : null;
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
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, "api/crest/admin-menus")));
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Admin menus API returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        return await response.Content.ReadFromJsonAsync<AdminMenusState>() ?? AdminMenusState.Empty;
    }

    public async Task<AdminMenuSummary?> GetAsync(string menuId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Get, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> CreateMenuAsync(AdminMenuEditModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, "api/crest/admin-menus")
        {
            Content = JsonContent.Create(model),
        }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> RenameMenuAsync(string menuId, AdminMenuEditModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/rename")
        {
            Content = JsonContent.Create(model),
        }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> ConvertMenuAsync(string menuId, ConvertMenuModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/convert")
        {
            Content = JsonContent.Create(model),
        }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> ToggleMenuAsync(string menuId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/toggle")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> DuplicateMenuAsync(string menuId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/duplicate")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<bool> DeleteMenuAsync(string menuId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Delete, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}")));
        return response.IsSuccessStatusCode;
    }

    public async Task<AdminMenuSummary?> PruneOverridesAsync(string menuId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/prune-overrides")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuLayoutExportResult?> ExportLayoutAsync(string? fileName = null)
    {
        var uri = "api/crest/admin-menu-layout/export";
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            uri += $"?file={Uri.EscapeDataString(fileName)}";
        }

        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, uri)));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuLayoutExportResult>() : null;
    }

    public Task<AdminMenuSummary?> CreateNodeAsync(string menuId, AdminMenuNodeEditModel model) =>
        SendNodeAsync(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/nodes", model);

    public Task<AdminMenuSummary?> UpdateNodeAsync(string menuId, string nodeId, AdminMenuNodeEditModel model) =>
        SendNodeAsync(HttpMethod.Put, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}", model);

    public Task<AdminMenuSummary?> RenameNodeAsync(string menuId, string nodeId, AdminMenuNodeRenameModel model) =>
        SendNodeAsync(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}/rename", model);

    // Promotes the rename already recorded for the current culture into the tenant's
    // translation store, so it also applies outside Crest's own sidebar. Requires
    // ManageTranslations on top of ManageAdminMenu, so this returns null (403) for admins who
    // may reorganize their own menu but not rewrite the tenant's translations.
    public Task<AdminMenuSummary?> PromoteNodeRenameAsync(string menuId, string nodeId) =>
        SendNodeAsync(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}/promote-rename", null);

    public Task<AdminMenuSummary?> MoveNodeAsync(string menuId, string nodeId, AdminMenuNodeMoveModel model) =>
        SendNodeAsync(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}/move", model);

    public Task<AdminMenuSummary?> ToggleNodeAsync(string menuId, string nodeId) =>
        SendNodeAsync(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}/toggle", null);

    public async Task<AdminMenuSummary?> DeleteNodeAsync(string menuId, string nodeId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Delete, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/nodes/{Uri.EscapeDataString(nodeId)}")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> CreateSeparatorAsync(string menuId, AdminMenuSeparatorEditModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/separators")
        {
            Content = JsonContent.Create(model),
        }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> DeleteSeparatorAsync(string menuId, string separatorId)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Delete, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/separators/{Uri.EscapeDataString(separatorId)}")));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> MoveSeparatorAsync(string menuId, string separatorId, AdminMenuSeparatorEditModel model)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/separators/{Uri.EscapeDataString(separatorId)}/move")
        {
            Content = JsonContent.Create(model),
        }));
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminMenuSummary>() : null;
    }

    public async Task<AdminMenuSummary?> UpdatePrimaryNavMenuSettingsAsync(string menuId, AdminPrimaryNavMenuSettings settings)
    {
        using var response = await http.SendAsync(WithCredentials(new(HttpMethod.Post, $"api/crest/admin-menus/{Uri.EscapeDataString(menuId)}/primary-nav-menu-settings")
        {
            Content = JsonContent.Create(settings),
        }));
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
    CrestTitleBarSettingsDto TitleBarSettings,
    AdminDescriptor Admin,
    int FeatureSerialNumber,
    string FeatureHash,
    Feature[] Features,
    NavigationMenu AdminMenu,
    CrestRouteAccess[] AuthorizedRoutes,
    CultureSelector CultureSelector,
    NavigationMenu? ProfileMenu = null);

public sealed record CrestRouteAccess(string Template);

public sealed record Tenant(
    string Name,
    string TenantId,
    string State,
    string? RequestUrlHost,
    string[] RequestUrlHosts,
    string? RequestUrlPrefix);

public sealed record AdminDescriptor(string BasePath);

public sealed record CultureSelector(
    string? UserDefaultCulture,
    string TenantDefaultCulture,
    string? AdminDefaultCulture,
    CultureOption[] Cultures,
    string CookieName,
    string CookiePath);

public sealed record CultureOption(string Value, string Label, string Icon);

public sealed record AdminSettingsDto(
    bool DisplayThemeToggler,
    bool DisplayMenuFilter,
    bool DisplayNewMenu,
    bool DisplayTitlesInTopbar)
{
    public static AdminSettingsDto Default { get; } = new(true, false, false, false);
}

public sealed record CrestTitleBarSettingsDto(
    bool DisplayCultureLabel,
    string? TenantAvatarImageUrl,
    string TenantAvatarShape,
    string? TenantAvatarClipPath,
    string? TenantAvatarBorderRadius)
{
    public static CrestTitleBarSettingsDto Default { get; } = new(false, null, "Circle", null, null);
}

public sealed record CrestTitleBarSettingsUpdate(
    bool DisplayCultureLabel,
    string? TenantAvatarImageUrl,
    string TenantAvatarShape,
    string? TenantAvatarClipPath,
    string? TenantAvatarBorderRadius);

public sealed record AdminSettingsUpdate(
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
    int MaxPagedCount,
    bool AppendVersion,
    bool UseCdn,
    string CdnBaseUrl,
    string ResourceDebugMode,
    string CacheMode)
{
    public static SiteSettings Default { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        10,
        100,
        0,
        true,
        false,
        string.Empty,
        "FromConfiguration",
        "FromConfiguration");
}

public sealed record SiteSettingsUpdate(
    string SiteName,
    string PageTitleFormat,
    string BaseUrl,
    string TimeZoneId,
    string Calendar,
    int PageSize,
    int MaxPageSize,
    int MaxPagedCount,
    bool AppendVersion,
    bool UseCdn,
    string CdnBaseUrl,
    string ResourceDebugMode,
    string CacheMode);

public sealed record NavigationMenu(string Name, NavigationItem[] Items, Crest.Icons.IconPack? Icons = null, NavigationSeparator[]? Separators = null, AdminPrimaryNavMenuSettings? PrimaryNavMenuSettings = null)
{
    public static NavigationMenu Empty(string name) => new(name, []);
}

public sealed record NavigationSeparator(string Key, string? ParentKey, int Order);

public sealed record NavigationIcon(string? Key, string Library, string? Version, string? Style, string Name, string? SvgMarkup);

public sealed record NavigationItem(
    string Text,
    string? TextKey,
    string? Id,
    string? Href,
    string? Url,
    string? Target,
    string? Position,
    NavigationIcon? Icon,
    string[] Classes,
    NavigationItem[] Items)
{
    // Must mirror NavigationController.NavigationItem.Key exactly (server and client
    // compute the same key independently from the same wire payload) - Text is
    // translated and must never be part of the match key.
    //
    // TextKey is MenuItem.Text.Name, the untranslated S["..."] literal that OrchardCore's
    // own Merge matches on, so it does not vary by admin culture. Id is preferred because
    // it survives a caption being reworded; TextKey is the fallback for items whose
    // provider never set an Id, which would otherwise have no stable handle at all.
    public string? Key => !string.IsNullOrEmpty(Id) ? Id : TextKey;
    public string? Link => !string.IsNullOrWhiteSpace(Href) ? Href : Url;
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

public sealed record ContentItemListResult(ContentItem[] Items, int Total, int Page, int PageSize)
{
    public static ContentItemListResult Empty { get; } = new([], 0, 1, 20);
}

public sealed record ContentItemWriteRequest(string ContentType, string? DisplayText, JsonObject? Content, bool Publish);

public sealed record MediaDirectoryResult(string Path, string? ParentPath, MediaEntry[] Entries)
{
    public static MediaDirectoryResult Empty { get; } = new(string.Empty, null, []);
}

public sealed record MediaEntry(string Path, string Name, bool IsDirectory, long Length, DateTimeOffset LastModifiedUtc, string? PublicUrl);
public sealed record MediaFolderRequest(string? ParentPath, string? Name);
public sealed record MediaProfile(string Name, string? Hint, int Width, int Height, int Mode, int Format, int Quality, string? BackgroundColor, bool AutoOrient);
public sealed record MediaProfileWriteRequest(string? Hint, int Width, int Height, int Mode, int Format, int Quality, string? BackgroundColor, bool AutoOrient);
public sealed record CrestMediaOptions(int[] SupportedSizes, IEnumerable<string> AllowedFileExtensions, int MaxBrowserCacheDays, int MaxSecureFilesBrowserCacheDays, int MaxCacheDays, long MaxFileSize, int? MaxUploadChunkSize, string CdnBaseUrl, string AssetsRequestPath, string AssetsPath, string AssetsUsersFolder, bool UseTokenizedQueryString);
public sealed record CrestTemplate(string Name,string? Description,string Content);
public sealed record CrestTemplateWrite(string? Description,string? Content);
public sealed record CrestSecurityHeaders(Dictionary<string,string>? ContentSecurityPolicy,Dictionary<string,string>? PermissionsPolicy,string? ReferrerPolicy,bool FromConfiguration);
public sealed class CrestLoginSettings
{
    public bool AllowRememberMe { get; set; }
    public bool AllowChangingUsername { get; set; }
    public bool AllowChangingEmail { get; set; }
    public bool AllowChangingPhoneNumber { get; set; }
    public bool UseSiteTheme { get; set; }
    public bool DisableLocalLogin { get; set; }
    public bool RequireTwoFactorAuthentication { get; set; }
    public bool AllowRememberClientTwoFactorAuthentication { get; set; }
    public int NumberOfRecoveryCodesToGenerate { get; set; } = 5;
    public bool UseSiteThemeForTwoFactorAuthentication { get; set; }
    public bool UseExternalProviderIfOnlyOneDefined { get; set; }
    public bool UseScriptToSyncProperties { get; set; }
    public string? SyncPropertiesScript { get; set; }
}
public sealed record CrestUserList(int Total, CrestUser[] Items);
public sealed record CrestUser(string Id,string? UserName,string? Email,string? PhoneNumber,bool EmailConfirmed,bool IsEnabled,bool TwoFactorEnabled,string[] Roles);
public sealed class CrestUserWrite
{
    public CrestUserWrite() { }
    public CrestUserWrite(string? userName,string? email,string? phoneNumber,bool emailConfirmed,bool isEnabled,string[]? roles,string? password) { UserName=userName; Email=email; PhoneNumber=phoneNumber; EmailConfirmed=emailConfirmed; IsEnabled=isEnabled; Roles=roles; Password=password; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool IsEnabled { get; set; }
    public string[]? Roles { get; set; }
    public string? Password { get; set; }
}
public sealed record CrestUserEnabled(bool Enabled);
public sealed record CrestRecipe(string Name,string? DisplayName,string? Description,string? FileName,string? BasePath,string[]? Tags);
public sealed record CrestRecipeKey(string? BasePath,string? FileName);
public sealed class CrestLocalization
{
    public string DefaultCulture { get; set; } = "";
    public string[] SupportedCultures { get; set; } = [];
    public bool FallBackToParentCulture { get; set; }
    public string? AdminDefaultCulture { get; set; }
    public CrestCulture[] AvailableCultures { get; set; } = [];
}

public sealed record CrestCulture(string Value, string Label, string NativeLabel);
public sealed record CrestUserCulture(string? Culture);
public sealed record CrestTranslations(CrestTranslationCulture[] Cultures, string Culture, bool CanEdit, CrestTranslationGroup[] Groups);
public sealed record CrestTranslationCulture(string Value, string Label, bool CanEdit);
public sealed record CrestTranslationGroup(string Name, CrestTranslationString[] Strings);
// Orphan: stored translation no provider currently enumerates (disabled feature, changed
// source string) - still applied at render time, shown so it stays editable/deletable.
public sealed record CrestTranslationString(string Context, string Key, string Value, bool Orphan);
public sealed record CrestTranslationsSaveModel(string Culture, CrestTranslationSaveEntry[] Translations);
public sealed record CrestTranslationSaveEntry(string Context, string Key, string? Value);
public sealed record CrestIndex(string Id,string? Name,string? Provider,string? IndexName,string? Type,string? CreatedUtc);
public sealed record CrestQueryCatalog(CrestQuery[] Queries, string[] Sources)
{
    public static CrestQueryCatalog Empty { get; } = new([], []);
}
public sealed record CrestQuery(string Name, string Source, string? Schema, bool ReturnContentItems, JsonObject Properties);
public sealed record CrestQueryWrite(string Name, string Source, string? Schema, bool ReturnContentItems, JsonObject? Properties);
public sealed record CrestQueryNames(string[]? Names);
public sealed record CrestTenantCatalog(CrestTenant[] Tenants, string[] Categories, bool TenantRemovalAllowed) { public static CrestTenantCatalog Empty { get; } = new([], [], false); }
public sealed record CrestTenantBulkAction(string Action, string[]? Names);
public sealed record CrestTenant(string Name, string State, string? Category, string? Description, string? RequestUrlHost, string? RequestUrlPrefix, bool IsDefault, bool IsRemovable);

public sealed record Feature(
    string Id,
    string Name,
    string Category,
    string Description,
    string ExtensionId,
    string[] Dependencies,
    bool AlwaysEnabled,
    bool Enabled,
    bool EnabledByDependencyOnly);

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

public sealed record IconSearchResult(IconLibrary[] Libraries, IconSearchFacet[] Facets, IconCatalogItem[] Items, int Total, int Skip, int Take)
{
    public static IconSearchResult Empty { get; } = new([], [], [], 0, 0, 200);
}

public sealed record IconLibrary(string Id, string Name, string? Version, string? ProviderId = null, string? ProviderName = null);

public sealed record IconSearchFilter(string Facet, string Value);

public sealed record IconSearchFacet(string Id, string Label, string SelectionMode, IconSearchFacetOption[] Options);

public sealed record IconSearchFacetOption(string Value, string Label, int? Count = null);

public sealed record IconCatalogItem(string Key, string Library, string? Version, string Style, string Name, string IconClass, string? SvgMarkup, string? ProviderId = null);

public sealed record TenantIconSummary(string Key, string Name, string DisplayName, string IconClass, string Path, string PublicUrl);

public sealed record CrestIconProvidersSettings(IconifyIconProviderSettings Iconify)
{
    public static CrestIconProvidersSettings Default { get; } = new(IconifyIconProviderSettings.Default);
}

public sealed record IconifyIconProviderSettings(
    bool Enabled,
    string BaseUrl,
    string? ApiKey,
    string? ApiKeyHeader,
    string[] Prefixes,
    bool LocalLibraryCacheEnabled = true)
{
    public static IconifyIconProviderSettings Default { get; } = new(
        true,
        "https://api.iconify.design",
        null,
        null,
        [],
        true);
}

public sealed record IconifyLocalMirrorStatus(
    bool IsAvailable,
    bool IsSyncing,
    string? Version,
    string RootPath,
    string? SourcePath,
    int PrefixCount,
    int IconCount,
    DateTimeOffset? LastSyncUtc,
    DateTimeOffset? LastErrorUtc,
    string? LastError,
    bool RemoteFallbackEnabled = true)
{
    public static IconifyLocalMirrorStatus Empty { get; } = new(false, false, null, string.Empty, null, 0, 0, null, null, null);
}

public sealed record StandardMenusState(StandardMenuSummary[] Menus, StandardMenuNodeType[] AvailableNodeTypes)
{
    public static StandardMenusState Empty { get; } = new([], []);
}

public sealed record StandardMenuNodeType(string Type, string DisplayName);

public sealed record StandardMenuEditModel(string? Name, bool Published);

public sealed record StandardMenuSummary(string Id, string ContentItemId, string ContentItemVersionId, string Name, bool Published, StandardMenuNodeSummary[] Nodes);

public sealed record StandardMenuNodeSummary(
    string Id,
    string Type,
    string Text,
    string? Url,
    string? Target,
    string? Html,
    bool Enabled,
    int Depth,
    int Order,
    string? ParentId,
    string[] PermissionNames,
    StandardMenuNodeSummary[] Items);

public sealed record StandardMenuNodeEditModel(
    string Type,
    string Text,
    string? Url,
    string? Target,
    string? Html,
    string[]? PermissionNames,
    string? ParentNodeId,
    int? Position);

public sealed record StandardMenuNodeMoveModel(string? ParentNodeId, int? Position);

public sealed record AdminMenusState(AdminMenuSummary[] Menus)
{
    public static AdminMenusState Empty { get; } = new([]);
}

// Vanilla OrchardCore's own AdminMenu has no concept of placement — every custom menu it
// stores gets injected into the "admin" sidebar tree unconditionally. Placement is a
// Crest-owned classification (tracked server-side in CrestMenuPlacementDocument) layered on
// top: Admin behaves exactly as it always has; Local and User are excluded from the
// sidebar (AdminMenu.Enabled forced false server-side) and tracked independently. Admin and
// Local are convertible into each other; User is stable once created. More placements may
// exist later, filtered by the current screen/responsive size.
[JsonConverter(typeof(JsonStringEnumConverter<CrestMenuPlacement>))]
public enum CrestMenuPlacement
{
    Admin,
    Local,
    User,
}

public sealed record ConvertMenuModel(CrestMenuPlacement Placement);

public sealed record AdminMenuSummary(string Id, string Name, bool Enabled, bool IsDefault, AdminMenuSeparatorSummary[] Separators, AdminPrimaryNavMenuSettings PrimaryNavMenuSettings, Crest.Icons.IconPack? Icons, AdminMenuNodeSummary[] Nodes, CrestMenuPlacement Placement = CrestMenuPlacement.Admin);

// Available options are expected to grow (more anchor corners, responsive-size-specific
// choices) — keep this an open enum rather than a bool.
[JsonConverter(typeof(JsonStringEnumConverter<PrimaryNavMenuCollapseIconPosition>))]
public enum PrimaryNavMenuCollapseIconPosition
{
    OutsideBottomRight,
    InsideBottomLeft,
}

public sealed class AdminPrimaryNavMenuSettings
{
    public bool Collapsible { get; set; } = true;
    public int ExpansionDurationMilliseconds { get; set; } = 500;
    public List<bool> TierSeparators { get; set; } = [true, false, false];
    public List<string> TierIndents { get; set; } = ["0rem", "0.75rem", "1.25rem", "1.75rem"];
    public List<string> TierBackgrounds { get; set; } = ["transparent", "transparent", "color-mix(in srgb, var(--crest-color-surface-1) 88%, var(--crest-color-text-1) 12%)", "transparent"];
    public List<string> TierBaseSizes { get; set; } = ["1rem", "0.95rem", "0.9rem"];
    public List<double> TierBaseRems { get; set; } = [1.0, 0.95, 0.9];
    public PrimaryNavMenuCollapseIconPosition CollapseIconPosition { get; set; } = PrimaryNavMenuCollapseIconPosition.OutsideBottomRight;
}

public sealed record AdminMenuSeparatorSummary(string Id, string? ParentId, int Depth, int Order);

public sealed record AdminMenuEditModel(string? Name, bool Enabled, CrestMenuPlacement Placement = CrestMenuPlacement.Admin);

public sealed record AdminMenuNodeSummary(
    string Id,
    string Type,
    string Text,
    string? Url,
    string? IconClass,
    NavigationIcon? Icon,
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

public sealed record AdminMenuSeparatorEditModel(string? ParentNodeId, int? Position);

public sealed record AdminMenuLayoutExportResult(string File, string Path, int ItemCount, int CustomItemCount, int SeparatorCount);

public sealed record CrestThemeSettings(string RadzenTheme, Dictionary<string, string> Tokens)
{
    public static CrestThemeSettings Default { get; } = new(
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
