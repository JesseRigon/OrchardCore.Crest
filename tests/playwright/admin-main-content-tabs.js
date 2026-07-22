const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  page.setDefaultTimeout(20000);
  try {
    await page.goto(`${baseUrl}/login`, { waitUntil: 'domcontentloaded' });
    await page.locator('input[name="UserName"]').waitFor({ timeout: 20000 });
    await page.locator('input[name="UserName"]').fill(username);
    await page.locator('input[name="Password"]').fill(password);
    await page.getByRole('button', { name: 'Login', exact: true }).click();
    await page.waitForURL(/\/admin/i, { timeout: 15000 });
    await page.locator('.admin-shell').waitFor({ timeout: 20000 });

    for (const [path, labels] of [
      ['/Admin/Settings/SecurityHeaders', ['Content Security Policy', 'Permissions Policy', 'Referrer Policy']],
      ['/Admin/Settings/admin', ['Admin', 'Site Map']],
      ['/Admin/Settings/general', ['General', 'Resources', 'Cache']],
    ]) {
      await page.goto(`${baseUrl}${path}`, { waitUntil: 'domcontentloaded' });
      const tabs = page.locator('.crest-main-content-tabs');
      await tabs.waitFor({ timeout: 20000 });
      for (const label of labels) {
        await tabs.locator('[role="tab"]').filter({ hasText: label }).waitFor();
      }
      const lastTab = tabs.locator('[role="tab"]').filter({ hasText: labels.at(-1) });
      await lastTab.click();
      if (await lastTab.getAttribute('aria-selected') !== 'true') throw new Error(`Tab did not become selected: ${labels.at(-1)}`);
    }

    console.log(JSON.stringify({ component: 'CrestMainContentTabs', pages: 3 }));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
