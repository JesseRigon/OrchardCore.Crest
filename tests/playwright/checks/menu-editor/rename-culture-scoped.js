// A rename is a translation of one caption, not a change of identity, so it must only apply
// to the culture the admin was viewing when they typed it. Renaming under fr-FR used to
// replace the single stored DisplayText and therefore changed the English caption too - the
// layout had one rename slot per item, with no record of which culture it belonged to.
//
// Drives a rename under French through a browser context resolved to French, then reads the
// same item back under English and asserts the English caption is untouched, and vice versa:
// both cultures must be able to hold their own rename of the same item simultaneously.
const { fetchAntiforgeryToken } = require('../../harness/antiforgery');
const { createInstance } = require('../../harness/instance');
const { loginAsAdmin } = require('../../harness/auth');

const defaultMenuId = '__crest_default_admin_menu';

async function getRootNodes(page) {
  const data = await page.evaluate(async () => {
    const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
    if (!response.ok) throw new Error(`admin menus failed: ${response.status}`);
    return response.json();
  });
  return data.menus.find(menu => menu.id === defaultMenuId).nodes;
}

async function renameNode(page, baseUrl, nodeId, text) {
  const antiforgery = await fetchAntiforgeryToken(page, baseUrl);
  const result = await page.evaluate(
    async ({ nodeId, text, antiforgery }) => {
      const response = await fetch(`/api/crest/admin-menus/__crest_default_admin_menu/nodes/${encodeURIComponent(nodeId)}/rename`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
        body: JSON.stringify({ text }),
      });
      return { ok: response.ok, status: response.status, text: await response.text() };
    },
    { nodeId, text, antiforgery },
  );
  if (!result.ok) throw new Error(`Renaming ${nodeId} failed: ${result.status} ${result.text}`);
}

async function putJson(page, baseUrl, path, body) {
  const antiforgery = await fetchAntiforgeryToken(page, baseUrl);
  return page.evaluate(async ({ baseUrl, path, body, antiforgery }) => {
    const response = await fetch(`${baseUrl}${path}`, {
      method: 'PUT',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
      body: JSON.stringify(body),
    });
    if (!response.ok) throw new Error(`PUT ${path} failed: ${response.status} ${await response.text()}`);
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
  const frenchRename = 'Contenu personnalise';
  const englishRename = 'Custom Content';

  const localization = await getJson(page, ctx.baseUrl, '/api/crest/localization');
  await putJson(page, ctx.baseUrl, '/api/crest/localization', {
    ...localization,
    supportedCultures: Array.from(new Set([...(localization.supportedCultures || []), 'en-US', 'fr-FR'])),
    defaultCulture: 'en-US',
  });

  // Resolved from the live menu rather than hardcoded: the node's key is a UniqueId/Text.Name
  // handle, not something this check can know up front.
  const contentNode = (await getRootNodes(page)).find(node => node.text === 'Content');
  if (!contentNode) throw new Error('Expected a Content root menu node.');
  const nodeId = contentNode.id;

  const french = await createInstance({ contextOptions: { locale: 'fr-FR' } });
  try {
    await loginAsAdmin(french.page, ctx.baseUrl);
    const trigger = french.page.locator('.admin-titlebar__culture-selector');
    await trigger.click();
    await french.page.locator('[role="option"]', { hasText: 'français' }).first().click();
    await french.page.waitForTimeout(300);

    await renameNode(french.page, ctx.baseUrl, nodeId, frenchRename);

    const frenchAfter = (await getRootNodes(french.page)).find(node => node.id === nodeId);
    results.push({
      name: 'rename-applies-in-its-own-culture',
      pass: frenchAfter?.text === frenchRename,
      message: `fr text="${frenchAfter?.text}"`,
    });

    // The point of the check: the English caption must still be the provider's own, not the
    // French rename leaking across cultures.
    const englishAfter = (await getRootNodes(page)).find(node => node.id === nodeId);
    results.push({
      name: 'rename-does-not-leak-to-other-culture',
      pass: englishAfter?.text === 'Content',
      message: `en text="${englishAfter?.text}" (expected "Content")`,
    });

    // Both cultures holding their own rename of the SAME item at once - this is what a single
    // DisplayText field structurally could not represent.
    await renameNode(page, ctx.baseUrl, nodeId, englishRename);
    const englishOwn = (await getRootNodes(page)).find(node => node.id === nodeId);
    const frenchStill = (await getRootNodes(french.page)).find(node => node.id === nodeId);
    results.push({
      name: 'each-culture-keeps-its-own-rename',
      pass: englishOwn?.text === englishRename && frenchStill?.text === frenchRename,
      message: `en="${englishOwn?.text}" fr="${frenchStill?.text}"`,
    });

    // Clearing a rename must clear only the culture it was cleared in.
    await renameNode(page, ctx.baseUrl, nodeId, '');
    const englishCleared = (await getRootNodes(page)).find(node => node.id === nodeId);
    const frenchAfterEnglishClear = (await getRootNodes(french.page)).find(node => node.id === nodeId);
    results.push({
      name: 'clearing-one-culture-keeps-the-other',
      pass: englishCleared?.text === 'Content' && frenchAfterEnglishClear?.text === frenchRename,
      message: `en="${englishCleared?.text}" fr="${frenchAfterEnglishClear?.text}"`,
    });

    await renameNode(french.page, ctx.baseUrl, nodeId, '').catch(() => {});
  } finally {
    await french.browser.close();
    // Both renames cleared above, but repeat defensively from the surviving page so a throw
    // mid-check doesn't leave a renamed menu behind for the rest of the suite.
    await renameNode(page, ctx.baseUrl, nodeId, '').catch(() => {});
    await putJson(page, ctx.baseUrl, '/api/crest/localization', localization).catch(() => {});
  }

  return results;
};
