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
    await page.goto(`${baseUrl}/Admin/Settings/admin`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: 'Admin Settings', exact: true }).waitFor({ timeout: 20000 });

    const displayNewMenuControls = await page.getByText('Display New menu').count();
    if (displayNewMenuControls !== 0) {
      throw new Error('Display New menu toggle should not be rendered on Admin Settings.');
    }

    console.log(JSON.stringify({ displayNewMenuToggle: 'removed' }));
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
