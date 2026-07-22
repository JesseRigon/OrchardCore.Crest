const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  try {
    await page.goto(`${baseUrl}/login`, { waitUntil: 'domcontentloaded' });
    await page.locator('input[name="UserName"]').fill(username);
    await page.locator('input[name="Password"]').fill(password);
    await page.getByRole('button', { name: 'Login', exact: true }).click();
    await page.waitForURL(/\/admin/i, { timeout: 15000 });

    await page.goto(`${baseUrl}/Admin/Tenants`, { waitUntil: 'domcontentloaded' });
    const tenants = page.locator('[data-testid="tenants-page"]');
    await tenants.waitFor({ timeout: 20000 });
    await tenants.getByText('Tenants', { exact: true }).first().waitFor();
    await tenants.locator('.rz-progressbar').waitFor({ state: 'hidden', timeout: 20000 });

    const response = await page.request.get(`${baseUrl}/api/crest/tenants`);
    if (!response.ok()) throw new Error(`Tenant catalog failed: ${response.status()}`);
    const catalog = await response.json();
    if (!Array.isArray(catalog.tenants) || !catalog.tenants.some(tenant => tenant.isDefault)) {
      throw new Error('Tenant catalog did not return the default tenant.');
    }
    if (await tenants.getByText('Manage tenants from the default Orchard tenant.', { exact: true }).count() !== 1) {
      throw new Error('The Blazor tenant page did not render.');
    }

    await tenants.locator('button').filter({ hasText: 'Add tenant' }).click();
    await page.locator('iframe.legacy-admin-frame').waitFor({ timeout: 20000 });
    if (!page.url().includes('/Admin/Tenants/Create')) throw new Error('Add tenant did not open Orchard’s native create form.');

    console.log(JSON.stringify({ page: 'tenants', tenants: catalog.tenants.length, defaultOnly: catalog.tenants.filter(tenant => tenant.isDefault).length }));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
