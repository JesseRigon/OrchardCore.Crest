const { chromium } = require('playwright');
const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
async function main() {
  const browser = await chromium.launch({ headless: true }); const page = await browser.newPage(); const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  try {
    await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
    if (await page.locator('#UserName').count()) { await page.fill('#UserName', username); await page.fill('#Password', password); await page.press('#Password', 'Enter'); await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {}); }
    await page.goto(`${baseUrl}/Admin/Media/Options`, { waitUntil: 'networkidle' });
    await page.locator('[data-testid="media-options-page"]').waitFor({ timeout: 20000 });
    await page.getByText('Supported sizes', { exact: true }).waitFor({ timeout: 20000 });
    if (await page.locator('iframe').count()) throw new Error('Media Options rendered a legacy iframe.');
    if (errors.length) throw new Error(errors.join('\n'));
    console.log(JSON.stringify({ native: true }));
  } finally { await browser.close(); }
}
main().catch(error => { console.error(error); process.exit(1); });
// Playwright probe owned by OrchardCore.Crest.Admin.
