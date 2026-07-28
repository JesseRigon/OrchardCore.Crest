const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function login(page) {
  await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
  if (await page.locator('#UserName').count()) {
    await page.fill('#UserName', username);
    await page.fill('#Password', password);
    await page.locator('button:has-text("Login")').click();
    await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
  }
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  try {
    await login(page);
    await page.goto(`${baseUrl}/Admin/Settings/localization`, { waitUntil: 'networkidle' });
    await page.locator('.admin-titlebar').waitFor({ timeout: 20000 });

    const tenantSelector = page.locator('.admin-titlebar__tenant-selector');
    if (await tenantSelector.count() !== 1) {
      throw new Error('Tenant selector must use one CrestDropDown instance.');
    }
    if (await page.locator('.admin-titlebar__tenant-menu').count()) {
      throw new Error('Legacy Crest tenant selector is still rendered.');
    }

    const cultureSelector = page.locator('.admin-titlebar__culture-selector');
    if (await cultureSelector.count()) {
      const iconCount = await cultureSelector.locator('.crest-dropdown__trigger .orchard-icon').count();
      if (iconCount < 2) {
        throw new Error(`Culture trigger must render its selected culture icon and Crest chevron; found ${iconCount}.`);
      }
    }

    const tenantBackground = await tenantSelector.locator('.crest-dropdown__trigger').evaluate(element => getComputedStyle(element).backgroundColor);
    if (tenantBackground !== 'rgba(0, 0, 0, 0)') {
      throw new Error(`Tenant selector must not use an accent background: ${tenantBackground}`);
    }

    console.log(JSON.stringify({ tenantBackground }));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
