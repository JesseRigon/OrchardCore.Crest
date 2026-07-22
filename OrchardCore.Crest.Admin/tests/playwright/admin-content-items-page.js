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
    await page.goto(`${baseUrl}/Admin/Contents/ContentItems`, { waitUntil: 'networkidle' });
    await page.locator('[data-testid="content-items-page"]').waitFor({ timeout: 20000 });
    await page.getByRole('heading', { name: 'Content Items', exact: true, level: 4 }).waitFor({ timeout: 20000 });
    await page.locator('[data-testid="content-items-grid"]').waitFor({ timeout: 20000 });
    const rows = page.locator('[data-testid="content-items-grid"] tbody tr');
    const count = await rows.count();
    if (count < 1) throw new Error('Expected current Orchard content items.');
    const title = (await rows.first().locator('td').first().innerText()).split('\n')[0].trim();
    const search = page.getByPlaceholder('Search content titles...');
    await search.fill(title);
    await page.waitForTimeout(700);
    const filtered = await rows.count();
    if (filtered < 1 || filtered > count) throw new Error(`Unexpected content title filtering: ${JSON.stringify({ count, filtered, title })}`);
    if (await page.locator('iframe').count()) throw new Error('Content Items rendered the legacy iframe.');
    const severeConsole = consoleMessages.filter(line => /\[(error|pageerror)\]/i.test(line) && !/favicon|Failed to load resource.*404|browser refresh server|WebSocket connection to .*localhost/i.test(line));
    if (severeConsole.length) throw new Error(`Unexpected browser errors:\n${severeConsole.join('\n')}`);
    console.log(JSON.stringify({ count, filtered, title }, null, 2));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
// Playwright probe owned by OrchardCore.Crest.Admin.
