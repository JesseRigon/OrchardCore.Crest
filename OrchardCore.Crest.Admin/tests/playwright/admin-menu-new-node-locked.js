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
    await page.locator('button:has-text("Login")').click();
    await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

async function main() {
  const browser = await chromium.launch({ headless: !headed });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  const consoleMessages = [];
  page.on('console', message => consoleMessages.push(`[${message.type()}] ${message.text()}`));
  page.on('pageerror', error => consoleMessages.push(`[pageerror] ${error.message}`));

  try {
    await login(page);
    await page.goto(`${baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
    await page.locator('.admin-shell').waitFor({ timeout: 20000 });
    await page.getByRole('heading', { name: 'Admin Menus', exact: true }).waitFor({ timeout: 20000 });
    await page.locator('.admin-menu-tree').waitFor({ timeout: 20000 });

    const state = await page.locator('.admin-menu-tree').evaluate(root => {
      const newItem = root.querySelector('li[data-node-id="new"]');
      if (!newItem) {
        return { found: false };
      }

      const newNode = newItem.querySelector(':scope > .admin-menu-node');
      const buttons = Array.from(newNode.querySelectorAll('button'));
      const actionButtons = buttons.filter(button => !button.classList.contains('admin-menu-node__collapse'));
      const childItems = Array.from(newItem.querySelectorAll(':scope > ol li[data-node-id]'));
      const lockedChildItems = childItems.filter(item => item.getAttribute('data-locked') === 'true');
      const handle = newNode.querySelector('.admin-menu-node__handle');

      return {
        found: true,
        locked: newItem.getAttribute('data-locked') === 'true',
        greyed: newNode.classList.contains('admin-menu-node--locked'),
        lockedBadge: newNode.textContent.includes('Locked'),
        draggable: handle?.getAttribute('draggable'),
        actionButtonCount: actionButtons.length,
        disabledActionButtonCount: actionButtons.filter(button => button.disabled || button.getAttribute('aria-disabled') === 'true').length,
        childItemCount: childItems.length,
        lockedChildItemCount: lockedChildItems.length,
      };
    });

    console.log(JSON.stringify(state, null, 2));

    if (!state.found) {
      throw new Error('Expected the New menu node to exist in the Primary Navigation editor.');
    }
    if (!state.locked || !state.greyed || !state.lockedBadge || state.draggable !== 'false') {
      throw new Error(`Expected New node to render locked and greyed out, got ${JSON.stringify(state)}.`);
    }
    if (state.actionButtonCount === 0 || state.disabledActionButtonCount !== state.actionButtonCount) {
      throw new Error(`Expected New node action buttons to be disabled, got ${JSON.stringify(state)}.`);
    }
    if (state.lockedChildItemCount !== state.childItemCount) {
      throw new Error(`Expected all New node children to be locked, got ${JSON.stringify(state)}.`);
    }

    const severeConsole = consoleMessages.filter(line => /\[(error|pageerror)\]/i.test(line) && !/favicon|Failed to load resource.*404|browser refresh server|WebSocket connection to .*localhost/i.test(line));
    if (severeConsole.length) {
      throw new Error(`Unexpected browser errors:\n${severeConsole.join('\n')}`);
    }
  } catch (error) {
    console.log(`current url: ${page.url()}`);
    console.log(`body preview: ${(await page.locator('body').innerText().catch(() => '')).slice(0, 1600)}`);
    console.log(`console messages:\n${consoleMessages.join('\n')}`);
    throw error;
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
