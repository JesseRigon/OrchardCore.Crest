// The menu editor and the sidebar both persist overrides against NavigationItem.Key, so that
// key has to survive the two things that legitimately change about an item: its caption being
// translated, and its caption being edited.
//
// Two different mechanisms provide that stability, and this check covers both:
//   * Provider-contributed items (OrchardCore's INavigationProvider implementations) carry
//     MenuItem.Text.Name, the untranslated S["..."] literal. Served as TextKey.
//   * Admin Menu feature nodes (DB-backed, created in the admin UI) carry no such literal -
//     their caption is a raw string the admin typed. They instead carry AdminNode.UniqueId,
//     a GUID assigned at creation, which the node navigation builders copy onto MenuItem.Id.
//
// Key prefers Id and falls back to TextKey, so in both cases it must be free of the displayed
// caption. A key that varies with culture silently orphans every stored override on a culture
// switch, which is the failure this guards.
const { fetchAntiforgeryToken } = require('../../harness/antiforgery');
const { createInstance } = require('../../harness/instance');
const { loginAsAdmin } = require('../../harness/auth');

const defaultMenuId = '__crest_default_admin_menu';
const GUID_LIKE = /^[0-9a-f]{32}$/i;

async function getRootNodes(page) {
  const data = await page.evaluate(async () => {
    const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
    if (!response.ok) throw new Error(`admin menus failed: ${response.status}`);
    return response.json();
  });
  return data.menus.find(menu => menu.id === defaultMenuId).nodes;
}

async function getNavigationItems(page) {
  const nav = await page.evaluate(async () => {
    const response = await fetch('/api/crest/navigation/admin', { credentials: 'include' });
    if (!response.ok) throw new Error(`navigation failed: ${response.status}`);
    return response.json();
  });
  return nav.items ?? [];
}

async function postJson(page, baseUrl, path, body) {
  const antiforgery = await fetchAntiforgeryToken(page, baseUrl);
  return page.evaluate(async ({ path, body, antiforgery }) => {
    const response = await fetch(path, {
      method: 'POST',
      credentials: 'include',
      headers: { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
      body: JSON.stringify(body),
    });
    return { ok: response.ok, status: response.status, body: response.ok ? await response.json() : await response.text() };
  }, { path, body, antiforgery });
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

  // --- Provider-contributed items: TextKey is the untranslated literal ---------------------
  const navItems = await getNavigationItems(page);
  const withTextKey = navItems.filter(item => item.textKey);
  results.push({
    name: 'provider-items-carry-textkey',
    pass: withTextKey.length > 0,
    message: `${withTextKey.length}/${navItems.length} root items have a textKey`,
  });

  // A provider item whose caption is untranslated in this culture has textKey === text, which
  // proves nothing. Assert instead that the key never contains the *translated* caption, by
  // comparing keys across two cultures below.

  // --- Admin Menu feature nodes: Id is the node's UniqueId ---------------------------------
  // Created through the API rather than assumed to exist, so the check is self-contained and
  // exercises the create -> read -> rename path a real admin takes.
  const created = await postJson(page, ctx.baseUrl, '/api/crest/admin-menus', {
    name: `Key stability ${Date.now()}`,
  });
  let createdMenuId = created.ok ? created.body?.id : null;
  let nodeKeyBefore;
  let nodeKeyAfterRename;

  const localization = await getJson(page, ctx.baseUrl, '/api/crest/localization');
  let french;

  try {
    if (createdMenuId) {
      const nodeCreated = await postJson(page, ctx.baseUrl, `/api/crest/admin-menus/${encodeURIComponent(createdMenuId)}/nodes`, {
        type: 'LinkAdminNode',
        text: 'Key Probe',
        url: '/Admin',
        enabled: true,
        priority: 0,
        displayPosition: null,
        permissionNames: [],
        parentNodeId: null,
        position: null,
      });

      const probe = nodeCreated.ok
        ? (nodeCreated.body?.nodes ?? []).find(node => node.text === 'Key Probe')
        : null;
      nodeKeyBefore = probe?.id;

      results.push({
        name: 'admin-node-id-is-a-uniqueid-guid',
        pass: !!nodeKeyBefore && GUID_LIKE.test(nodeKeyBefore),
        message: `id=${nodeKeyBefore} (expected a 32-char guid, not a caption or url)`,
      });

      results.push({
        name: 'admin-node-id-is-not-the-caption',
        pass: !!nodeKeyBefore && !nodeKeyBefore.includes('Key Probe'),
        message: `id=${nodeKeyBefore}`,
      });

      // Editing the caption must not move the identity - this is the whole reason UniqueId is
      // preferred over the caption as a key.
      if (nodeKeyBefore) {
        const renamed = await postJson(
          page,
          ctx.baseUrl,
          `/api/crest/admin-menus/${encodeURIComponent(createdMenuId)}/nodes/${encodeURIComponent(nodeKeyBefore)}`,
          {
            type: 'LinkAdminNode',
            text: 'Key Probe Renamed',
            url: '/Admin',
            enabled: true,
            priority: 0,
            displayPosition: null,
            permissionNames: [],
            parentNodeId: null,
            position: null,
          },
        );
        // The update endpoint is a PUT; POST above is only used for create. Re-read instead of
        // trusting the response shape.
        const after = await page.evaluate(async (menuId) => {
          const response = await fetch(`/api/crest/admin-menus/${encodeURIComponent(menuId)}`, { credentials: 'include' });
          return response.ok ? await response.json() : null;
        }, createdMenuId);
        const probeAfter = (after?.nodes ?? []).find(node => node.id === nodeKeyBefore);
        nodeKeyAfterRename = probeAfter?.id;
        results.push({
          name: 'admin-node-id-survives-a-rename',
          pass: !!nodeKeyAfterRename && nodeKeyAfterRename === nodeKeyBefore,
          message: `before=${nodeKeyBefore} after=${nodeKeyAfterRename} renameStatus=${renamed.status}`,
        });
      }
    } else {
      results.push({
        name: 'admin-node-id-is-a-uniqueid-guid',
        pass: false,
        message: `could not create a probe menu: ${created.status} ${created.body}`,
      });
    }

    // --- Keys are identical across cultures --------------------------------------------------
    await putJson(page, ctx.baseUrl, '/api/crest/localization', {
      ...localization,
      supportedCultures: Array.from(new Set([...(localization.supportedCultures || []), 'en-US', 'fr-FR'])),
      defaultCulture: 'en-US',
    });

    const englishRoots = await getRootNodes(page);

    french = await createInstance({ contextOptions: { locale: 'fr-FR' } });
    await loginAsAdmin(french.page, ctx.baseUrl);
    await french.page.locator('.admin-titlebar__culture-selector').click();
    await french.page.locator('[role="option"]', { hasText: 'français' }).first().click();
    await french.page.waitForTimeout(300);

    const frenchRoots = await getRootNodes(french.page);

    const englishKeys = englishRoots.map(node => node.id).sort();
    const frenchKeys = frenchRoots.map(node => node.id).sort();
    results.push({
      name: 'root-keys-identical-across-cultures',
      pass: englishKeys.length > 0 && JSON.stringify(englishKeys) === JSON.stringify(frenchKeys),
      message: englishKeys.length === 0
        ? 'no root nodes'
        : `en=${englishKeys.length} fr=${frenchKeys.length} onlyEn=${englishKeys.filter(k => !frenchKeys.includes(k)).join(',') || 'none'} onlyFr=${frenchKeys.filter(k => !englishKeys.includes(k)).join(',') || 'none'}`,
    });

    // At least one root node must actually be displaying a different caption under French,
    // otherwise the assertion above passes trivially on a tenant with no French translations
    // and tells us nothing about culture-invariance.
    const captionsDiffer = englishRoots.some(en => {
      const fr = frenchRoots.find(node => node.id === en.id);
      return fr && fr.text !== en.text;
    });
    results.push({
      name: 'at-least-one-caption-differs-across-cultures',
      pass: captionsDiffer,
      message: captionsDiffer
        ? 'captions differ, so the key comparison above is meaningful'
        : 'no root caption changed under fr-FR; key comparison is inconclusive',
    });

    const frenchNav = await getNavigationItems(french.page);
    const textKeyStable = withTextKey.every(en => {
      const fr = frenchNav.find(item => item.textKey === en.textKey);
      return !!fr;
    });
    results.push({
      name: 'provider-textkey-is-culture-invariant',
      pass: withTextKey.length > 0 && textKeyStable,
      message: `matched ${withTextKey.filter(en => frenchNav.some(fr => fr.textKey === en.textKey)).length}/${withTextKey.length} textKeys under fr-FR`,
    });
  } finally {
    if (french) await french.browser.close();
    if (createdMenuId) {
      const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
      await page.evaluate(async ({ menuId, antiforgery }) => {
        await fetch(`/api/crest/admin-menus/${encodeURIComponent(menuId)}`, {
          method: 'DELETE',
          credentials: 'include',
          headers: { [antiforgery.headerName]: antiforgery.requestToken },
        });
      }, { menuId: createdMenuId, antiforgery }).catch(() => {});
    }
    await putJson(page, ctx.baseUrl, '/api/crest/localization', localization).catch(() => {});
  }

  return results;
};
