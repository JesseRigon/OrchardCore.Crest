// The "New" branch's children are content types, and their captions render outside any owning
// admin menu - upstream resolves them under the generic "Admin Menus" data-localization
// context, which no provider populates with type names, leaving them untranslatable from the
// Translations editor. Crest closes that seam additively: an ownerless caption with no
// "Admin Menus" translation falls back to the same caption's "Content Types" translation - the
// one the Translations editor DOES offer, and the one every content-editing surface already
// uses. One translation covers every surface a type name appears on.
//
// The check writes a probe translation for the first New child's type name under the
// "Content Types" context (via Orchard's own Save endpoint, carrying the full existing list
// because Save replaces a culture's translations wholesale), asserts the New menu serves it
// for that culture, and restores the original list afterwards.
const { fetchAntiforgeryToken } = require('../../harness/antiforgery');

const probeCulture = 'es-ES';

module.exports = async function run(page, ctx) {
  const results = [];
  const probeValue = 'Tipo Probe';

  const cookie = await page.evaluate(async () => {
    const response = await fetch('/api/crest/app/manifest', { credentials: 'include' });
    if (!response.ok) throw new Error(`manifest failed: ${response.status}`);
    const manifest = await response.json();
    return { name: manifest.cultureSelector.cookieName, path: manifest.cultureSelector.cookiePath };
  });

  async function navMenu(cultureValue) {
    return page.evaluate(async ({ cookie, cultureValue }) => {
      if (cultureValue) {
        document.cookie = `${cookie.name}=${encodeURIComponent(`c=${cultureValue}|uic=${cultureValue}`)}; path=${cookie.path}`;
      } else {
        document.cookie = `${cookie.name}=; path=${cookie.path}; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
      }
      const response = await fetch('/api/crest/navigation/admin', { credentials: 'include' });
      if (!response.ok) throw new Error(`navigation failed: ${response.status}`);
      return response.json();
    }, { cookie, cultureValue });
  }

  // The New branch is the provider-owned item whose Id stays the literal "new".
  const newBranchOf = menu => menu.items.find(item => item.id === 'new');

  // Existing translations for the culture, reconstructed the same way the Vue editor does
  // before a save: every provider-enumerated string that currently has a value. Save replaces
  // the culture's whole list, so this is what keeps the seeded/promoted entries alive.
  async function readTranslations() {
    return page.evaluate(async (probeCulture) => {
      const response = await fetch(`/Admin/DataLocalization/GetStrings?culture=${probeCulture}&legacy-frame=1`, { credentials: 'include' });
      if (!response.ok) throw new Error(`GetStrings failed: ${response.status}`);
      const data = await response.json();
      const translations = [];
      for (const provider of data.providers ?? []) {
        for (const s of provider.strings ?? []) {
          if (s.value) translations.push({ context: s.context, key: s.key, value: s.value });
        }
        for (const subGroup of provider.subGroups ?? []) {
          for (const s of subGroup.strings ?? []) {
            if (s.value) translations.push({ context: s.context, key: s.key, value: s.value });
          }
        }
      }
      return translations;
    }, probeCulture);
  }

  async function saveTranslations(translations) {
    const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
    return page.evaluate(async ({ translations, antiforgery, probeCulture }) => {
      const response = await fetch('/Admin/DataLocalization/Save?legacy-frame=1', {
        method: 'POST',
        credentials: 'include',
        headers: { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
        body: JSON.stringify({ culture: probeCulture, translations }),
      });
      return { ok: response.ok, status: response.status, text: response.ok ? '' : await response.text() };
    }, { translations, antiforgery, probeCulture });
  }

  const before = await navMenu(probeCulture);
  const newBranch = newBranchOf(before);
  const typeChild = newBranch?.items?.[0];
  if (!typeChild) {
    return [{ name: 'new-branch-found', pass: false, message: `no New branch child (${JSON.stringify(newBranch)})` }];
  }
  // textKey is the untranslated literal - the content type's DisplayName, which is the key the
  // Content Types translations are stored under.
  const typeName = typeChild.textKey;

  const original = await readTranslations();

  try {
    const withProbe = [
      ...original.filter(t => !(t.context === 'Content Types' && t.key === typeName)),
      { context: 'Content Types', key: typeName, value: probeValue },
    ];
    const saved = await saveTranslations(withProbe);
    results.push({
      name: 'content-type-translation-saves',
      pass: saved.ok,
      message: `status=${saved.status} ${saved.text.slice(0, 150)}`,
    });

    // Save evicts the culture dictionary cache in-request, but the document write commits
    // after the response - so a menu request fired in that window re-primes the cache from the
    // OLD document and nothing evicts it again (an upstream race, not a Crest one). Wait for
    // the commit to be visible at the document level (GetStrings reads the document, not the
    // dictionary cache), then save the identical list once more: that second eviction happens
    // strictly after the commit, so the next menu read rebuilds from the probe-bearing state.
    {
      const deadline = Date.now() + 5000;
      while (Date.now() < deadline) {
        const stored = await readTranslations();
        if (stored.some(t => t.context === 'Content Types' && t.key === typeName && t.value === probeValue)) break;
        await page.waitForTimeout(250);
      }
      await saveTranslations(withProbe);
    }

    let after = await navMenu(probeCulture);
    const deadline = Date.now() + 5000;
    while (Date.now() < deadline) {
      const child = newBranchOf(after)?.items?.find(item => item.textKey === typeName);
      if (child?.text === probeValue) break;
      await page.waitForTimeout(250);
      after = await navMenu(probeCulture);
    }

    const probed = newBranchOf(after)?.items?.find(item => item.textKey === typeName);
    results.push({
      name: 'new-menu-uses-content-type-translation',
      pass: probed?.text === probeValue,
      message: `type="${typeName}" rendered="${probed?.text}"`,
    });

    const english = await navMenu('en-US');
    const englishChild = newBranchOf(english)?.items?.find(item => item.textKey === typeName);
    results.push({
      name: 'other-cultures-unaffected',
      pass: englishChild?.text === typeName,
      message: `en-US rendered="${englishChild?.text}"`,
    });
  } finally {
    await saveTranslations(original).catch(() => {});
    // Orchard's Save replaces the culture's list with what the editor enumerates, and the
    // upstream admin menu providers enumerate top-level nodes only - so both saves above
    // silently dropped every seeded child-caption translation. Re-running the provider sync
    // reseeds exactly the missing ones, leaving the tenant as this check found it.
    await page.evaluate(async (antiforgery) => {
      await fetch('/api/crest/admin-menus/sync-providers', {
        method: 'POST',
        credentials: 'include',
        headers: { [antiforgery.headerName]: antiforgery.requestToken },
      }).catch(() => {});
    }, await fetchAntiforgeryToken(page, ctx.baseUrl)).catch(() => {});
    await navMenu(null).catch(() => {});
  }

  return results;
};
