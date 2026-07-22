using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Environment.Shell;
using OrchardCore.Entities;
using OrchardCore.Localization;
using OrchardCore.Localization.Models;
using OrchardCore.Settings;

namespace Crest.Controllers;

[ApiController, AutoValidateAntiforgeryToken, Route("api/crest/localization")]
public sealed class CrestLocalizationController(ISiteService sites, IShellReleaseManager releases, IAuthorizationService authorization) : ControllerBase
{
 [HttpGet] public async Task<ActionResult<CrestLocalization>> GetAsync(){if(!await authorization.AuthorizeAsync(User,LocalizationPermissions.ManageCultures))return Forbid();return Ok(CrestLocalization.From(await sites.GetSettingsAsync<LocalizationSettings>()));}
 [HttpPut] public async Task<ActionResult<CrestLocalization>> SaveAsync(CrestLocalization x){if(!await authorization.AuthorizeAsync(User,LocalizationPermissions.ManageCultures))return Forbid();var cultures=(x.SupportedCultures??[]).Where(c=>!string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();if(cultures.Length==0)return BadRequest("At least one supported culture is required.");var site=await sites.LoadSiteSettingsAsync();site.Alter<LocalizationSettings>(s=>{s.SupportedCultures=cultures;s.DefaultCulture=cultures.Contains(x.DefaultCulture,StringComparer.OrdinalIgnoreCase)?x.DefaultCulture:cultures[0];s.FallBackToParentCulture=x.FallBackToParentCulture;});await sites.UpdateSiteSettingsAsync(site);releases.RequestRelease();return Ok(x with {SupportedCultures=cultures,DefaultCulture=cultures.Contains(x.DefaultCulture,StringComparer.OrdinalIgnoreCase)?x.DefaultCulture:cultures[0]});}
}
public sealed record CrestLocalization(string DefaultCulture,string[]? SupportedCultures,bool FallBackToParentCulture){public static CrestLocalization From(LocalizationSettings x)=>new(x.DefaultCulture,x.SupportedCultures,x.FallBackToParentCulture);}
