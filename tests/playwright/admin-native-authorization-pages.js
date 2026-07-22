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
      await page.fill('#UserName', username); await page.fill('#Password', password);
      await page.press('#Password', 'Enter'); await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    }
    for (const [path, heading] of [['/Admin/ContentTypes/List', 'Content Types'], ['/Admin/Roles/Index', 'Roles'], ['/Admin/Settings/general', 'General Settings'], ['/Admin/Features', 'Features']]) {
      await page.goto(`${baseUrl}${path}`, { waitUntil: 'networkidle' });
      await page.locator('h4').filter({ hasText: heading }).first().waitFor({ timeout: 20000 });
      if (await page.locator('iframe').count()) throw new Error(`Legacy iframe rendered for ${path}`);
    }
    console.log(JSON.stringify({ native: true, protectedPages: 4 }));
  } finally { await browser.close(); }
}
main().catch(error => { console.error(error); process.exit(1); });
