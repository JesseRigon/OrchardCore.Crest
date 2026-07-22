const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const menuId = '__crest_default_admin_menu';
const targetNodeId = process.env.NODE_ID || 'content';
const testIconClass = process.env.ICON_CLASS || '@iconify:mdi:folder';

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

function flatten(nodes) {
  return nodes.flatMap(node => [node, ...flatten(node.items || [])]);
}

async function api(page, path, options = {}) {
  const response = await page.evaluate(async ({ path, options }) => {
    const res = await fetch(path, {
      credentials: 'include',
      headers: { 'content-type': 'application/json', ...(options.headers || {}) },
      ...options,
    });
    return { ok: res.ok, status: res.status, text: await res.text() };
  }, { path, options });
  if (!response.ok) throw new Error(`${path} failed ${response.status}: ${response.text}`);
  return response.text ? JSON.parse(response.text) : null;
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  await login(page);

  const state = await api(page, '/api/crest/admin-menus');
  const menu = state.menus.find(menu => menu.id === menuId);
  if (!menu) throw new Error(`Menu ${menuId} not found`);
  const node = flatten(menu.nodes).find(node => node.id === targetNodeId);
  if (!node) throw new Error(`Node ${targetNodeId} not found`);
  const originalIconClass = node.iconClass || null;

  const model = {
    type: node.type,
    text: node.text,
    url: node.url,
    iconClass: testIconClass,
    enabled: node.enabled,
    priority: node.priority,
    displayPosition: node.displayPosition,
    permissionNames: node.permissionNames || [],
    parentNodeId: node.parentId || '',
    position: node.order,
  };

  try {
    const updated = await api(page, `/api/crest/admin-menus/${encodeURIComponent(menuId)}/nodes/${encodeURIComponent(targetNodeId)}`, {
      method: 'PUT',
      body: JSON.stringify(model),
    });
    const updatedNode = flatten(updated.nodes).find(node => node.id === targetNodeId);
    console.log(`api iconClass: ${updatedNode?.iconClass}`);

    await page.goto(`${baseUrl}/Admin/CRM/Customers`, { waitUntil: 'networkidle' });
    await page.locator('.admin-menu-sidebar').waitFor({ timeout: 15000 });
    const contentIcon = await page.locator('.admin-menu-sidebar__item-content', { hasText: /^Content$/ }).first().locator('.orchard-icon').evaluate(icon => ({
      library: icon.getAttribute('data-icon-library'),
      version: icon.getAttribute('data-icon-version'),
      name: icon.getAttribute('data-icon-name'),
      hasSvg: !!icon.querySelector('svg'),
      rect: (() => { const r = icon.getBoundingClientRect(); return { width: r.width, height: r.height }; })(),
    }));
    console.log(JSON.stringify({ targetNodeId, contentIcon }, null, 2));
  } finally {
    const restore = { ...model, iconClass: originalIconClass };
    await api(page, `/api/crest/admin-menus/${encodeURIComponent(menuId)}/nodes/${encodeURIComponent(targetNodeId)}`, {
      method: 'PUT',
      body: JSON.stringify(restore),
    }).catch(error => console.error(`restore failed: ${error.message}`));
  }

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
