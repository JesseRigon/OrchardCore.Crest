const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function login(page) {
  await page.goto(`${baseUrl}/login`, { waitUntil: 'domcontentloaded' });
  await page.locator('input[name="UserName"]').waitFor({ timeout: 20000 });
  await page.locator('input[name="UserName"]').fill(username);
  await page.locator('input[name="Password"]').fill(password);
  await page.getByRole('button', { name: 'Login', exact: true }).click();
  await page.waitForURL(/\/admin/i, { timeout: 20000 });
}

async function fetchJson(page, url, options = {}) {
  return await page.evaluate(async ({ url, options }) => {
    const response = await fetch(url, { credentials: 'include', ...options });
    const text = await response.text();
    if (!response.ok) {
      throw new Error(`${url}: ${response.status} ${text}`);
    }

    return text ? JSON.parse(text) : null;
  }, { url, options });
}

async function postJson(page, url, body) {
  return await page.evaluate(async ({ url, body }) => {
    const tokenResponse = await fetch('/api/crest/antiforgery/token', { credentials: 'include' });
    if (!tokenResponse.ok) {
      throw new Error(`Unable to load antiforgery token: ${tokenResponse.status}`);
    }

    const token = await tokenResponse.json();
    const response = await fetch(url, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'content-type': 'application/json',
        [token.headerName || 'RequestVerificationToken']: token.requestToken,
      },
      body: JSON.stringify(body),
    });
    const text = await response.text();
    if (!response.ok) {
      throw new Error(`${url}: ${response.status} ${text}`);
    }

    return text ? JSON.parse(text) : null;
  }, { url, body });
}

async function deleteNode(page, menuId, nodeId) {
  await page.evaluate(async ({ menuId, nodeId }) => {
    const token = await (await fetch('/api/crest/antiforgery/token', { credentials: 'include' })).json();
    const response = await fetch(`/api/crest/admin-menus/${encodeURIComponent(menuId)}/nodes/${encodeURIComponent(nodeId)}`, {
      method: 'DELETE',
      credentials: 'include',
      headers: {
        [token.headerName || 'RequestVerificationToken']: token.requestToken,
      },
    });
    if (!response.ok && response.status !== 404) {
      throw new Error(`Unable to delete temporary node ${nodeId}: ${response.status} ${await response.text()}`);
    }
  }, { menuId, nodeId });
}

function findNode(nodes, text) {
  for (const node of nodes || []) {
    if (node.text === text) {
      return node;
    }

    const child = findNode(node.items || node.children || node.nodes || [], text);
    if (child) {
      return child;
    }
  }

  return null;
}

async function createNode(page, menuId, text, parentNodeId, position = 2147483647) {
  await postJson(page, `/api/crest/admin-menus/${encodeURIComponent(menuId)}/nodes`, {
    type: 'placeholder',
    text,
    iconClass: '@iconify:mdi:circle-outline',
    enabled: true,
    priority: 0,
    permissionNames: [],
    parentNodeId,
    position,
  });

  for (let attempt = 0; attempt < 20; attempt++) {
    const summary = await fetchJson(page, `/api/crest/admin-menus/${encodeURIComponent(menuId)}`);
    const node = findNode(summary.nodes, text);
    if (node?.id) {
      return node.id;
    }

    await page.waitForTimeout(150);
  }

  throw new Error(`Created node "${text}" was not returned by the admin menu API.`);
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const createdNodeIds = [];
  let originalSettings = null;
  let menuId = null;

  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    await login(page);

    const state = await fetchJson(page, '/api/crest/admin-menus');
    const menu = state.menus?.find(menu => menu.isDefault) || state.menus?.find(menu => menu.id === '__crest_default_admin_menu');
    if (!menu?.id) {
      throw new Error('Default primary navigation menu was not returned.');
    }

    menuId = menu.id;
    originalSettings = JSON.parse(JSON.stringify(menu.primaryNavMenuSettings));
    await postJson(page, `/api/crest/admin-menus/${encodeURIComponent(menuId)}/primary-nav-menu-settings`, {
      ...originalSettings,
      collapsible: true,
      expansionDurationMilliseconds: 300,
    });

    const stamp = Date.now().toString(36);
    const rootText = `Popup Root ${stamp}`;
    const childText = `Popup Child ${stamp}`;
    const triggerText = `Popup Trigger ${stamp}`;
    const leafText = `Popup Leaf ${stamp}`;

    const rootId = await createNode(page, menuId, rootText, null);
    createdNodeIds.push(rootId);
    const childId = await createNode(page, menuId, childText, rootId);
    createdNodeIds.push(childId);
    const triggerId = await createNode(page, menuId, triggerText, childId);
    createdNodeIds.push(triggerId);
    const leafId = await createNode(page, menuId, leafText, triggerId);
    createdNodeIds.push(leafId);

    await page.goto(`${baseUrl}/Admin`, { waitUntil: 'domcontentloaded' });
    const primaryNavMenu = page.locator('[data-testid="primary-nav-menu"]');
    await primaryNavMenu.waitFor({ timeout: 20000 });

    const collapseButton = page.getByRole('button', { name: 'Collapse navigation' });
    if (await collapseButton.isVisible().catch(() => false)) {
      await collapseButton.click();
    }

    await page.locator('.admin-dashboard__main').hover();
    await page.locator('[data-testid="primary-nav-menu"].primary-nav-menu--compact').waitFor({ timeout: 5000 });
    await primaryNavMenu.hover();
    await page.waitForTimeout(1350);

    const rootButton = primaryNavMenu.locator('.crest-panel-menu__item-link', { hasText: rootText }).first();
    await rootButton.waitFor({ timeout: 10000 });
    if (await rootButton.getAttribute('aria-expanded') !== 'true') {
      await rootButton.click();
    }

    const childButton = primaryNavMenu.locator('.crest-panel-menu__item-link', { hasText: childText }).first();
    await childButton.waitFor({ timeout: 10000 });
    if (await childButton.getAttribute('aria-expanded') !== 'true') {
      await childButton.click();
    }

    const trigger = primaryNavMenu.locator('.crest-panel-menu__item-link', { hasText: triggerText }).first();
    await trigger.waitFor({ timeout: 10000 });
    await trigger.scrollIntoViewIfNeeded();
    await trigger.hover();

    const popup = page.locator('.primary-nav-menu__flyout--detached').first();
    await popup.waitFor({ timeout: 5000 });

    const triggerRect = await trigger.evaluate(element => element.getBoundingClientRect().toJSON());
    const anchoredPlacement = await page.evaluate(() => {
      const popupElement = document.querySelector('.primary-nav-menu__flyout--detached');
      return {
        placement: popupElement?.dataset.crestPopupPlacement,
        popup: popupElement?.getBoundingClientRect().toJSON(),
      };
    });

    if (
      anchoredPlacement.placement !== 'right-below' ||
      !anchoredPlacement.popup ||
      Math.abs(anchoredPlacement.popup.top - triggerRect.top) > 2 ||
      anchoredPlacement.popup.left <= triggerRect.right
    ) {
      throw new Error(`Popup did not anchor to the trigger top-right from DOM coordinates: ${JSON.stringify({ trigger: triggerRect, ...anchoredPlacement })}`);
    }

    // The styled one-item popup is intentionally compact; use a viewport that
    // genuinely requires its window-aware placement to flip above the anchor.
    await page.setViewportSize({ width: 1280, height: 490 });
    await trigger.evaluate(element => element.scrollIntoView({ block: 'end', inline: 'nearest' }));
    await trigger.hover();

    await popup.waitFor({ timeout: 5000 });
    const flippedTriggerRect = await trigger.evaluate(element => element.getBoundingClientRect().toJSON());

    const placement = await popup.evaluate(element => ({
      placement: element.dataset.crestPopupPlacement,
      rect: element.getBoundingClientRect().toJSON(),
      viewportHeight: window.innerHeight,
    }));
    const expectedFlippedBottom = Math.min(flippedTriggerRect.bottom, placement.viewportHeight - 8);
    if (
      placement.placement !== 'right-above' ||
      placement.rect.bottom > placement.viewportHeight ||
      Math.abs(placement.rect.bottom - expectedFlippedBottom) > 2
    ) {
      throw new Error(`Popup did not flip above from the trigger bottom-right and stay inside the viewport: ${JSON.stringify({ trigger: flippedTriggerRect, ...placement })}`);
    }

    const popupBox = await popup.boundingBox();
    await page.mouse.move(popupBox.x + Math.min(20, popupBox.width / 2), popupBox.y + Math.min(20, popupBox.height / 2));
    await page.waitForTimeout(650);

    const stillExpanded = await primaryNavMenu.evaluate(element => element.classList.contains('primary-nav-menu--expanded'));
    if (!stillExpanded) {
      throw new Error('PrimaryNavMenu collapsed while the pointer was over the detached tier-4 popup.');
    }

    await page.locator('.admin-dashboard__main').hover();
    await popup.waitFor({ state: 'detached', timeout: 5000 });
    await page.locator('[data-testid="primary-nav-menu"].primary-nav-menu--compact').waitFor({ timeout: 5000 });

    console.log(JSON.stringify({ popup: 'ok', placement: placement.placement }));
  } finally {
    const page = await browser.newPage();
    try {
      await login(page);
      if (menuId) {
        for (const nodeId of createdNodeIds.reverse()) {
          await deleteNode(page, menuId, nodeId);
        }

        if (originalSettings) {
          await postJson(page, `/api/crest/admin-menus/${encodeURIComponent(menuId)}/primary-nav-menu-settings`, originalSettings);
        }
      }
    } finally {
      await page.close();
      await browser.close();
    }
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
