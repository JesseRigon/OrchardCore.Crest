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
    await page.locator('input[name="UserName"]').waitFor({ timeout: 20000 });
    await page.locator('input[name="UserName"]').fill(username);
    await page.locator('input[name="Password"]').fill(password);
    await page.getByRole('button', { name: 'Login', exact: true }).click();
    await page.waitForURL(/\/admin/i, { timeout: 15000 });
    await page.locator('.admin-shell').waitFor({ timeout: 20000 });

    const response = await page.goto(`${baseUrl}/Admin/Settings/SecurityHeaders`, { waitUntil: 'domcontentloaded' });
    const html = await response.text();
    await page.locator('[data-testid="security-headers-page"]').waitFor({ timeout: 20000 });
    const renderedHeading = await page.locator('[data-testid="security-headers-page"] h4').innerText();
    const tabs = page.locator('[data-testid="security-headers-tabs"]');
    await tabs.locator('[role="tab"]').first().waitFor({ timeout: 10000 });
    await page.screenshot({ path: 'chat/security-headers-rendered.png', fullPage: true });

    console.log(JSON.stringify({
      status: response.status(),
      contentType: response.headers()['content-type'],
      finalUrl: page.url(),
      hasBlazorHost: html.includes('<div id="app">Loading...</div>'),
      hasNativeSecurityHeadersMarkup: html.includes('Content-Security-Policy'),
      renderedHeading,
      tabText: (await tabs.innerText()).slice(0, 300),
      tabHtml: (await tabs.innerHTML()).slice(0, 1000),
      browserErrors,
    }));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
