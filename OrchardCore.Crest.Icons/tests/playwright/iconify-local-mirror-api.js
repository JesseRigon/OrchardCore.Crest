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
  return await page.evaluate(async ({ path, options }) => {
    const response = await fetch(path, {
      credentials: 'include',
      headers: { 'content-type': 'application/json', ...(options.headers || {}) },
      ...options,
    });
    const text = await response.text();
    let body = null;
    try {
      body = text ? JSON.parse(text) : null;
    } catch {
      body = text;
    }

    return { status: response.status, ok: response.ok, body };
  }, { path, options });
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
  await login(page);

  const original = await api(page, '/api/crest/icons/providers');
  if (!original.ok) {
    throw new Error(`Could not read providers: ${original.status}`);
  }

  try {
    const publicSettings = {
      iconify: {
        enabled: true,
        baseUrl: 'https://api.iconify.design',
        apiKey: null,
        apiKeyHeader: null,
        prefixes: ['mdi'],
      },
    };
    const savePublic = await api(page, '/api/crest/icons/providers', {
      method: 'PUT',
      body: JSON.stringify(publicSettings),
    });
    if (!savePublic.ok) {
      throw new Error(`Could not save public Iconify settings: ${savePublic.status}`);
    }

    const status = await api(page, '/api/crest/icons/providers/iconify/local');
    if (!status.ok || !status.body?.isAvailable) {
      throw new Error(`Expected App_Data Iconify cache to be available, got: ${status.status} ${JSON.stringify(status.body)}`);
    }

    const localSearch = await api(page, '/api/crest/icons?library=iconify.mdi&query=home&skip=0&take=20');
    const homeIcon = localSearch.body?.items?.find(item => item.key === 'iconify.mdi/current/default/home' && item.svgMarkup?.includes('<svg'));
    if (!localSearch.ok || !homeIcon) {
      throw new Error(`Expected mdi:home from local Iconify cache, got: ${localSearch.status} ${JSON.stringify(localSearch.body).slice(0, 600)}`);
    }

    const customSettings = {
      iconify: {
        enabled: true,
        baseUrl: 'http://127.0.0.1:9',
        apiKey: null,
        apiKeyHeader: null,
        prefixes: ['mdi'],
      },
    };
    const saveCustom = await api(page, '/api/crest/icons/providers', {
      method: 'PUT',
      body: JSON.stringify(customSettings),
    });
    if (!saveCustom.ok) {
      throw new Error(`Could not save custom Iconify settings: ${saveCustom.status}`);
    }

    const customSearch = await api(page, '/api/crest/icons?library=iconify.mdi&query=home&skip=0&take=20');
    if (!customSearch.ok || customSearch.body?.items?.some(item => item.key === 'iconify.mdi/current/default/home')) {
      throw new Error('Custom Iconify server search unexpectedly used the public local cache.');
    }

    console.log(JSON.stringify({
      cacheAvailable: status.body.isAvailable,
      version: status.body.version,
      rootPath: status.body.rootPath,
      sourcePath: status.body.sourcePath,
      prefixCount: status.body.prefixCount,
      iconCount: status.body.iconCount,
      localSearchTotal: localSearch.body?.total,
      foundHome: Boolean(homeIcon),
      customSearchTotal: customSearch.body?.total,
    }, null, 2));
  } finally {
    await api(page, '/api/crest/icons/providers', {
      method: 'PUT',
      body: JSON.stringify(original.body),
    }).catch(error => console.error(`restore failed: ${error.message}`));
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Icons.
