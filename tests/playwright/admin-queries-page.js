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

    await page.goto(`${baseUrl}/Admin/Queries/Index`, { waitUntil: 'domcontentloaded' });
    const queries = page.locator('[data-testid="queries-page"]');
    await queries.waitFor({ timeout: 20000 });
    await queries.getByText('Queries', { exact: true }).first().waitFor();
    await queries.locator('.rz-progressbar').waitFor({ state: 'hidden', timeout: 20000 });
    if (await queries.getByText('The source and metadata are provider-neutral.', { exact: false }).count()) {
      throw new Error('Query editor was unexpectedly open.');
    }

    const response = await page.request.get(`${baseUrl}/api/crest/queries`);
    if (!response.ok()) throw new Error(`Query catalog failed: ${response.status()}`);
    const catalog = await response.json();
    if (!Array.isArray(catalog.sources)) throw new Error('Query catalog did not return sources.');

    const removeQuery = async name => {
      const row = page.locator('tr').filter({ hasText: name });
      await row.locator('button').last().click();
      await page.getByText(`Delete query ${name}?`, { exact: false }).waitFor();
      await page.getByRole('button', { name: 'Delete', exact: true }).click();
      await row.waitFor({ state: 'detached', timeout: 20000 });
    };

    for (const query of catalog.queries.filter(query => query.name.startsWith('crest-playwright-'))) {
      await removeQuery(query.name);
    }

    if (catalog.sources.length > 0) {
      await queries.locator('button').filter({ hasText: 'New query' }).click();
      const editor = page.locator('[data-testid="query-editor"]');
      await editor.waitFor();
      const temporaryName = `crest-playwright-${Date.now()}`;
      await editor.locator('input').first().fill(temporaryName);
      await editor.locator('textarea').fill('{"SqlQueryMetadata":{"Template":"select 1"}}');
      await editor.locator('button').filter({ hasText: 'Save' }).click();
      await page.getByText(temporaryName, { exact: true }).waitFor({ timeout: 20000 });
      await removeQuery(temporaryName);
    }

    await page.goto(`${baseUrl}/Admin/Queries/Sql/Query`, { waitUntil: 'domcontentloaded' });
    const legacyFrame = page.locator('iframe.legacy-admin-frame');
    await legacyFrame.waitFor({ timeout: 20000 });
    if (!(await legacyFrame.getAttribute('src'))?.includes('legacy-frame=1')) {
      throw new Error('The native SQL route was not loaded through the Crest legacy frame.');
    }
    if (await page.locator('[data-testid="queries-page"]').count()) {
      throw new Error('The Crest All Queries page intercepted Orchard’s SQL console route.');
    }

    console.log(JSON.stringify({ page: 'queries', sources: catalog.sources.length, queries: catalog.queries.length }));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
