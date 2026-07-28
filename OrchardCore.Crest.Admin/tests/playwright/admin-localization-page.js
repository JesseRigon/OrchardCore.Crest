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
    await page.locator('[data-testid="localization-page"]').waitFor({ timeout: 20000 });
    if (await page.locator('iframe').count()) throw new Error('Localization must render as a Crest Blazor page.');

    const cultureCards = page.locator('.localization-page__culture-card');
    if (await cultureCards.count() < 1) throw new Error('No supported culture cards were rendered.');

    const dropdowns = page.locator('.localization-page__culture-dropdown');
    if (await dropdowns.count() < 2) throw new Error('Expected Crest dropdowns for adding and selecting the default culture.');

    const defaultDropdown = dropdowns.nth(1);
    await defaultDropdown.locator('.crest-dropdown__trigger').click();
    const options = defaultDropdown.locator('[role="option"]');
    const defaultOptionCount = await options.count();
    if (defaultOptionCount < 1) throw new Error('The default-culture Crest dropdown has no supported-culture options.');
    await page.keyboard.press('Escape');

    console.log(JSON.stringify({ supportedCultures: await cultureCards.count(), defaultOptions: defaultOptionCount }));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
