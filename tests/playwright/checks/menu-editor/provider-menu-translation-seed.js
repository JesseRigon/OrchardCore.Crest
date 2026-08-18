// Importing a provider item moves its caption out of the PO pipeline's reach: the rendered
// item carries the imported node's raw literal and resolves through the tenant translation
// store instead of the provider's S["..."] resource. The sync therefore SEEDS that store from
// the PO catalogs - for every supported culture, every imported caption with a PO translation
// gets a store entry, unless the tenant already has one (admin edits and promotions always
// win). Without this, switching to es-ES showed an all-English sidebar even though the es PO
// catalog translates every stock caption - the import is expected to carry the provider's
// translations along with its items.
//
// Assertions run against both halves of the pipeline: the store itself (via GetStrings, the
// JSON endpoint Orchard's own translations editor loads from) and the served sidebar (the
// navigation API, which resolves captions through IDataLocalizer per request culture).
//
// A fresh FruitfulSetup tenant supports en-US and es-ES, so es-ES needs no provisioning here.
// No cleanup either: seeded translations are the intended baseline state of a synced tenant,
// exactly like the imported nodes themselves.
const { fetchAntiforgeryToken } = require('../../harness/antiforgery');

module.exports = async function run(page, ctx) {
  const results = [];

  // Re-runs the import (and its seeding) on demand - idempotent, and independent of whether
  // this shell already synced before the check ran.
  const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
  const synced = await page.evaluate(async ({ antiforgery }) => {
    const response = await fetch('/api/crest/admin-menus/sync-providers', {
      method: 'POST',
      credentials: 'include',
      headers: { [antiforgery.headerName]: antiforgery.requestToken },
    });
    return { ok: response.ok, status: response.status, body: response.ok ? await response.json() : await response.text() };
  }, { antiforgery });
  results.push({
    name: 'sync-providers-succeeds',
    pass: synced.ok,
    message: `status=${synced.status} ${JSON.stringify(synced.body).slice(0, 200)}`,
  });

  // The store, read through the same endpoint the translations editor loads from.
  // legacy-frame=1 keeps the admin middleware from serving the Blazor shell document instead.
  const store = await page.evaluate(async () => {
    const response = await fetch('/Admin/DataLocalization/GetStrings?culture=es-ES&legacy-frame=1', { credentials: 'include' });
    if (!response.ok) throw new Error(`GetStrings failed: ${response.status}`);
    return response.text();
  });
  results.push({
    name: 'store-holds-the-po-translation',
    pass: store.includes('"Contenido"'),
    message: store.includes('"Contenido"')
      ? 'es-ES store translates "Content" to "Contenido"'
      : `es-ES store has no "Contenido" (${store.length} bytes returned)`,
  });

  // The sidebar under es-ES. The culture cookie is Crest's own (name carries the shell's
  // VersionId), so it is read from the app manifest rather than assumed.
  const cookie = await page.evaluate(async () => {
    const response = await fetch('/api/crest/app/manifest', { credentials: 'include' });
    if (!response.ok) throw new Error(`manifest failed: ${response.status}`);
    const manifest = await response.json();
    return { name: manifest.cultureSelector.cookieName, path: manifest.cultureSelector.cookiePath };
  });

  async function sidebarTexts(cultureValue) {
    return page.evaluate(async ({ cookie, cultureValue }) => {
      if (cultureValue) {
        document.cookie = `${cookie.name}=${encodeURIComponent(`c=${cultureValue}|uic=${cultureValue}`)}; path=${cookie.path}`;
      } else {
        document.cookie = `${cookie.name}=; path=${cookie.path}; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
      }
      const response = await fetch('/api/crest/navigation/admin', { credentials: 'include' });
      if (!response.ok) throw new Error(`navigation failed: ${response.status}`);
      const menu = await response.json();
      return menu.items.map(item => item.text);
    }, { cookie, cultureValue });
  }

  try {
    const spanish = await sidebarTexts('es-ES');
    results.push({
      name: 'sidebar-renders-seeded-captions-under-es',
      pass: spanish.includes('Contenido'),
      message: JSON.stringify(spanish.slice(0, 8)),
    });

    const english = await sidebarTexts('en-US');
    results.push({
      name: 'sidebar-still-english-under-en',
      pass: english.includes('Content') && !english.includes('Contenido'),
      message: JSON.stringify(english.slice(0, 8)),
    });
  } finally {
    // Drop the culture cookie so later checks in the shared browser run under the default.
    await sidebarTexts(null).catch(() => {});
  }

  return results;
};
