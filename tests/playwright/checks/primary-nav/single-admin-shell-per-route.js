// Converted from OrchardCore.Crest.Admin/tests/playwright/admin-route-primary-nav-menu.js.
// The original script was parameterized per-invocation (ADMIN_ROUTE / EXPECT_ACTIVE_TEXT /
// EXPECT_INACTIVE_TEXT env vars) to probe many routes individually. This keeps the
// generally-true invariant — exactly one Crest admin shell renders, with a visible
// primaryNavMenu and main content area — for a representative route, rather than
// re-parameterizing per fixed suite run.
//
// Note: the original defaulted to /Admin/CRM/Customers, which predates this repo's
// CRM -> Accounting rename; updated to /Admin/Accounting/Customers.
module.exports = async function run(page, ctx) {
  const route = '/Admin/Accounting/Customers';
  await page.goto(`${ctx.baseUrl}${route}`, { waitUntil: 'networkidle' });
  await page.locator('.admin-shell').waitFor({ timeout: 15000 });
  await page.locator('.primary-nav-menu').waitFor({ timeout: 15000 });
  await page.locator('.admin-dashboard__main').waitFor({ timeout: 15000 });

  const shellCount = await page.locator('.admin-shell').count();
  const primaryNavMenuVisible = await page.locator('.primary-nav-menu').first().isVisible();
  const mainVisible = await page.locator('.admin-dashboard__main').first().isVisible();

  return [
    {
      name: 'exactly-one-admin-shell',
      pass: shellCount === 1 && primaryNavMenuVisible && mainVisible,
      message: `shellCount=${shellCount} primaryNavMenuVisible=${primaryNavMenuVisible} mainVisible=${mainVisible}`,
    },
  ];
};
