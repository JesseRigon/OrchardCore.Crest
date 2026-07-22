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
  const consoleMessages = [];
  page.on('console', message => consoleMessages.push(`[${message.type()}] ${message.text()}`));
  page.on('pageerror', error => consoleMessages.push(`[pageerror] ${error.message}`));

  try {
    await login(page);
    await page.goto(`${baseUrl}/Admin/ContentTypes/ListParts`, { waitUntil: 'networkidle' });
    await page.locator('[data-testid="content-parts-page"]').waitFor({ timeout: 20000 });
    await page.getByRole('heading', { name: 'Content Parts', exact: true, level: 4 }).waitFor({ timeout: 20000 });

    const rows = page.locator('.crest-model-list__item');
    const count = await rows.count();
    if (count < 1) throw new Error('Expected attached Orchard content parts.');
    const selectedName = await rows.first().locator('.crest-model-list__item-title').innerText();
    await rows.first().click();
    await page.locator('[data-testid="content-part-detail"]').waitFor({ timeout: 10000 });
    await page.getByRole('heading', { name: selectedName, exact: true, level: 5 }).waitFor({ timeout: 10000 });

    const search = page.getByPlaceholder('Search...');
    await search.fill(selectedName);
    await search.press('Tab');
    await page.waitForTimeout(200);
    const filteredCount = await rows.count();
    if (filteredCount < 1 || filteredCount >= count) throw new Error(`Expected part filter to narrow list: ${JSON.stringify({ count, filteredCount, selectedName })}`);
    if (await page.locator('iframe').count()) throw new Error('Content Parts route rendered a legacy iframe.');

    const severeConsole = consoleMessages.filter(line => /\[(error|pageerror)\]/i.test(line) && !/favicon|Failed to load resource.*404|browser refresh server|WebSocket connection to .*localhost/i.test(line));
    if (severeConsole.length) throw new Error(`Unexpected browser errors:\n${severeConsole.join('\n')}`);
    console.log(JSON.stringify({ count, filteredCount, selectedName }, null, 2));
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
