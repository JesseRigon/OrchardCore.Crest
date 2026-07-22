const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const headed = process.env.HEADED === '1';
const route = process.env.ADMIN_ROUTE || '/Admin/CRM/Customers';

async function login(page) {
  await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
  if (await page.locator('#UserName').count()) {
    await page.fill('#UserName', username);
    await page.fill('#Password', password);
    await page.locator('button:has-text("LOGIN"), button:has-text("Login")').click();
    await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

async function getAntiforgeryToken(page) {
  return await page.evaluate(async () => {
    const response = await fetch('/api/crest/antiforgery/token', { credentials: 'include' });
    if (!response.ok) throw new Error(`antiforgery failed: ${response.status}`);
    return await response.json();
  });
}

async function api(page, method, url, body) {
  const token = await getAntiforgeryToken(page);
  return await page.evaluate(async ({ method, url, body, token }) => {
    const response = await fetch(url, {
      method,
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        [token.headerName || 'RequestVerificationToken']: token.requestToken,
      },
      body: body === null || body === undefined ? undefined : JSON.stringify(body),
    });

    const text = await response.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch {}
    return { ok: response.ok, status: response.status, text, json };
  }, { method, url, body, token });
}

async function listCustomers(page) {
  return await page.evaluate(async () => {
    const response = await fetch('/api/crest/model/content-items?contentType=Customer&name=Customers&latest=true&limit=250', { credentials: 'include' });
    if (!response.ok) throw new Error(`list customers failed: ${response.status}`);
    return await response.json();
  });
}

async function cleanupPlaywrightCustomers(page) {
  const state = await listCustomers(page).catch(() => null);
  const items = state?.items || [];
  for (const item of items) {
    const contentItem = item.contentItem;
    if (contentItem?.contentItemId && contentItem.displayText?.includes('Playwright customer')) {
      await deleteContentItem(page, contentItem.contentItemId).catch(() => {});
    }
  }
}

async function createCustomer(page, displayText) {
  const result = await api(page, 'POST', '/api/crest/model/content-items', {
    contentType: 'Customer',
    displayText,
    publish: false,
  });
  if (!result.ok) throw new Error(`create failed: ${result.status} ${result.text}`);
  return result.json;
}

async function deleteContentItem(page, contentItemId) {
  if (!contentItemId) return;
  await api(page, 'DELETE', `/api/crest/model/content-items/${encodeURIComponent(contentItemId)}`, null);
}

async function rowFor(page, contentItemId) {
  const row = page.locator('.crest-model-list__item', { hasText: contentItemId }).first();
  await row.waitFor({ timeout: 15000 });
  await row.scrollIntoViewIfNeeded();
  await row.hover();
  return row;
}

async function main() {
  const browser = await chromium.launch({ headless: !headed });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  page.on('console', message => console.log(`[browser:${message.type()}] ${message.text()}`));
  page.on('pageerror', error => console.log(`[browser:error] ${error.message}`));

  try {
    await login(page);

    await cleanupPlaywrightCustomers(page);

    const seedName = `Playwright customer ${Date.now()}`;
    const seed = await createCustomer(page, seedName);
    const seedId = seed?.contentItem?.contentItemId;
    if (!seedId) throw new Error('create response did not include contentItem.contentItemId');
    console.log(`seed customer: ${seedId}`);

    await page.goto(`${baseUrl}${route}`, { waitUntil: 'networkidle' });
    await page.locator('.crest-model-list').waitFor({ timeout: 20000 });
    await page.waitForTimeout(1000);

    const initialUrl = page.url();
    const row = await rowFor(page, seedId);
    console.log(`edit button count: ${await row.locator('button[title="Edit"]').count()}`);

    await row.locator('button[title="Edit"]').click();
    await page.locator('.crest-content-item-editor').waitFor({ timeout: 15000 });
    await page.locator('.crest-form-manager').waitFor({ timeout: 15000 });
    console.log(`after edit url: ${page.url()}`);
    console.log(`editor visible: ${await page.locator('.crest-content-item-editor').isVisible()}`);
    console.log(`form manager visible: ${await page.locator('.crest-form-manager').isVisible()}`);
    console.log(`customer form field visible: ${await page.locator('.crest-content-item-editor', { hasText: 'Customer number' }).count() > 0}`);
    if (page.url() !== initialUrl) {
      throw new Error(`Edit should keep the user in the page flow. Expected ${initialUrl}, got ${page.url()}`);
    }

    await page.locator('.crest-content-item-editor input').first().fill(`${seedName} updated`);
    await page.locator('.crest-content-item-editor button:has-text("Save draft")').click();
    await page.waitForTimeout(1500);
    const updatedVisible = await page.locator('.crest-model-list__item', { hasText: `${seedName} updated` }).count();
    console.log(`updated row visible: ${updatedVisible > 0}`);

    const duplicateButton = page.locator('.crest-content-item-editor button:has-text("Duplicate")').first();
    await duplicateButton.click();
    await page.waitForTimeout(1500);
    const duplicateRows = await page.locator('.crest-model-list__item', { hasText: 'copy' }).count();
    console.log(`rows containing copy after duplicate: ${duplicateRows}`);

    const listRow = await rowFor(page, seedId);
    await listRow.locator('button[title="Duplicate"]').click();
    await page.waitForTimeout(1500);
    const rowCountAfterListDuplicate = await page.locator('.crest-model-list__item').count();
    console.log(`row count after list duplicate: ${rowCountAfterListDuplicate}`);

    const deleteRow = await rowFor(page, seedId);
    await deleteRow.locator('button[title="Delete"]').click();
    await page.waitForTimeout(1500);
    const deletedStillVisible = await page.locator('.crest-model-list__item', { hasText: seedId }).count();
    console.log(`seed row visible after list delete: ${deletedStillVisible > 0}`);
    if (deletedStillVisible > 0) {
      throw new Error('Expected list delete action to remove the seed customer row.');
    }

    const alerts = await page.locator('.rz-alert-danger').count();
    console.log(`danger alerts: ${alerts}`);
    if (alerts > 0) {
      throw new Error('Unexpected danger alert after model list/editor actions.');
    }
  } finally {
    await cleanupPlaywrightCustomers(page).catch(() => {});
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
