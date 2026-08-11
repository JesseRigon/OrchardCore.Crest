const { severeConsoleErrors, drainConsoleErrors } = require('../harness/instance');

// Converted from the old admin-titlebar.js. Same assertions, minus the per-script
// browser launch/login boilerplate — that now lives once in the harness. Uses the
// localization settings page (as the original did) purely as a page that renders the
// shared admin titlebar; the assertions are about the titlebar, not that page.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Settings/localization`, { waitUntil: 'networkidle' });
  await page.locator('.admin-titlebar').waitFor({ timeout: 20000 });

  const tenantSelector = page.locator('.admin-titlebar__tenant-selector');
  const tenantSelectorCount = await tenantSelector.count();
  const legacyTenantMenuCount = await page.locator('.admin-titlebar__tenant-menu').count();

  // Since the Radzen source merge (ad8db47) the dropdown root is the visible,
  // clickable face — the old .crest-dropdown__trigger inner button is gone. The
  // culture value (flag icon) renders in the label span; the chevron is a
  // .crest-icon inside .rz-dropdown-trigger.
  const cultureSelector = page.locator('.admin-titlebar__culture-selector');
  const cultureSelectorCount = await cultureSelector.count();
  let cultureIconCount = null;
  if (cultureSelectorCount) {
    cultureIconCount = await cultureSelector
      .locator('.rz-dropdown-label .orchard-icon, .rz-dropdown-trigger .crest-icon, .rz-dropdown-trigger .orchard-icon')
      .count();
  }

  let tenantBackground = null;
  if (tenantSelectorCount === 1) {
    tenantBackground = await tenantSelector
      .evaluate(element => getComputedStyle(element).backgroundColor);
  }

  const errors = severeConsoleErrors(drainConsoleErrors(ctx.consoleErrors));

  return [
    { name: 'tenant-selector-single-instance', pass: tenantSelectorCount === 1, message: `count=${tenantSelectorCount}` },
    { name: 'no-legacy-tenant-menu', pass: legacyTenantMenuCount === 0, message: `count=${legacyTenantMenuCount}` },
    {
      name: 'culture-trigger-has-icon-and-chevron',
      // Original only asserted this when a culture selector happened to be present.
      pass: cultureSelectorCount === 0 || cultureIconCount >= 2,
      message: cultureSelectorCount === 0 ? 'no culture selector present' : `icons=${cultureIconCount}`,
    },
    {
      name: 'tenant-selector-no-accent-background',
      pass: tenantSelectorCount === 1 && tenantBackground === 'rgba(0, 0, 0, 0)',
      message: `background=${tenantBackground}`,
    },
    { name: 'no-console-errors', pass: errors.length === 0, message: errors.join(' | ') || 'clean' },
  ];
};
