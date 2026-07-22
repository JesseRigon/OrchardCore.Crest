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
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });

  page.on('console', message => console.log(`[browser:${message.type()}] ${message.text()}`));
  page.on('pageerror', error => console.log(`[browser:error] ${error.message}`));

  await login(page);
  await page.goto(`${baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
  await page.locator('h4', { hasText: 'Admin Menus' }).waitFor({ timeout: 20000 });
  await page.locator('.admin-menu-tree').waitFor({ timeout: 20000 });

  const state = await page.locator('.admin-menu-tree').evaluate(root => {
    const parentButtons = Array.from(root.querySelectorAll('.admin-menu-node__collapse'));
    const nestedLists = Array.from(root.querySelectorAll('.admin-menu-tree__list .admin-menu-tree__list'));
    return {
      parentButtonCount: parentButtons.length,
      expandedButtonCount: parentButtons.filter(button => button.textContent?.includes('expand_more')).length,
      collapsedButtonCount: parentButtons.filter(button => button.textContent?.includes('chevron_right')).length,
      nestedListCount: nestedLists.length,
      visibleNestedListCount: nestedLists.filter(list => {
        const rect = list.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
      }).length
    };
  });

  console.log(JSON.stringify(state, null, 2));

  if (state.parentButtonCount === 0) {
    throw new Error('Expected at least one parent menu node in the admin menu editor.');
  }
  if (state.expandedButtonCount !== 0 || state.visibleNestedListCount !== 0) {
    throw new Error(`Expected admin menu editor nodes to default collapsed, got ${JSON.stringify(state)}.`);
  }

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
