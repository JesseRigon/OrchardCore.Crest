using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Crest Server",
    Author = "OrchardCore.Crest",
    Website = "https://crest.local",
    Version = "3.0.0.0.0",
    Description = "Provides Crest tenant APIs and server-side Orchard integrations.",
    Category = "OrchardCore.Crest"
)]

[assembly: Feature(
    Id = "OrchardCore.Crest",
    Name = "Crest Server",
    Description = "Provides Crest tenant APIs for authentication, theme settings, and app configuration.",
    Category = "OrchardCore.Crest",
    // Every entry here is constructor-injected or GetRequiredService'd by an
    // always-live controller/service in this assembly - see docs/feature-enablement.md
    // in the host repo for why these belong in the manifest rather than a setup recipe
    // (a recipe entry only fixes tenants provisioned from that recipe).
    //   OrchardCore.Navigation -> INavigationManager (AdminMenus/App/Navigation
    //     controllers). Resolves today only because OrchardCore.Admin happens to call
    //     AddNavigation() without declaring the feature; declared here so Crest does not
    //     depend on that incidental coupling.
    //   OrchardCore.Recipes    -> IRecipeExecutor (RecipesController.ExecuteAsync).
    //   OrchardCore.Autoroute  -> ISite.HomeRoute (SiteController's home-page-lookup
    //     endpoint, consumed by Site's Home.razor) is only ever WRITTEN by
    //     AutoroutePartHandler.PublishedAsync, which only runs while this feature is
    //     enabled - AutorouteOptions itself is a ContentManagement.Abstractions type
    //     (already referenced), but without this feature enabled, HomeRoute would stay
    //     permanently null regardless. Also brings in OrchardCore.HomeRoute (its own
    //     manifest dependency) for free.
    Dependencies = ["OrchardCore.Admin", "OrchardCore.AdminMenu", "OrchardCore.Autoroute", "OrchardCore.Contents", "OrchardCore.Indexing", "OrchardCore.Localization", "OrchardCore.Media", "OrchardCore.Menu", "OrchardCore.Navigation", "OrchardCore.Queries", "OrchardCore.Recipes", "OrchardCore.Security", "OrchardCore.Settings", "OrchardCore.Templates", "OrchardCore.Themes", "OrchardCore.Users", "OrchardCore.Crest.LegacyFrame", "OrchardCore.Crest.Icons"],
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.Icons",
    Name = "Orchard Crest UI Framework Icons",
    Description = "Provides packaged icon registry, local SVG icon sources, icon search, and icon pack delivery.",
    Category = "OrchardCore.Crest",
    IsAlwaysEnabled = true
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.Icons.TenantMedia",
    Name = "Orchard Crest UI Framework Tenant Media Icons",
    Description = "Allows tenants to upload, index, search, and use their own SVG icons from Orchard Media storage.",
    Category = "OrchardCore.Crest",
    Dependencies = ["OrchardCore.Crest.Icons", "OrchardCore.Media"]
)]

[assembly: Feature(
    Id = "OrchardCore.Crest.DesignSystem",
    Name = "Crest Design System",
    Description = "Adds tenant-level design token editing for Orchard Crest UI Framework without switching Orchard themes.",
    Category = "OrchardCore.Crest",
    Dependencies = ["OrchardCore.Settings", "OrchardCore.Themes"]
)]
