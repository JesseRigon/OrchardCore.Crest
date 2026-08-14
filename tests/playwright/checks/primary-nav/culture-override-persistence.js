const { fetchAntiforgeryToken } = require('../../harness/antiforgery');
const { createInstance } = require('../../harness/instance');
const { loginAsAdmin } = require('../../harness/auth');

// Regression check for the NavigationItem.Key bug: menu-editor overrides (hide/reorder/
// icon/rename) used to be keyed by a hash of the item's TRANSLATED display text when no
// explicit Id/link was present, so switching the admin UI culture changed the hash and
// silently orphaned every stored override for items without an Id (most stock
// OrchardCore admin-menu category nodes - "Content", "Design", etc). The fix makes Key
// use LocalizedString.Name (the untranslated resource key) instead, so this check drives
// the SAME override under one culture and verifies it still applies after switching the
// admin's resolved culture, in a browser context configured for that other culture.
const defaultMenuId = '__crest_default_admin_menu';

async function getRootNodes(page) {
  const data = await page.evaluate(async () => {
    const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
    if (!response.ok) throw new Error(`admin menus failed: ${response.status}`);
    return response.json();
  });
  return data.menus.find(menu => menu.id === defaultMenuId).nodes;
}

async function updateNodeIcon(page, baseUrl, node, iconClass) {
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
    position: null,
  };

  // AdminMenusController is [AutoValidateAntiforgeryToken] - see harness/antiforgery.js.
  const antiforgery = await fetchAntiforgeryToken(page, baseUrl);
  const result = await page.evaluate(
    async ({ id, payload, antiforgery }) => {
      const response = await fetch(`/api/crest/admin-menus/__crest_default_admin_menu/nodes/${encodeURIComponent(id)}`, {
        method: 'PUT',
        credentials: 'include',
        headers: { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
        body: JSON.stringify(payload),
      });
      return { ok: response.ok, status: response.status, text: await response.text() };
    },
    { id: node.id, payload, antiforgery },
  );

  if (!result.ok) throw new Error(`Updating ${node.text} failed: ${result.status} ${result.text}`);
}

async function putJson(page, baseUrl, path, body) {
  const antiforgery = await fetchAntiforgeryToken(page, baseUrl);

  return page.evaluate(async ({ baseUrl, path, body, antiforgery }) => {
    const headers = { 'Content-Type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken };
    const response = await fetch(`${baseUrl}${path}`, {
      method: 'PUT',
      credentials: 'include',
      headers,
      body: JSON.stringify(body),
    });
    if (!response.ok) {
      throw new Error(`PUT ${path} failed: ${response.status} ${await response.text()}`);
    }
    return response.json();
  }, { baseUrl, path, body, antiforgery });
}

async function getJson(page, baseUrl, path) {
  return page.evaluate(async ({ baseUrl, path }) => {
    const response = await fetch(`${baseUrl}${path}`, { credentials: 'include' });
    if (!response.ok) throw new Error(`GET ${path} failed: ${response.status}`);
    return response.json();
  }, { baseUrl, path });
}

module.exports = async function run(page, ctx) {
  const results = [];
  const overrideIcon = '@iconify:mdi:home';

  const localization = await getJson(page, ctx.baseUrl, '/api/crest/localization');
  const withFrench = { ...localization, supportedCultures: Array.from(new Set([...(localization.supportedCultures || []), 'en-US', 'fr-FR'])), defaultCulture: 'en-US' };
  await putJson(page, ctx.baseUrl, '/api/crest/localization', withFrench);
  results.push({ name: 'enables-fr-culture', pass: true, message: 'supportedCultures includes fr-FR' });

  let original;
  try {
    // Save the override under English (the tenant/browser default) - a root category
    // node like "Content" has no Id and no link, so before the fix its key was a hash of
    // the English text "Content".
    let nodes = await getRootNodes(page);
    const content = nodes.find(node => node.text === 'Content');
    if (!content) throw new Error('Expected a Content root menu node.');
    original = content.iconClass;

    await updateNodeIcon(page, ctx.baseUrl, content, overrideIcon);
    nodes = await getRootNodes(page);
    const afterEnglishSave = nodes.find(node => node.text === 'Content');
    results.push({
      name: 'override-applies-under-english',
      pass: afterEnglishSave?.iconClass === overrideIcon,
      message: `iconClass=${afterEnglishSave?.iconClass}`,
    });

    // Now read the SAME menu back through a browser context resolved to French (session
    // override, the highest-priority rung - see plans/user-localization.md) and confirm
    // the override still applies to the French-labelled node. Before the fix this failed:
    // the French label hashed to a different key than the one the override was saved
    // under, so the node came back with iconClass=null under French.
    const french = await createInstance({ contextOptions: { locale: 'fr-FR' } });
    try {
      await loginAsAdmin(french.page, ctx.baseUrl);
      const trigger = french.page.locator('.admin-titlebar__culture-selector');
      await trigger.click();
      await french.page.locator('[role="option"]', { hasText: 'français' }).first().click();
      await french.page.waitForTimeout(300);

      const frenchNodes = await getRootNodes(french.page);
      const frenchContent = frenchNodes.find(node => node.iconClass === overrideIcon);
      results.push({
        name: 'override-survives-culture-switch',
        pass: !!frenchContent,
        message: frenchContent ? `text="${frenchContent.text}" iconClass=${frenchContent.iconClass}` : `no node carried iconClass=${overrideIcon}; nodes=${JSON.stringify(frenchNodes.map(n => ({ text: n.text, iconClass: n.iconClass })))}`,
      });

      // The key itself must also be culture-invariant - the node's id should be the same
      // technical key regardless of which culture rendered its label.
      const englishContentAgain = (await getRootNodes(page)).find(node => node.iconClass === overrideIcon);
      results.push({
        name: 'key-is-culture-invariant',
        pass: !!frenchContent && !!englishContentAgain && frenchContent.id === englishContentAgain.id,
        message: `en-id=${englishContentAgain?.id} fr-id=${frenchContent?.id}`,
      });
    } finally {
      await french.browser.close();
    }
  } finally {
    if (original !== undefined) {
      const restoreNodes = await getRootNodes(page);
      const restoreContent = restoreNodes.find(node => node.iconClass === overrideIcon);
      if (restoreContent) {
        await updateNodeIcon(page, ctx.baseUrl, restoreContent, original).catch(() => {});
      }
    }
    // Restore the tenant's original supportedCultures/defaultCulture - otherwise fr-FR
    // stays enabled for every check that runs after this one in the same suite run.
    await putJson(page, ctx.baseUrl, '/api/crest/localization', localization).catch(() => {});
  }

  return results;
};
