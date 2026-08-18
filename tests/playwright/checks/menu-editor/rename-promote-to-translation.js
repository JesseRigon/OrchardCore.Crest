// A rename recorded in the Crest layout only applies inside Crest's own sidebar. Promoting it
// writes the same text into the tenant's translation store - the store IDataLocalizer reads at
// render time - keyed on the item's ORIGINAL caption and scoped to the admin menu the node
// belongs to.
//
// The store is read through GetStrings, the same JSON endpoint Orchard's own translations
// editor (a Vue app) loads its values from. Nothing HTML-based works here: /Admin pages render
// inside the Crest shell whose sidebar shows the renamed caption as text, the editor itself
// sits in the legacy-frame iframe outside page.content(), and its values only arrive
// client-side - the JSON endpoint is the one surface that reports exactly what the store holds.
//
// Since provider items are imported as admin menu nodes ("Primary Navigation"), promotion works
// for them too: the imported node supplies the MenuName that scopes the IDataLocalizer context.
// The store is verified through Orchard's own Data Localization editor (/Admin/DataLocalization),
// which renders directly from TranslationsDocument and knows nothing about the Crest layout.
const { fetchAntiforgeryToken } = require('../../harness/antiforgery');

const defaultMenuId = '__crest_default_admin_menu';

module.exports = async function run(page, ctx) {
  const results = [];
  const renameTo = 'Promoted Caption Probe';

  async function post(path) {
    const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
    return page.evaluate(async ({ path, antiforgery }) => {
      const response = await fetch(path, {
        method: 'POST',
        credentials: 'include',
        headers: { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
        body: undefined,
      });
      return { ok: response.ok, status: response.status, text: response.ok ? '' : await response.text() };
    }, { path, antiforgery });
  }

  async function rename(nodeId, text) {
    const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
    return page.evaluate(async ({ nodeId, text, antiforgery }) => {
      const response = await fetch(`/api/crest/admin-menus/__crest_default_admin_menu/nodes/${encodeURIComponent(nodeId)}/rename`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
        body: JSON.stringify({ text }),
      });
      return { ok: response.ok, status: response.status };
    }, { nodeId, text, antiforgery });
  }

  // The translation store, read through the same JSON endpoint Orchard's translations editor
  // loads its values from.
  async function storeHtml() {
    return page.evaluate(async (baseUrl) => {
      // legacy-frame=1 is the marker that makes Crest's admin middleware pass an /Admin/*
      // request through to MVC instead of serving the Blazor shell document - without it this
      // fetch returns the shell's HTML, whose sidebar contains the renamed caption as text and
      // makes every assertion here vacuous.
      const response = await fetch(`${baseUrl}/Admin/DataLocalization/GetStrings?culture=en-US&legacy-frame=1`, { credentials: 'include' });
      if (!response.ok) throw new Error(`GetStrings failed: ${response.status}`);
      return response.text();
    }, ctx.baseUrl);
  }

  // "Content" is a provider-contributed item, imported as an admin menu node - exactly the case
  // promotion exists for. Resolved by caption; its id is the imported node's UniqueId.
  const content = await page.evaluate(async () => {
    const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
    if (!response.ok) throw new Error(`admin menus failed: ${response.status}`);
    const data = await response.json();
    const menu = data.menus.find(candidate => candidate.id === '__crest_default_admin_menu');
    return menu.nodes.find(node => node.text === 'Content') ?? null;
  });
  if (!content) throw new Error('Expected a Content root menu node.');
  const nodeId = content.id;
  const nodeUrl = `/api/crest/admin-menus/${defaultMenuId}/nodes/${encodeURIComponent(nodeId)}`;

  results.push({
    name: 'imported-provider-item-has-uniqueid',
    pass: /^[0-9a-f]{32}$/i.test(nodeId),
    message: `id=${nodeId}`,
  });

  const inStore = json => json.includes(`"${renameTo}"`);

  // Document writes commit after the writing request's response is fully written, so a read
  // fired immediately after can race the tail and see the previous state. Poll briefly for the
  // expected state instead of sampling once.
  async function storeSettles(expected, timeoutMs = 5000) {
    const deadline = Date.now() + timeoutMs;
    let last = await storeHtml();
    while (inStore(last) !== expected && Date.now() < deadline) {
      await page.waitForTimeout(250);
      last = await storeHtml();
    }
    return last;
  }

  try {
    const beforeAny = await storeHtml();
    const preexisting = inStore(beforeAny);

    // A rename alone is a Crest-local display override and must NOT reach the translation
    // store - that separation is what makes promotion a distinct, separately-authorized action.
    await rename(nodeId, renameTo);
    const afterRenameOnly = await storeHtml();
    results.push({
      name: 'rename-alone-does-not-touch-the-translation-store',
      pass: !preexisting && !inStore(afterRenameOnly),
      message: preexisting
        ? `"${renameTo}" already present before the check ran; inconclusive`
        : inStore(afterRenameOnly)
          ? 'store already holds the rename before any promotion'
          : 'store unchanged by the Crest-local rename, as expected',
    });

    const promoted = await post(`${nodeUrl}/promote-rename`);
    results.push({
      name: 'promotes-an-imported-provider-item',
      pass: promoted.ok,
      message: `status=${promoted.status} ${promoted.text.slice(0, 200)}`,
    });

    const afterPromote = await storeSettles(true);
    results.push({
      name: 'promotion-reaches-the-translation-store',
      pass: inStore(afterPromote),
      message: inStore(afterPromote)
        ? `store holds "${renameTo}"`
        : `store does not mention "${renameTo}"`,
    });

    // Promotion follows the layout, including when the rename is cleared, so the two stores can
    // be brought back into agreement the same way they were put into it.
    await rename(nodeId, '');
    await post(`${nodeUrl}/promote-rename`);
    const afterClear = await storeSettles(false);
    results.push({
      name: 'promoting-a-cleared-rename-removes-the-translation',
      pass: !inStore(afterClear),
      message: inStore(afterClear)
        ? `store still holds "${renameTo}" after the rename was cleared and re-promoted`
        : 'translation removed',
    });
  } finally {
    // Repeat defensively so a throw partway through never strands a tenant-wide translation or
    // a renamed sidebar for the rest of the suite.
    await rename(nodeId, '').catch(() => {});
    await post(`${nodeUrl}/promote-rename`).catch(() => {});
  }

  return results;
};
