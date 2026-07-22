const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const headed = process.env.HEADED === '1';
const defaultMenuId = '__crest_default_admin_menu';

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

  async function getRootNodes() {
    const data = await page.evaluate(async () => {
      const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
      if (!response.ok) {
        throw new Error(`admin menus failed: ${response.status}`);
      }

      return await response.json();
    });

    return data.menus.find(menu => menu.id === defaultMenuId).nodes;
  }

  async function updateNodeIcon(node, iconClass) {
    const payload = {
      type: node.type,
      text: node.text,
      url: node.url,
      iconClass,
      enabled: node.enabled,
      priority: node.priority,
      displayPosition: node.displayPosition,
      permissionNames: node.permissionNames,
      parentNodeId: null,
      position: null
    };

    const result = await page.evaluate(async ({ id, payload }) => {
      const response = await fetch(`/api/crest/admin-menus/__crest_default_admin_menu/nodes/${encodeURIComponent(id)}`, {
        method: 'PUT',
        credentials: 'include',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(payload)
      });

      return { ok: response.ok, status: response.status, text: await response.text() };
    }, { id: node.id, payload });

    if (!result.ok) {
      throw new Error(`Updating ${node.text} failed: ${result.status} ${result.text}`);
    }
  }

  let nodes = await getRootNodes();
  const content = nodes.find(node => node.text === 'Content');
  const design = nodes.find(node => node.text === 'Design');

  if (!content || !design) {
    throw new Error('Expected Content and Design root menu nodes.');
  }

  const original = {
    content: content.iconClass,
    design: design.iconClass
  };

  try {
    await updateNodeIcon(content, '@iconify:mdi:home');
    nodes = await getRootNodes();
    await updateNodeIcon(nodes.find(node => node.text === 'Design'), '@iconify:mdi:wrench');

    nodes = await getRootNodes();
    const afterTwoUpdates = {
      content: nodes.find(node => node.text === 'Content')?.iconClass,
      design: nodes.find(node => node.text === 'Design')?.iconClass
    };

    await updateNodeIcon(nodes.find(node => node.text === 'Content'), '@iconify:mdi:account');

    nodes = await getRootNodes();
    const afterChangingContentAgain = {
      content: nodes.find(node => node.text === 'Content')?.iconClass,
      design: nodes.find(node => node.text === 'Design')?.iconClass
    };

    console.log(JSON.stringify({ original, afterTwoUpdates, afterChangingContentAgain }, null, 2));

    if (afterTwoUpdates.content !== '@iconify:mdi:home' || afterTwoUpdates.design !== '@iconify:mdi:wrench') {
      throw new Error('Expected both icon overrides to persist after separate saves.');
    }

    if (afterChangingContentAgain.content !== '@iconify:mdi:account') {
      throw new Error(`Expected Content icon to update, got ${afterChangingContentAgain.content}`);
    }

    if (afterChangingContentAgain.design !== '@iconify:mdi:wrench') {
      throw new Error(`Expected Design override to remain untouched, got ${afterChangingContentAgain.design}`);
    }
  } finally {
    const restoreNodes = await getRootNodes();
    const restoreContent = restoreNodes.find(node => node.text === 'Content');
    const restoreDesign = restoreNodes.find(node => node.text === 'Design');

    if (restoreContent && original.content) {
      await updateNodeIcon(restoreContent, original.content);
    }

    if (restoreDesign && original.design) {
      const latestNodes = await getRootNodes();
      await updateNodeIcon(latestNodes.find(node => node.text === 'Design'), original.design);
    }
  }

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
