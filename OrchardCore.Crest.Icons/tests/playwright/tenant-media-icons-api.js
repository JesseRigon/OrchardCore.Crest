const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const headed = process.env.HEADED === '1';
const featureId = 'OrchardCore.Crest.Icons.TenantMedia';
const iconFileName = `codex-tenant-icon-${Date.now()}.svg`;
const iconName = iconFileName.replace(/\.svg$/i, '');
const svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M12 3 3 8l9 5 9-5-9-5Zm-7 8v5l7 5 7-5v-5l-7 4-7-4Z"/></svg>';

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
    const response = await fetch(path, { credentials: 'include', ...options });
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

async function uploadIcon(page) {
  return await page.evaluate(async ({ iconFileName, svg }) => {
    const form = new FormData();
    form.append('file', new Blob([svg], { type: 'image/svg+xml' }), iconFileName);
    form.append('overwrite', 'true');

    const response = await fetch('/api/crest/icons/tenant', {
      method: 'POST',
      credentials: 'include',
      body: form,
    });

    const text = await response.text();
    return {
      status: response.status,
      ok: response.ok,
      body: text ? JSON.parse(text) : null,
    };
  }, { iconFileName, svg });
}

async function main() {
  const browser = await chromium.launch({ headless: !headed });
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });

  page.on('console', message => console.log(`[browser:${message.type()}] ${message.text()}`));
  page.on('pageerror', error => console.log(`[browser:error] ${error.message}`));

  await login(page);

  const enable = await api(page, `/api/crest/features/${encodeURIComponent(featureId)}/enable`, { method: 'POST' });
  console.log(`enable ${featureId}: ${enable.status} ok=${enable.ok}`);
  if (!enable.ok && enable.status !== 204) {
    throw new Error(`Unable to enable ${featureId}: ${enable.status} ${JSON.stringify(enable.body).slice(0, 500)}`);
  }

  await page.waitForTimeout(3000);

  const upload = await uploadIcon(page);
  console.log(`upload tenant icon: ${upload.status} ok=${upload.ok}`);
  if (!upload.ok || upload.body?.name !== iconName) {
    throw new Error(`Tenant icon upload failed: ${upload.status} ${JSON.stringify(upload.body).slice(0, 500)}`);
  }

  const list = await api(page, '/api/crest/icons/tenant');
  const listed = Array.isArray(list.body) && list.body.some(icon => icon.name === iconName && icon.key === `tenant/current/default/${iconName}`);
  console.log(`list has uploaded icon: ${listed}`);
  if (!listed) {
    throw new Error(`Tenant icon list did not include ${iconName}.`);
  }

  const search = await api(page, `/api/crest/icons?library=tenant&query=${encodeURIComponent(iconName)}&skip=0&take=20`);
  const found = search.ok && Array.isArray(search.body?.items) && search.body.items.some(icon => icon.key === `tenant/current/default/${iconName}` && Boolean(icon.svgMarkup));
  console.log(`search found uploaded icon: ${found}`);
  if (!found) {
    throw new Error(`Tenant icon search did not include ${iconName}.`);
  }

  const remove = await api(page, `/api/crest/icons/tenant/${encodeURIComponent(iconName)}`, { method: 'DELETE' });
  console.log(`delete tenant icon: ${remove.status} ok=${remove.ok}`);
  if (!remove.ok) {
    throw new Error(`Tenant icon delete failed: ${remove.status}`);
  }

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Icons.
