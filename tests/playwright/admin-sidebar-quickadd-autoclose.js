const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function login(page) {
  await page.goto(`${baseUrl}/login`, { waitUntil: 'domcontentloaded' });
  await page.locator('input[name="UserName"]').waitFor({ timeout: 20000 });
  await page.locator('input[name="UserName"]').fill(username);
  await page.locator('input[name="Password"]').fill(password);
  await page.getByRole('button', { name: 'Login', exact: true }).click();
  await page.waitForURL(/\/admin/i, { timeout: 20000 });
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1366, height: 900 } });
    await login(page);
    await page.goto(`${baseUrl}/Admin`, { waitUntil: 'domcontentloaded' });

    const sidebar = page.locator('[data-testid="admin-menu-sidebar"]');
    await sidebar.waitFor({ timeout: 20000 });

    const quickAddButton = sidebar.locator('.admin-menu-sidebar__quickadd-button').first();
    await quickAddButton.waitFor({ timeout: 10000 });
    await quickAddButton.click();

    const popover = sidebar.locator('.admin-menu-sidebar__quickadd-popover');
    await popover.waitFor({ timeout: 10000 });

    await page.mouse.move(640, 320);
    await popover.waitFor({ state: 'detached', timeout: 10000 });

    console.log(JSON.stringify({ quickAddAutoClose: 'ok' }));
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
