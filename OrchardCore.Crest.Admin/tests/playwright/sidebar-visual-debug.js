const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const route = process.env.ADMIN_ROUTE || '/Admin/CRM/Customers';

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
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, deviceScaleFactor: 1 });
  page.on('console', message => console.log(`[browser:${message.type()}] ${message.text()}`));
  page.on('pageerror', error => console.log(`[browser:error] ${error.message}`));
  await login(page);
  await page.goto(`${baseUrl}${route}`, { waitUntil: 'networkidle' });
  await page.locator('.admin-menu-sidebar').waitFor({ timeout: 15000 });

  const out = 'chat/playwright/sidebar-visual-debug.png';
  await page.locator('.admin-menu-sidebar').screenshot({ path: out });

  const details = await page.locator('.admin-menu-sidebar .admin-menu-sidebar__item-content').evaluateAll(items => items.map(item => {
    const text = item.textContent?.trim() || '';
    const icon = item.querySelector('.orchard-icon');
    const svg = icon?.querySelector('svg');
    const iconRect = icon?.getBoundingClientRect();
    const svgRect = svg?.getBoundingClientRect();
    const iconStyle = icon ? getComputedStyle(icon) : null;
    const svgStyle = svg ? getComputedStyle(svg) : null;
    return {
      text,
      icon: icon ? {
        library: icon.getAttribute('data-icon-library'),
        version: icon.getAttribute('data-icon-version'),
        name: icon.getAttribute('data-icon-name'),
        rect: iconRect ? { x: iconRect.x, y: iconRect.y, width: iconRect.width, height: iconRect.height } : null,
        color: iconStyle?.color,
        display: iconStyle?.display,
        visibility: iconStyle?.visibility,
        opacity: iconStyle?.opacity,
        svgRect: svgRect ? { x: svgRect.x, y: svgRect.y, width: svgRect.width, height: svgRect.height } : null,
        svgColor: svgStyle?.color,
        svgDisplay: svgStyle?.display,
        html: icon.outerHTML.replace(/\s+/g, ' ').slice(0, 400)
      } : null
    };
  }));

  console.log(`screenshot: ${out}`);
  console.log(JSON.stringify(details.filter(x => /CRM|Customers|Content|Design|Platform|New/i.test(x.text)).slice(0, 20), null, 2));
  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
