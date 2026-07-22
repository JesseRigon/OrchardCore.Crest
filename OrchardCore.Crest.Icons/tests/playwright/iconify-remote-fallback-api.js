const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

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

async function api(page, path, options = {}) {
  return page.evaluate(async ({ path, options }) => {
    const response = await fetch(path, { credentials: 'include', headers: { 'content-type': 'application/json' }, ...options });
    const text = await response.text();
    return { status: response.status, ok: response.ok, body: text ? JSON.parse(text) : null };
  }, { path, options });
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  await login(page);

  const original = await api(page, '/api/crest/icons/providers');
  if (!original.ok) throw new Error(`Could not read Iconify provider settings: ${original.status}`);

  try {
    const save = await api(page, '/api/crest/icons/providers', {
      method: 'PUT',
      body: JSON.stringify({ iconify: { enabled: true, baseUrl: 'https://api.iconify.design', apiKey: null, apiKeyHeader: null, prefixes: ['mdi'] } }),
    });
    if (!save.ok) throw new Error(`Could not configure public Iconify: ${save.status}`);

    const status = await api(page, '/api/crest/icons/providers/iconify/local');
    if (!status.ok || status.body?.isAvailable || !/disabled for this build/i.test(status.body?.lastError || '')) {
      throw new Error(`Expected the Debug build to bypass the local cache, got ${status.status} ${JSON.stringify(status.body)}`);
    }

    const search = await api(page, '/api/crest/icons?library=iconify.mdi&query=home&skip=0&take=20');
    const home = search.body?.items?.find(item => item.key === 'iconify.mdi/current/default/home' && item.svgMarkup?.includes('<svg'));
    if (!search.ok || !home) {
      throw new Error(`Expected mdi:home through the remote Iconify fallback, got ${search.status} ${JSON.stringify(search.body).slice(0, 900)}`);
    }

    console.log(JSON.stringify({ localCacheAvailable: status.body.isAvailable, localCacheMessage: status.body.lastError, total: search.body.total, key: home.key }, null, 2));
  } finally {
    await api(page, '/api/crest/icons/providers', { method: 'PUT', body: JSON.stringify(original.body) }).catch(() => {});
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Icons.
