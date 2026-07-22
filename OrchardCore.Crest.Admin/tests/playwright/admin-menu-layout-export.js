const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const headed = process.env.HEADED === '1';
const repoRoot = path.resolve(__dirname, '..', '..', '..', '..', '..');
const exportFile = path.join(repoRoot, 'recipes', 'crest-admin-menu-layout.json');

async function login(page) {
  await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
  if (await page.locator('#UserName').count()) {
    await page.fill('#UserName', username);
    await page.fill('#Password', password);
    await page.locator('button:has-text("Login")').click();
    await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

async function main() {
  const browser = await chromium.launch({ headless: !headed });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  const consoleMessages = [];
  page.on('console', message => consoleMessages.push(`[${message.type()}] ${message.text()}`));
  page.on('pageerror', error => consoleMessages.push(`[pageerror] ${error.message}`));

  try {
    await login(page);
    await page.goto(`${baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
    await page.locator('.admin-shell').waitFor({ timeout: 20000 });
    await page.getByRole('heading', { name: 'Admin Menus', exact: true }).waitFor({ timeout: 20000 });

    const builtInMenu = page.getByRole('button', { name: /built-in/i }).first();
    if (await builtInMenu.count()) {
      await builtInMenu.click();
    }

    const button = page.getByRole('button', { name: /export layout json/i }).first();
    await button.waitFor({ timeout: 20000 });
    await button.click();

    await page.getByText('Sidebar layout exported', { exact: false }).waitFor({ timeout: 20000 });

    if (!fs.existsSync(exportFile)) {
      throw new Error(`Expected export file to exist: ${exportFile}`);
    }

    const exported = JSON.parse(fs.readFileSync(exportFile, 'utf8'));
    if (!Array.isArray(exported.items) || !Array.isArray(exported.customItems)) {
      throw new Error(`Export file does not contain items/customItems arrays: ${exportFile}`);
    }

    const severeConsole = consoleMessages.filter(line => /\[(error|pageerror)\]/i.test(line) && !/favicon|Failed to load resource.*404|browser refresh server|WebSocket connection to .*localhost/i.test(line));
    if (severeConsole.length) {
      throw new Error(`Unexpected browser errors:\n${severeConsole.join('\n')}`);
    }

    console.log(`exported ${exported.items.length} layout items and ${exported.customItems.length} custom items to ${exportFile}`);
  } catch (error) {
    console.log(`current url: ${page.url()}`);
    console.log(`body preview: ${(await page.locator('body').innerText().catch(() => '')).slice(0, 1600)}`);
    console.log(`console messages:\n${consoleMessages.join('\n')}`);
    throw error;
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
