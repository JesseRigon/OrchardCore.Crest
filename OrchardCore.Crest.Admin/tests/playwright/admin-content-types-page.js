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
    await page.goto(`${baseUrl}/Admin/ContentTypes/List`, { waitUntil: 'networkidle' });
    await page.locator('[data-testid="content-types-page"]').waitFor({ timeout: 20000 });
    await page.getByRole('heading', { name: 'Content Types', exact: true, level: 4 }).waitFor({ timeout: 20000 });

    const typeRows = page.locator('.crest-model-list__item');
    const typeCount = await typeRows.count();
    if (typeCount < 1) throw new Error('Expected at least one Orchard content type.');

    const selectedName = await typeRows.first().locator('.crest-model-list__item-title').innerText();
    await typeRows.first().click();
    await page.locator('[data-testid="content-type-detail"]').waitFor({ timeout: 10000 });
    await page.getByRole('heading', { name: selectedName, exact: true, level: 5 }).waitFor({ timeout: 10000 });

    const search = page.getByPlaceholder('Search...');
    await search.fill(selectedName);
    await search.press('Tab');
    await page.waitForTimeout(200);
    const filteredCount = await typeRows.count();
    if (filteredCount < 1 || filteredCount >= typeCount) {
      throw new Error(`Expected content-type filter to narrow the list: ${JSON.stringify({ typeCount, filteredCount, selectedName })}`);
    }

    const legacyFrame = await page.locator('iframe').count();
    if (legacyFrame) throw new Error('Content Types route rendered a legacy iframe.');

    const severeConsole = consoleMessages.filter(line => /\[(error|pageerror)\]/i.test(line) && !/favicon|Failed to load resource.*404|browser refresh server|WebSocket connection to .*localhost/i.test(line));
    if (severeConsole.length) throw new Error(`Unexpected browser errors:\n${severeConsole.join('\n')}`);

    console.log(JSON.stringify({ typeCount, filteredCount, selectedName }, null, 2));
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
