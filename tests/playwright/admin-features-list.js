const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function login(page) {
  await page.goto(`${baseUrl}/Login?ReturnUrl=%2FAdmin`, { waitUntil: 'domcontentloaded' });

  const userNameInput = page.locator('input[name="UserName"], #UserName').first();
  await userNameInput.waitFor({ timeout: 20000 });
  await userNameInput.fill(username);
  await page.locator('input[name="Password"], #Password').first().fill(password);
  await page.locator('input[name="Password"], #Password').first().press('Enter');
  await page.waitForURL(/\/admin/i, { timeout: 20000 }).catch(() => {});

  await page.goto(`${baseUrl}/Admin`, { waitUntil: 'domcontentloaded' });
  if (/login/i.test(page.url())) {
    throw new Error(`Login did not establish an admin session; current URL is ${page.url()}`);
  }
}

async function main() {
  const browser = await chromium.launch({ headless: true });

  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    await login(page);

    const featuresResponse = page.waitForResponse(response =>
      response.url().includes('/api/crest/features') && response.request().method() === 'GET',
      { timeout: 30000 }
    );

    await page.goto(`${baseUrl}/Admin/Features`, { waitUntil: 'domcontentloaded' });
    await page.locator('[data-testid="features-page"]').waitFor({ timeout: 30000 });

    const response = await featuresResponse;
    if (!response.ok()) {
      throw new Error(`Features API returned ${response.status()}: ${await response.text()}`);
    }

    const features = await response.json();
    const ids = features
      .map(feature => feature.id)
      .filter(Boolean)
      .sort((left, right) => left.localeCompare(right));

    console.log(JSON.stringify({ count: ids.length, ids }, null, 2));
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
