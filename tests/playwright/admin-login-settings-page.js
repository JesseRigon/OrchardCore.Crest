const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  try {
    await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
    if (await page.locator('#UserName').count()) {
      await page.fill('#UserName', username);
      await page.fill('#Password', password);
      await page.press('#Password', 'Enter');
      await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    }

    await page.goto(`${baseUrl}/Admin/Settings/userLogin`, { waitUntil: 'networkidle' });
    await page.locator('[data-testid="login-settings-page"]').waitFor({ timeout: 20000 });
    await page.getByRole('heading', { name: 'User Login Settings' }).waitFor({ timeout: 20000 });
    await page.getByText('Allow user to be remembered during login').waitFor();
    await page.getByText('Two-Factor Authentication', { exact: true }).waitFor();
    await page.getByText('External Login', { exact: true }).waitFor();
    if (await page.locator('iframe').count()) throw new Error('Legacy iframe rendered');
    console.log(JSON.stringify({ native: true }));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
