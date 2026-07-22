const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  try {
    await page.goto(`${baseUrl}/login`, { waitUntil: 'domcontentloaded' });
    if (await page.locator('#UserName').count()) {
      await page.fill('#UserName', username);
      await page.fill('#Password', password);
      await page.press('#Password', 'Enter');
      await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    }

    await page.goto(`${baseUrl}/Admin/Settings/SecurityHeaders`, { waitUntil: 'domcontentloaded' });
    await page.locator('[data-testid="security-headers-page"]').waitFor({ timeout: 20000 });
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
