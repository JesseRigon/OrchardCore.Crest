// Converted from OrchardCore.Crest.Icons/tests/playwright/iconify-local-mirror-api.js.
// Verifies the App_Data local Iconify mirror is used for search when the provider is
// configured with the public Iconify baseUrl, and is bypassed (no stale results) when a
// deliberately unreachable custom baseUrl (127.0.0.1:9) is configured instead. Restores
// whatever provider settings were in place beforehand, since this now runs inside a shared
// browser alongside other icon checks.
const { fetchAntiforgeryToken } = require('../harness/antiforgery');

async function api(page, path, options = {}) {
  return page.evaluate(async ({ path, options }) => {
    const response = await fetch(path, {
      credentials: 'include',
      ...options,
      // Merged AFTER the options spread, or options.headers (the antiforgery token
      // alone) would replace this object entirely and drop the content-type → 415.
      headers: { 'content-type': 'application/json', ...(options.headers || {}) },
    });
    const text = await response.text();
    let body = null;
    try {
      body = text ? JSON.parse(text) : null;
    } catch {
      body = text;
    }
    return { status: response.status, ok: response.ok, body };
  }, { path, options });
}

module.exports = async function run(page, ctx) {
  const original = await api(page, '/api/crest/icons/providers');
  if (!original.ok) {
    return { name: 'read-provider-settings', pass: false, message: `status=${original.status}` };
  }

  // Mutating Crest APIs are antiforgery-protected - see harness/antiforgery.js.
  const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
  const antiforgeryHeaders = { [antiforgery.headerName]: antiforgery.requestToken };

  const results = [];
  try {
    const savePublic = await api(page, '/api/crest/icons/providers', {
      method: 'PUT',
      headers: antiforgeryHeaders,
      body: JSON.stringify({ iconify: { enabled: true, baseUrl: 'https://api.iconify.design', apiKey: null, apiKeyHeader: null, prefixes: ['mdi'] } }),
    });
    results.push({ name: 'save-public-iconify-settings', pass: savePublic.ok, message: `status=${savePublic.status}` });

    // The App_Data mirror is an opt-in sync, not a build output — a fresh tenant has
    // never populated it (dev.sh clean wipes App_Data, and the source-side
    // icons/Sources/IconifyCache is absent too). Everything below this point asserts
    // mirror-vs-remote cache semantics, which are meaningless without a mirror, so
    // report the environment condition and stop rather than emitting false failures.
    const status = await api(page, '/api/crest/icons/providers/iconify/local');
    const mirrorAvailable = status.ok && Boolean(status.body?.isAvailable);
    results.push({
      name: 'local-cache-status-readable',
      pass: status.ok,
      message: `status=${status.status} isAvailable=${status.body?.isAvailable} lastSyncUtc=${status.body?.lastSyncUtc ?? 'null'}`,
    });
    if (!mirrorAvailable) {
      results.push({
        name: 'local-mirror-cache-semantics',
        pass: true,
        status: 'skipped',
        message: 'no local Iconify mirror synced on this tenant — cache-vs-remote assertions skipped',
      });
      return results;
    }

    const localSearch = await api(page, '/api/crest/icons?library=iconify.mdi&query=home&skip=0&take=20');
    const homeIcon = localSearch.body?.items?.find(item => item.key === 'iconify.mdi/current/default/home' && item.svgMarkup?.includes('<svg'));
    results.push({
      name: 'local-search-finds-home-icon',
      pass: localSearch.ok && Boolean(homeIcon),
      message: `status=${localSearch.status} found=${Boolean(homeIcon)}`,
    });

    const saveCustom = await api(page, '/api/crest/icons/providers', {
      method: 'PUT',
      headers: antiforgeryHeaders,
      body: JSON.stringify({ iconify: { enabled: true, baseUrl: 'http://127.0.0.1:9', apiKey: null, apiKeyHeader: null, prefixes: ['mdi'] } }),
    });
    results.push({ name: 'save-custom-iconify-settings', pass: saveCustom.ok, message: `status=${saveCustom.status}` });

    // baseUrl 127.0.0.1:9 is deliberately unreachable, so a search under this config must not
    // silently fall back to the public App_Data mirror's cached results.
    const customSearch = await api(page, '/api/crest/icons?library=iconify.mdi&query=home&skip=0&take=20');
    const customUsedLocalCache = customSearch.body?.items?.some(item => item.key === 'iconify.mdi/current/default/home');
    results.push({
      name: 'custom-provider-bypasses-public-cache',
      pass: customSearch.ok && !customUsedLocalCache,
      message: `status=${customSearch.status} usedLocalCache=${Boolean(customUsedLocalCache)}`,
    });
  } finally {
    await api(page, '/api/crest/icons/providers', { method: 'PUT', headers: antiforgeryHeaders, body: JSON.stringify(original.body) }).catch(() => {});
  }

  return results;
};
