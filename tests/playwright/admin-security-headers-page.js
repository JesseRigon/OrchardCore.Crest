const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  const browserErrors = [];
  page.on('pageerror', error => browserErrors.push(error.message));
  try {
    await page.goto(`${baseUrl}/login`, { waitUntil: 'domcontentloaded' });
    const userNameInput = page.locator('input[name="UserName"]');
    await userNameInput.waitFor({ timeout: 20000 });
    await userNameInput.fill(username);
    await page.locator('input[name="Password"]').fill(password);
    await page.getByRole('button', { name: 'Login', exact: true }).click();
    await page.waitForURL(/\/admin/i, { timeout: 15000 });
    await page.locator('.admin-shell').waitFor({ timeout: 20000 });

    const directResponse = await page.goto(`${baseUrl}/Admin/Settings/SecurityHeaders`, { waitUntil: 'domcontentloaded' });
    try {
      await page.locator('[data-testid="security-headers-page"]').waitFor({ timeout: 20000 });
    } catch (error) {
      await page.screenshot({ path: 'chat/security-headers-failure.png', fullPage: true });
      throw new Error(`${error.message}\nURL: ${page.url()}\nRoute response: ${directResponse?.status()} ${directResponse?.url()}\nBrowser errors: ${browserErrors.join(' | ')}`);
    }
    for (const label of ['Content Security Policy', 'Permissions Policy', 'Referrer Policy']) {
      await page.getByText(label, { exact: true }).first().waitFor();
    }
    await page.screenshot({ path: 'chat/security-headers-page.png', fullPage: true });

    const responsePromise = page.waitForResponse(response =>
      response.url().includes('/api/crest/security-headers') && response.request().method() === 'PUT');
    await page.getByRole('button', { name: 'Save' }).click();
    const response = await responsePromise;
    if (!response.ok()) throw Error(`Security headers save failed: ${response.status()}`);
    if (!response.request().headers().requestverificationtoken) {
      throw Error('Unsafe Crest request was missing Orchard antiforgery token');
    }
    console.log(JSON.stringify({ native: true, sections: 3, antiforgery: true, url: page.url() }));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
