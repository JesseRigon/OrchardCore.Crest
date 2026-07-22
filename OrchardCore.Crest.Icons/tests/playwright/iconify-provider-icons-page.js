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
  const browser = await chromium.launch({ headless: !headed });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });

  page.on('console', message => console.log(`[browser:${message.type()}] ${message.text()}`));
  page.on('pageerror', error => console.log(`[browser:error] ${error.message}`));
  page.on('response', async response => {
    if (!response.url().includes('/api/crest/icons')) return;
    console.log(`[icons-api] ${response.status()} ${response.url()}`);
  });

  await login(page);

  const original = await api(page, '/api/crest/icons/providers');
  if (!original.ok || !original.body?.iconify) {
    throw new Error(`Unable to load icon provider settings: ${original.status}`);
  }

  const configured = {
    iconify: {
      enabled: true,
      baseUrl: 'https://api.iconify.design',
      apiKey: null,
      apiKeyHeader: null,
      prefixes: ['mdi'],
    },
  };

  const save = await api(page, '/api/crest/icons/providers', {
    method: 'PUT',
    body: JSON.stringify(configured),
  });
  if (!save.ok || save.body?.iconify?.baseUrl !== configured.iconify.baseUrl || save.body?.iconify?.prefixes?.[0] !== 'mdi') {
    throw new Error(`Unable to save Iconify provider test settings: ${save.status} ${JSON.stringify(save.body).slice(0, 500)}`);
  }

  await page.goto(`${baseUrl}/Admin/Design/Icons`, { waitUntil: 'networkidle' });
  await page.locator('h4', { hasText: 'Icons' }).waitFor({ timeout: 20000 });
  await page.getByText('Iconify', { exact: true }).waitFor({ timeout: 10000 });
  await page.getByText('Provider search preview').waitFor({ timeout: 10000 });
  if (await page.getByRole('button', { name: /update public iconify cache|sync public iconify/i }).count()) {
    throw new Error('Icons admin page must not expose a manual Iconify cache update button.');
  }

  const remoteSearch = await api(page, '/api/crest/icons?library=iconify&query=home&skip=0&take=20');
  const iconifyItem = remoteSearch.body?.items?.find(item =>
    item.providerId === 'iconify' &&
    item.key === 'iconify.mdi/current/default/home' &&
    item.svgMarkup?.includes('<svg'));
  console.log(JSON.stringify({
    providerEnabled: save.body.iconify.enabled,
    providerBaseUrl: save.body.iconify.baseUrl,
    providerPrefixes: save.body.iconify.prefixes,
    searchTotal: remoteSearch.body?.total,
    foundHome: Boolean(iconifyItem),
  }, null, 2));
  if (!remoteSearch.ok || !iconifyItem) {
    throw new Error(`Iconify search did not return mdi home SVG: ${remoteSearch.status} ${JSON.stringify(remoteSearch.body).slice(0, 800)}`);
  }

  await page.getByTitle('Choose icon').click();
  const dialog = page.locator('.icon-selector__dialog');
  await dialog.waitFor({ timeout: 15000 });
  await dialog.locator('.icon-selector__filters').waitFor({ timeout: 10000 });
  await dialog.getByRole('button', { name: 'Iconify' }).click();
  const searchBox = dialog.getByPlaceholder('Search all icons...');
  await searchBox.click();
  await searchBox.pressSequentially('home');
  await page.locator('.icon-selector__item[title="@iconify:mdi:home"]').waitFor({ timeout: 30000 });
  await page.locator('.icon-selector__item[title="@iconify:mdi:home"]').click();
  await page.getByText('Selected icon: @iconify:mdi:home').waitFor({ timeout: 10000 });

  const restore = await api(page, '/api/crest/icons/providers', {
    method: 'PUT',
    body: JSON.stringify(original.body),
  });
  if (!restore.ok) {
    throw new Error(`Iconify provider settings were validated, but restore failed: ${restore.status}`);
  }

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Icons.
