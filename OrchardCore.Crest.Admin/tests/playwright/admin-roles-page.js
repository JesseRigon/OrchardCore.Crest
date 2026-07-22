const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function login(page) {
  await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
  if (await page.locator('#UserName').count()) {
    await page.fill('#UserName', username);
    await page.fill('#Password', password);
    await page.press('#Password', 'Enter');
    await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  try {
    await login(page);
    await page.goto(`${baseUrl}/Admin/Roles/Index`, { waitUntil: 'networkidle' });
    await page.locator('[data-testid="roles-page"]').waitFor({ timeout: 20000 });
    await page.getByRole('heading', { name: 'Roles', exact: true, level: 4 }).waitFor({ timeout: 20000 });
    const rows = page.locator('.crest-model-list__item');
    const count = await rows.count();
    if (count < 1) throw new Error('Expected one or more Orchard roles.');
    const name = await rows.first().locator('.crest-model-list__item-title').innerText();
    await rows.first().click();
    await page.locator('[data-testid="role-detail"]').waitFor({ timeout: 10000 });
    await page.getByRole('heading', { name, exact: true, level: 5 }).waitFor({ timeout: 10000 });
    if (await page.locator('iframe').count()) throw new Error('Roles rendered a legacy iframe.');
    console.log(JSON.stringify({ count, name }, null, 2));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
// Playwright probe owned by OrchardCore.Crest.Admin.
