const path = require('path');
const { runSuite, printSummary } = require('./harness/run-suite');
const { loginAsAdmin } = require('./harness/auth');

// The set of admin-route checks that exist in the shared Crest submodule — every repo
// that embeds this submodule (fruitful.orchard, OrchardCore.Crest.Host) gets these for
// free. A consuming repo's own entry script (e.g. fruitful.orchard/dev/run-admin-suite.js)
// calls buildSharedAdminChecks() and appends its own repo-specific checks to the list
// before calling runSuite — same shared browser instance, same login, no extra cost.
//
// STATUS: 34 of the ~55 pre-existing raw scripts under OrchardCore.Crest.Admin,
// OrchardCore.Crest.Icons, and the shared OrchardCore.Crest/tests/playwright directory
// have been converted into this checks/ convention. The Icons subproject is fully
// converted. Remaining un-converted (still standalone, not wired in here): the menu
// editor scripts (drag/drop, icon overrides, layout export), primary-nav-menu
// interaction tests (flyout/compact/quickadd), and legacy-frame-workflows.js — these are
// stateful/interactive enough to need real judgment, not a mechanical port.
function buildSharedAdminChecks() {
  return [
    // Phase 8 hosting-model checks: SSR prerender, route gating ahead of render,
    // culture flowing into the static document, and InteractiveAuto handoff.
    { name: 'ssr-login-prerender', fn: require('./checks/ssr-login-prerender') },
    { name: 'route-auth-ssr', fn: require('./checks/route-auth-ssr') },
    { name: 'ssr-culture', fn: require('./checks/ssr-culture') },
    { name: 'admin-interactive-auto-handoff', fn: require('./checks/admin-interactive-auto-handoff') },
    { name: 'dashboard-screenshot', fn: require('./checks/dashboard-screenshot') },
    { name: 'features-page', fn: require('./checks/features-page') },
    { name: 'features-list-api', fn: require('./checks/features-list-api') },
    { name: 'standard-pages', fn: require('./checks/standard-pages') },
    { name: 'content-item-editor-page', fn: require('./checks/content-item-editor-page') },
    { name: 'content-items-page', fn: require('./checks/content-items-page') },
    { name: 'content-parts-page', fn: require('./checks/content-parts-page') },
    { name: 'content-types-page', fn: require('./checks/content-types-page') },
    { name: 'templates-page', fn: require('./checks/templates-page') },
    { name: 'roles-page', fn: require('./checks/roles-page') },
    { name: 'media-library-page', fn: require('./checks/media-library-page') },
    { name: 'media-options-page', fn: require('./checks/media-options-page') },
    { name: 'media-profiles-page', fn: require('./checks/media-profiles-page') },
    { name: 'localization-page', fn: require('./checks/localization-page') },
    { name: 'localization-cultures-page', fn: require('./checks/localization-cultures-page') },
    { name: 'titlebar', fn: require('./checks/titlebar') },
    { name: 'login-settings-page', fn: require('./checks/login-settings-page') },
    { name: 'main-content-tabs', fn: require('./checks/main-content-tabs') },
    { name: 'indexes-page', fn: require('./checks/indexes-page') },
    { name: 'queries-page', fn: require('./checks/queries-page') },
    { name: 'recipes-page', fn: require('./checks/recipes-page') },
    { name: 'security-headers-page', fn: require('./checks/security-headers-page') },
    { name: 'security-headers-response', fn: require('./checks/security-headers-response') },
    { name: 'settings-no-new-menu-toggle', fn: require('./checks/settings-no-new-menu-toggle') },
    { name: 'tenants-page', fn: require('./checks/tenants-page') },
    { name: 'users-page', fn: require('./checks/users-page') },
    { name: 'native-authorization-pages', fn: require('./checks/native-authorization-pages') },
    { name: 'navigation-authorization', fn: require('./checks/navigation-authorization') },
    { name: 'icon-selector', fn: require('./checks/icon-selector') },
    { name: 'icon-selector-remote-fallback', fn: require('./checks/icon-selector-remote-fallback') },
    { name: 'iconify-local-mirror', fn: require('./checks/iconify-local-mirror') },
    { name: 'iconify-provider-page', fn: require('./checks/iconify-provider-page') },
    { name: 'iconify-provider-reset', fn: require('./checks/iconify-provider-reset') },
    { name: 'iconify-remote-fallback', fn: require('./checks/iconify-remote-fallback') },
    { name: 'icon-tenant-media', fn: require('./checks/icon-tenant-media') },
    { name: 'menu-editor-design-system', fn: require('./checks/menu-editor/design-system') },
    { name: 'menu-editor-collapsed', fn: require('./checks/menu-editor/editor-collapsed') },
    { name: 'menu-editor-icon-override', fn: require('./checks/menu-editor/icon-override') },
    { name: 'menu-editor-icon-override-persistence', fn: require('./checks/menu-editor/icon-override-persistence') },
    { name: 'menu-editor-layout-export', fn: require('./checks/menu-editor/layout-export') },
    { name: 'menu-editor-new-node-locked', fn: require('./checks/menu-editor/new-node-locked') },
    { name: 'menu-editor-node-editor-dialog', fn: require('./checks/menu-editor/node-editor-dialog') },
    { name: 'primary-nav-single-shell-per-route', fn: require('./checks/primary-nav/single-admin-shell-per-route') },
    { name: 'primary-nav-model-list-actions', fn: require('./checks/primary-nav/model-list-actions') },
    { name: 'primary-nav-submenu-hierarchy', fn: require('./checks/primary-nav/submenu-hierarchy') },
    { name: 'primary-nav-flyout-hover-detached', fn: require('./checks/primary-nav/flyout-hover-detached') },
    { name: 'primary-nav-new-branch-editable', fn: require('./checks/primary-nav/new-branch-editable') },
    { name: 'primary-nav-settings-panel', fn: require('./checks/primary-nav/settings-panel') },
    { name: 'primary-nav-separators', fn: require('./checks/primary-nav/separators') },
    { name: 'primary-nav-compact-mode', fn: require('./checks/primary-nav/compact-mode') },
    { name: 'primary-nav-flyout-popup-placement', fn: require('./checks/primary-nav/flyout-popup-placement') },
    { name: 'primary-nav-quickadd-autoclose', fn: require('./checks/primary-nav/quickadd-autoclose') },
    { name: 'primary-nav-culture-override-persistence', fn: require('./checks/primary-nav/culture-override-persistence') },
    { name: 'legacy-frame-workflows', fn: require('./checks/legacy-frame-workflows') },
    { name: 'localization-sequential-settings', fn: require('./checks/localization-sequential-settings') },
    { name: 'localization-tab-scoping', fn: require('./checks/localization-tab-scoping') },
    { name: 'localization-new-tab-inheritance', fn: require('./checks/localization-new-tab-inheritance') },
    { name: 'localization-multi-user-switch', fn: require('./checks/localization-multi-user-switch') },
  ];
}

async function main() {
  const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
  const outputRoot = process.env.OUTPUT_ROOT || path.join(__dirname, 'output');

  const results = await runSuite({
    baseUrl,
    login: loginAsAdmin,
    checks: buildSharedAdminChecks(),
    outputRoot,
  });

  const ok = printSummary(results);
  process.exit(ok ? 0 : 1);
}

module.exports = { buildSharedAdminChecks };

if (require.main === module) {
  main();
}
