// Converted from the old admin-tenants-page.js. Verifies the native Tenants page renders,
// its API-backed catalog includes the default tenant, and that "Add tenant" still falls
// back to Orchard's native (legacy iframe) create form.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Tenants`, { waitUntil: 'domcontentloaded' });
  const tenants = page.locator('[data-testid="tenants-page"]');

  const rendered = await tenants.waitFor({ timeout: 20000 }).then(() => true).catch(() => false);
  if (!rendered) {
    return { name: 'renders-tenants-page', pass: false, message: `page did not render at ${page.url()}` };
  }
  await tenants.getByText('Tenants', { exact: true }).first().waitFor().catch(() => {});
  await tenants.locator('.rz-progressbar').waitFor({ state: 'hidden', timeout: 20000 }).catch(() => {});

  const results = [{ name: 'renders-tenants-page', pass: true, message: 'ok' }];

  const response = await page.request.get(`${ctx.baseUrl}/api/crest/tenants`);
  let catalog = null;
  if (response.ok()) {
    catalog = await response.json();
  }
  const hasDefaultTenant = Boolean(catalog && Array.isArray(catalog.tenants) && catalog.tenants.some(tenant => tenant.isDefault));
  results.push({
    name: 'tenant-catalog-includes-default',
    pass: response.ok() && hasDefaultTenant,
    message: response.ok() ? `tenants=${catalog?.tenants?.length ?? 0}` : `catalog request failed: ${response.status()}`,
  });

  const blazorPageTextCount = await tenants.getByText('Manage tenants from the default Orchard tenant.', { exact: true }).count();
  results.push({
    name: 'renders-blazor-tenant-page',
    pass: blazorPageTextCount === 1,
    message: `count=${blazorPageTextCount}`,
  });

  await tenants.locator('button').filter({ hasText: 'Add tenant' }).click();
  const legacyFrame = await page.locator('iframe.legacy-admin-frame').waitFor({ timeout: 20000 }).then(() => true).catch(() => false);
  const opensCreateForm = legacyFrame && page.url().includes('/Admin/Tenants/Create');
  results.push({
    name: 'add-tenant-opens-native-create-form',
    pass: opensCreateForm,
    message: `legacyFrame=${legacyFrame} url=${page.url()}`,
  });

  return results;
};
