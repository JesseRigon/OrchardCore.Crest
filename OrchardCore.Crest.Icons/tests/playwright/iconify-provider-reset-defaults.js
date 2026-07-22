const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const headed = process.env.HEADED === '1';

async function login(page) {
  await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
  if (await page.locator('#UserName').count()) {
    await page.fill('#UserName', username);
    await page.fill('#Password', password);
    await page.press('#Password', 'Enter');
    await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

async function main() {
  const browser = await chromium.launch({ headless: !headed });
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  await login(page);

  const result = await page.evaluate(async () => {
    const settings = {
      iconify: {
        enabled: true,
        baseUrl: 'https://api.iconify.design',
        apiKey: null,
        apiKeyHeader: null,
        prefixes: [],
      },
    };

    const response = await fetch('/api/crest/icons/providers', {
      method: 'PUT',
      credentials: 'include',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(settings),
    });
    const body = await response.json();
    return { status: response.status, ok: response.ok, body };
  });

  console.log(JSON.stringify(result, null, 2));
  if (!result.ok || result.body?.iconify?.baseUrl !== 'https://api.iconify.design' || result.body?.iconify?.prefixes?.length !== 0) {
    throw new Error(`Unable to reset Iconify defaults: ${result.status}`);
  }

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Icons.
