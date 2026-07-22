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
    await page.goto(`${baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
    await page.getByRole('heading', { name: 'Admin Menus', exact: true }).waitFor({ timeout: 20000 });
    await page.getByRole('button', { name: /add node/i }).click();
    await page.locator('.admin-menu-node-editor').getByTitle('Choose icon').click();

    const dialog = page.locator('.icon-selector__dialog');
    await dialog.waitFor({ timeout: 15000 });
    await dialog.locator('.icon-selector__item svg').first().waitFor({ timeout: 30000 });
    const initialCount = await dialog.locator('.icon-selector__item').count();
    if (initialCount === 0) throw new Error('Remote Iconify fallback did not render any icon previews.');

    const search = dialog.getByPlaceholder('Search all icons...');
    await search.fill('home');
    await page.waitForFunction(() => Array.from(document.querySelectorAll('.icon-selector__item')).some(node => /home/i.test(node.getAttribute('title') || '')), null, { timeout: 30000 });
    const result = await dialog.locator('.icon-selector__item').first().evaluate(node => ({ title: node.getAttribute('title'), svg: Boolean(node.querySelector('svg')) }));
    if (!/home/i.test(result.title || '') || !result.svg) throw new Error(`Expected a remote home SVG result, got ${JSON.stringify(result)}.`);

    const severeConsole = consoleMessages.filter(line => /\[(error|pageerror)\]/i.test(line) && !/favicon|Failed to load resource.*404|browser refresh server|WebSocket connection to .*localhost/i.test(line));
    if (severeConsole.length) throw new Error(`Unexpected browser errors:\n${severeConsole.join('\n')}`);
    console.log(JSON.stringify({ initialCount, result }, null, 2));
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Icons.
