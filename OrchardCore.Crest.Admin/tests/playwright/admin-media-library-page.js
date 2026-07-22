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
  }
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  const messages = [];
  page.on('console', message => messages.push(`[${message.type()}] ${message.text()}`));
  page.on('pageerror', error => messages.push(`[pageerror] ${error.message}`));
  try {
    await login(page);
    await page.goto(`${baseUrl}/Admin/Media`, { waitUntil: 'networkidle' });
    await page.locator('[data-testid="media-library-page"]').waitFor({ timeout: 20000 });
    await page.getByRole('heading', { name: 'Media Library', exact: true, level: 4 }).waitFor({ timeout: 20000 });
    await page.locator('[data-testid="media-breadcrumbs"]').waitFor({ timeout: 20000 });
    if (await page.locator('iframe').count()) throw new Error('Media Library rendered the legacy iframe.');
    const empty = await page.getByText('This folder is empty. Upload a file or create a folder to get started.').count();
    const grid = await page.locator('[data-testid="media-library-grid"]').count();
    if (!empty && !grid) throw new Error('Expected either media entries or the native empty state.');
    const severe = messages.filter(line => /\[(error|pageerror)\]/i.test(line) && !/favicon|Failed to load resource.*404|browser refresh server|WebSocket connection to .*localhost/i.test(line));
    if (severe.length) throw new Error(`Unexpected browser errors:\n${severe.join('\n')}`);
    console.log(JSON.stringify({ empty: Boolean(empty), grid: Boolean(grid) }, null, 2));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
// Playwright probe owned by OrchardCore.Crest.Admin.
