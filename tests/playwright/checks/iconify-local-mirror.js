// Converted from OrchardCore.Crest.Icons/tests/playwright/iconify-local-mirror-api.js.
// Verifies the App_Data local Iconify mirror is used for search when the provider is
// configured with the public Iconify baseUrl, and is bypassed (no stale results) when a
// deliberately unreachable custom baseUrl (127.0.0.1:9) is configured instead. Restores
// whatever provider settings were in place beforehand, since this now runs inside a shared
// browser alongside other icon checks.
async function api(page, path, options = {}) {
  return page.evaluate(async ({ path, options }) => {
    const response = await fetch(path, {
      credentials: 'include',
      headers: { 'content-type': 'application/json', ...(options.headers || {}) },
      ...options,
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

  const results = [];
  try {
    const savePublic = await api(page, '/api/crest/icons/providers', {
      method: 'PUT',
      body: JSON.stringify({ iconify: { enabled: true, baseUrl: 'https://api.iconify.design', apiKey: null, apiKeyHeader: null, prefixes: ['mdi'] } }),
    });
    results.push({ name: 'save-public-iconify-settings', pass: savePublic.ok, message: `status=${savePublic.status}` });

    const status = await api(page, '/api/crest/icons/providers/iconify/local');
    results.push({
      name: 'local-cache-available',
      pass: status.ok && Boolean(status.body?.isAvailable),
      message: `status=${status.status} isAvailable=${status.body?.isAvailable}`,
    });

    const localSearch = await api(page, '/api/crest/icons?library=iconify.mdi&query=home&skip=0&take=20');
    const homeIcon = localSearch.body?.items?.find(item => item.key === 'iconify.mdi/current/default/home' && item.svgMarkup?.includes('<svg'));
    results.push({
      name: 'local-search-finds-home-icon',
      pass: localSearch.ok && Boolean(homeIcon),
      message: `status=${localSearch.status} found=${Boolean(homeIcon)}`,
    });

    const saveCustom = await api(page, '/api/crest/icons/providers', {
      method: 'PUT',
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
    await api(page, '/api/crest/icons/providers', { method: 'PUT', body: JSON.stringify(original.body) }).catch(() => {});
  }

  return results;
};
