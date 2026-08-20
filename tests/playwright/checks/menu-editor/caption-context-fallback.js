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
const { fetchAntiforgeryToken } = require('../../harness/antiforgery');

module.exports = async function run(page, ctx) {
  const results = [];
  const probeKey = 'Accounting';
  const probeValue = 'Contabilidad-PRUEBA';

  const cookie = await page.evaluate(async () => {
    const response = await fetch('/api/crest/app/manifest', { credentials: 'include' });
    if (!response.ok) throw new Error(`manifest failed: ${response.status}`);
    const manifest = await response.json();
    return { name: manifest.cultureSelector.cookieName, path: manifest.cultureSelector.cookiePath };
  });

  async function navTexts(cultureValue) {
    return page.evaluate(async ({ cookie, cultureValue }) => {
      if (cultureValue) {
        document.cookie = `${cookie.name}=${encodeURIComponent(`c=${cultureValue}|uic=${cultureValue}`)}; path=${cookie.path}`;
      } else {
        document.cookie = `${cookie.name}=; path=${cookie.path}; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
      }
      const response = await fetch('/api/crest/navigation/admin', { credentials: 'include' });
      if (!response.ok) throw new Error(`navigation failed: ${response.status}`);
      const menu = await response.json();
      const texts = [];
      (function walk(items) {
        for (const item of items || []) { texts.push(item.text); walk(item.items); }
      })(menu.items);
      return texts;
    }, { cookie, cultureValue });
  }

  async function putTranslation(value) {
    const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
    return page.evaluate(async ({ antiforgery, probeKey, value }) => {
      const response = await fetch('/api/crest/translations', {
        method: 'PUT',
        credentials: 'include',
        headers: { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
        body: JSON.stringify({ culture: 'es-ES', translations: [{ context: 'Content Types', key: probeKey, value }] }),
      });
      return response.status;
    }, { antiforgery, probeKey, value });
  }

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

    // 2. Cross-context fallback: a "Content Types" entry covers the menu caption no
    // "Admin Menus" context translates.
    const putStatus = await putTranslation(probeValue);
    let after = [];
    for (let attempt = 0; attempt < 20; attempt++) {
      after = await navTexts('es-ES');
      if (after.includes(probeValue)) break;
      await page.waitForTimeout(250);
    }
    results.push({
      name: 'foreign-context-entry-beats-invariant-literal',
      pass: putStatus === 200 && after.includes(probeValue),
      message: `put=${putStatus} rendered=${after.includes(probeValue)}`,
    });

    // 3. es entries never leak into en-US: the caption stays the invariant literal there.
    const underEn = await navTexts('en-US');
    results.push({
      name: 'fallback-stays-culture-scoped',
      pass: underEn.includes(probeKey) && !underEn.includes(probeValue),
      message: `en-US has "${probeKey}"=${underEn.includes(probeKey)} probe=${underEn.includes(probeValue)}`,
    });
  } finally {
    await putTranslation('').catch(() => {});
    await navTexts(null).catch(() => {});
  }

  return results;
};
