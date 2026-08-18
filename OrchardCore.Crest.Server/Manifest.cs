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
    //   OrchardCore.DataLocalization -> TranslationsManager (AdminMenusController's
    //     promote-rename-to-translation endpoint). This is also the feature that registers
    //     IDataLocalizer, which is how Orchard's own Razor admin translates DB-backed admin
    //     menu node captions - without it a rename can be stored per-culture in the Crest
    //     layout but never promoted into the tenant's translation store, so the Razor admin
    //     and Crest would disagree about the caption.
    //   OrchardCore.Autoroute  -> ISite.HomeRoute (SiteController's home-page-lookup
    //     endpoint, consumed by Site's Home.razor) is only ever WRITTEN by
    //     AutoroutePartHandler.PublishedAsync, which only runs while this feature is
    //     enabled - AutorouteOptions itself is a ContentManagement.Abstractions type
    //     (already referenced), but without this feature enabled, HomeRoute would stay
    //     permanently null regardless. Also brings in OrchardCore.HomeRoute (its own
    //     manifest dependency) for free.
    // OrchardCore.Crest.LegacyFrame is deliberately absent from Dependencies AND from any
    // Before/After hint, even though this feature needs it enabled. In OrchardCore's
    // ordering model a module can never point at a theme in either way without creating a
    // cycle: ThemeExtensionDependencyStrategy gives every theme an implicit dependency on
    // every non-theme feature, so LegacyFrame -> Crest already exists, and any
    // Crest -> LegacyFrame edge closes the loop. The resulting fallback ordering is not
    // cosmetic - it breaks the theme's own shape/view registration, which is what made
    // legacy-frame requests silently render TheAdmin's full chrome. LegacyFrame is
    // IsAlwaysEnabled instead, which is all this feature actually needs (the theme has to
    // exist and be enabled so LegacyFrameThemeSelector can switch to it by Id at runtime;
    // its load order relative to this feature is irrelevant).
    Dependencies = ["OrchardCore.Admin", "OrchardCore.AdminMenu", "OrchardCore.Autoroute", "OrchardCore.Contents", "OrchardCore.DataLocalization", "OrchardCore.Indexing", "OrchardCore.Localization", "OrchardCore.Media", "OrchardCore.Menu", "OrchardCore.Navigation", "OrchardCore.Queries", "OrchardCore.Recipes", "OrchardCore.Security", "OrchardCore.Settings", "OrchardCore.Templates", "OrchardCore.Themes", "OrchardCore.Users", "OrchardCore.Crest.Icons"],
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
