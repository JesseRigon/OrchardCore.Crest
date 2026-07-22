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
  }
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  const messages = [];
  page.on('console', message => messages.push(`[${message.type()}] ${message.text()}`));
  page.on('pageerror', error => messages.push(`[pageerror] ${error.message}`));
  try {
    await login(page);
    const response = await page.request.get(`${baseUrl}/api/crest/content-items?pageSize=1`);
    if (!response.ok()) throw new Error(`Cannot load a real content item: ${response.status()}`);
    const list = await response.json();
    if (!list.items?.length) throw new Error('Expected a real Orchard content item for editor validation.');
    await page.goto(`${baseUrl}/Admin/Contents/ContentItems/${encodeURIComponent(list.items[0].contentItemId)}/Edit`, { waitUntil: 'networkidle' });
    await page.locator('[data-testid="content-item-editor"]').waitFor({ timeout: 20000 });
    await page.getByText('Content document', { exact: true }).waitFor({ timeout: 20000 });
    const json = await page.locator('textarea').inputValue();
    JSON.parse(json);
    const created = await page.request.post(`${baseUrl}/api/crest/content-items`, {
      data: { contentType: list.items[0].contentType, displayText: 'Crest editor probe', content: {}, publish: false },
    });
    if (!created.ok()) throw new Error(`Native content create failed: ${created.status()}`);
    const probe = await created.json();
    const deleted = await page.request.delete(`${baseUrl}/api/crest/content-items/${encodeURIComponent(probe.contentItemId)}`);
    if (!deleted.ok()) throw new Error(`Native content probe cleanup failed: ${deleted.status()}`);
    if (await page.locator('iframe').count()) throw new Error('Content editor rendered the legacy iframe.');
    const severe = messages.filter(line => /\[(error|pageerror)\]/i.test(line) && !/favicon|Failed to load resource.*404|browser refresh server|WebSocket connection to .*localhost/i.test(line));
    if (severe.length) throw new Error(`Unexpected browser errors:\n${severe.join('\n')}`);
    console.log(JSON.stringify({ contentItemId: list.items[0].contentItemId, jsonLength: json.length, createdAndDeleted: probe.contentItemId }, null, 2));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
// Playwright probe owned by OrchardCore.Crest.Admin.
