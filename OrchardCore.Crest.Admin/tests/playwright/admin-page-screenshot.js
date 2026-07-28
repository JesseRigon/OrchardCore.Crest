const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const route = process.env.ADMIN_ROUTE || '/Admin/Menus';
const output = process.env.OUTPUT || 'chat/admin-page-screenshot.png';
const headed = process.env.HEADED === '1';

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
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, deviceScaleFactor: 1 });
  const page = await context.newPage();
  page.on('console', message => console.log(`[browser:${message.type()}] ${message.text()}`));
  page.on('pageerror', error => console.log(`[browser:error] ${error.message}`));

  await login(page);
  await page.goto(`${baseUrl}${route}`, { waitUntil: 'networkidle' });
  await page.locator('body').waitFor({ timeout: 15000 });

  const summary = await page.evaluate(() => ({
    url: window.location.href,
    title: document.title,
    hasCrestShell: !!document.querySelector('.admin-shell'),
    hasPrimaryNavMenu: !!document.querySelector('.primary-nav-menu'),
    h1: Array.from(document.querySelectorAll('h1,h2,h3,.rz-text-h4,.rz-text-h5')).map(x => x.textContent?.trim()).filter(Boolean).slice(0, 10),
    bodyPreview: document.body.innerText.replace(/\s+/g, ' ').trim().slice(0, 500),
    activeItems: Array.from(document.querySelectorAll('.primary-nav-menu__item-content--active .rz-navigation-item-text')).map(x => x.textContent?.trim()).filter(Boolean)
  }));

  await page.screenshot({ path: output, fullPage: true });
  console.log(JSON.stringify(summary, null, 2));
  console.log(`screenshot: ${output}`);
  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
