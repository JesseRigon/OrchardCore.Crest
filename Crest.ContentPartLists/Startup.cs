using Crest.Migrations;
using Crest.Models;
using Crest.Navigation;
using Crest.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace Crest.ContentPartLists;

// Content Part Lists - the tenant-editable enum system (see the host repo's
// plans/fruitful-modules.md, Tier 0). Modules declare the sets they own and consume
// them by logical key; tenants relabel/reorder/hide at runtime. Extracted from
// Crest.Server into its own feature so tenants opt in rather than getting it with
// the Crest core - consumers (the Fruitful modules) declare it as a manifest
// dependency instead.
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<CrestContentPartListPart>();
        services.AddContentPart<CrestOptionPart>();
        services.AddDataMigration<CrestContentPartListMigrations>();
        services.AddDataMigration<GlobalContentPartListsMigrations>();
        services.AddScoped<ICrestContentPartListService, CrestContentPartListService>();

        // Culture -> measurement system -> preferred units (CLDR-derived data);
        // stateless, so a singleton.
        services.AddSingleton<IMeasurementPreferenceService, MeasurementPreferenceService>();

        // Plugs Content Part Lists into Crest.Server's picker infrastructure as one source
        // provider among several (users and content items live in Crest.Server).
        services.AddScoped<IOptionSourceProvider, ContentPartListSourceProvider>();

        services.AddNavigationProvider<ContentPartListsAdminMenu>();
    }
}
