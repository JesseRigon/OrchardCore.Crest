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
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

function flatten(items, level = 0, result = []) {
  for (const item of items || []) {
    result.push({
      level,
      text: item.text,
      id: item.id,
      link: item.href || item.url || null,
      classes: item.classes || [],
      icon: item.icon ? {
        library: item.icon.library,
        version: item.icon.version,
        name: item.icon.name,
        hasSvg: !!item.icon.svgMarkup
      } : null
    });
    flatten(item.items, level + 1, result);
  }
  return result;
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  await login(page);

  const response = await page.request.get(`${baseUrl}/api/crest/navigation/admin`);
  console.log(`status: ${response.status()}`);
  const menu = await response.json();
  const maxLevel = Number.parseInt(process.env.MAX_LEVEL || '99', 10);
  const rows = flatten(menu.items).filter(item => item.level <= maxLevel);
  for (const row of rows) {
    console.log(`${'  '.repeat(row.level)}- ${row.text} id=${row.id || ''} link=${row.link || ''} classes=${JSON.stringify(row.classes)} icon=${row.icon ? `${row.icon.library}/${row.icon.version}/${row.icon.name}/svg=${row.icon.hasSvg}` : 'null'}`);
  }

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
