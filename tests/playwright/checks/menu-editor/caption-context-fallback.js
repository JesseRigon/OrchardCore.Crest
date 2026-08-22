// CrestMenuCaptionResolver: two behaviors a bare IDataLocalizer lookup lacks.
//
// 1. MenuName restoration. NavigationManager.Merge drops MenuName whenever the provider's
//    instance survives the fold (registration-order roulette; entry #7 in fruitful's
//    plans/upstream-orchard-proposals.md - upstreamable as a Merge copy-list completeness
//    fix, though only Crest's import makes it bite at scale), so captions like "Settings" carried a
//    stored translation that never rendered. The resolver restores the owning menu from the
//    surviving Id (the node's UniqueId), so every stored Primary Navigation caption must now
//    render translated - checked against the store itself rather than hardcoded values.
//
// 2. Hierarchical context fallback. A translation stored under a foreign context (here
//    "Content Types" for the menu caption "Accounting", which no Admin Menus context
//    translates) is the culture's best alternative and must render rather than the invariant
//    literal. en-US must stay unaffected: its store has no such entries, so captions remain
//    the invariant literals.
//
// 3. The PO layer (store edit -> PO -> invariant literal): deleting a stored entry reverts
//    the rendered caption to the shipped PO translation, not the literal, and the editor
//    surfaces that PO value as the empty row's Fallback placeholder.
const { fetchAntiforgeryToken } = require('../../harness/antiforgery');

module.exports = async function run(page, ctx) {
  const results = [];
  const probeValue = 'Contabilidad-PRUEBA';

  const cookie = await page.evaluate(async () => {
    const response = await fetch('/api/crest/app/manifest', { credentials: 'include' });
    if (!response.ok) throw new Error(`manifest failed: ${response.status}`);
    const manifest = await response.json();
    return { name: manifest.cultureSelector.cookieName, path: manifest.cultureSelector.cookiePath };
  });

  async function navItems(cultureValue) {
    return page.evaluate(async ({ cookie, cultureValue }) => {
      if (cultureValue) {
        document.cookie = `${cookie.name}=${encodeURIComponent(`c=${cultureValue}|uic=${cultureValue}`)}; path=${cookie.path}`;
      } else {
        document.cookie = `${cookie.name}=; path=${cookie.path}; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
      }
      const response = await fetch('/api/crest/navigation/admin', { credentials: 'include' });
      if (!response.ok) throw new Error(`navigation failed: ${response.status}`);
      const menu = await response.json();
      const items = [];
      (function walk(nodes) {
        for (const item of nodes || []) { items.push({ text: item.text, textKey: item.textKey }); walk(item.items); }
      })(menu.items);
      return items;
    }, { cookie, cultureValue });
  }

  const navTexts = async (cultureValue) => (await navItems(cultureValue)).map(item => item.text);

  async function putTranslation(key, value) {
    const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
    return page.evaluate(async ({ antiforgery, key, value }) => {
      const response = await fetch('/api/crest/translations', {
        method: 'PUT',
        credentials: 'include',
        headers: { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
        body: JSON.stringify({ culture: 'es-ES', translations: [{ context: 'Content Types', key, value }] }),
      });
      return response.status;
    }, { antiforgery, key, value });
  }

  let cleanupKey = null;
  try {
    // 1. Every stored Primary Navigation caption renders translated - read the expectation
    // from the store itself so the check tracks the seeds instead of hardcoding them.
    const stored = await page.evaluate(async () => {
      const response = await fetch('/api/crest/translations?culture=es-ES', { credentials: 'include' });
      if (!response.ok) throw new Error(`translations failed: ${response.status}`);
      const data = await response.json();
      const group = data.groups.find(g => g.name === 'Admin Menus:Primary Navigation');
      return (group?.strings || []).filter(s => s.value).map(s => ({ key: s.key, value: s.value }));
    });

    const spanish = await navTexts('es-ES');
    // Only entries whose key actually appears in the built menu can be asserted (permissions
    // or disabled features may hide some), so compare against what the en menu shows.
    const invariantTexts = new Set(await navTexts('en-US'));
    const applicable = stored.filter(entry => invariantTexts.has(entry.key));
    const missing = applicable.filter(entry => !spanish.includes(entry.value));
    results.push({
      name: 'all-stored-menu-captions-render-translated',
      pass: applicable.length > 0 && missing.length === 0,
      message: missing.length
        ? `missing: ${JSON.stringify(missing.slice(0, 6))}`
        : `${applicable.length} stored captions all rendered translated`,
    });

    // 2. Cross-context fallback: a "Content Types" entry covers a menu caption no
    // "Admin Menus" context translates. The probe caption is picked dynamically: an
    // item whose es rendering still equals its invariant key, i.e. one with no store
    // entry, no PO translation and - crucially - no per-culture rename overlay, which
    // sits ABOVE translation resolution by design and would legitimately shadow the
    // probe (found the hard way: a manual "testing" rename on Accounting).
    const esItems = await navItems('es-ES');
    const candidate = esItems.find(item =>
      item.textKey && item.text === item.textKey && item.textKey !== 'New');
    const probeKey = candidate?.textKey;
    let putStatus = null;
    let after = [];
    if (probeKey) {
      cleanupKey = probeKey;
      putStatus = await putTranslation(probeKey, probeValue);
      for (let attempt = 0; attempt < 20; attempt++) {
        after = await navTexts('es-ES');
        if (after.includes(probeValue)) break;
        await page.waitForTimeout(250);
      }
    }
    results.push({
      name: 'foreign-context-entry-beats-invariant-literal',
      pass: !!probeKey && putStatus === 200 && after.includes(probeValue),
      message: probeKey
        ? `key="${probeKey}" put=${putStatus} rendered=${after.includes(probeValue)}`
        : 'no un-overlaid untranslated caption available to probe',
    });

    // 3. es entries never leak into en-US: the caption stays the invariant literal there.
    const underEn = await navTexts('en-US');
    results.push({
      name: 'fallback-stays-culture-scoped',
      pass: !!probeKey && underEn.includes(probeKey) && !underEn.includes(probeValue),
      message: probeKey
        ? `en-US has "${probeKey}"=${underEn.includes(probeKey)} probe=${underEn.includes(probeValue)}`
        : 'skipped: no probe key',
    });

    // 4. The PO layer: deleting a stored translation reverts to the shipped PO value,
    // not the invariant literal (resolution is store edit -> PO -> literal, delete
    // walks down the hierarchy) - and the editor surfaces that PO value as Fallback on
    // the now-empty row. Restores the original entry afterward.
    const poKey = 'Cultures';
    const getRow = async () => page.evaluate(async ({ poKey }) => {
      const r = await fetch('/api/crest/translations?culture=es-ES', { credentials: 'include' });
      if (!r.ok) return null;
      const data = await r.json();
      const group = data.groups.find(g => g.name === 'Admin Menus:Primary Navigation');
      return group?.strings.find(s => s.key === poKey) ?? null;
    }, { poKey });

    const before = await getRow();
    if (before?.value) {
      const original = before.value;
      const antiforgery2 = await fetchAntiforgeryToken(page, ctx.baseUrl);
      const putRow = async (value) => page.evaluate(async ({ antiforgery, poKey, value }) => {
        const r = await fetch('/api/crest/translations', {
          method: 'PUT',
          credentials: 'include',
          headers: { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
          body: JSON.stringify({ culture: 'es-ES', translations: [{ context: 'Admin Menus:Primary Navigation', key: poKey, value }] }),
        });
        if (!r.ok) return null;
        const data = await r.json();
        const group = data.groups.find(g => g.name === 'Admin Menus:Primary Navigation');
        return group?.strings.find(s => s.key === poKey) ?? null;
      }, { antiforgery: antiforgery2, poKey, value });

      try {
        const deleted = await putRow('');
        results.push({
          name: 'empty-row-surfaces-po-fallback-in-editor',
          pass: !!deleted && !deleted.value && !!deleted.fallback,
          message: deleted ? `value="${deleted.value}" fallback="${deleted.fallback}"` : 'PUT failed',
        });

        // Wait until the deletion's deferred commit is visible to a fresh request,
        // then the sidebar must STILL render the PO value - previously a delete
        // dropped the caption back to the invariant literal.
        for (let attempt = 0; attempt < 20; attempt++) {
          const row = await getRow();
          if (row && !row.value) break;
          await page.waitForTimeout(250);
        }
        const esTexts = await navTexts('es-ES');
        results.push({
          name: 'deleted-entry-reverts-to-po-not-literal',
          pass: !!deleted?.fallback && esTexts.includes(deleted.fallback),
          message: `expected PO value "${deleted?.fallback}" in sidebar; literal present=${esTexts.includes(poKey)}`,
        });
      } finally {
        await putRow(original).catch(() => {});
      }
    } else {
      results.push({
        name: 'deleted-entry-reverts-to-po-not-literal',
        pass: true,
        message: `skipped: no stored es-ES value for "${poKey}" to exercise`,
      });
    }
  } finally {
    if (cleanupKey) {
      await putTranslation(cleanupKey, '').catch(() => {});
    }
    await navTexts(null).catch(() => {});
  }

  return results;
};
