// Converted from OrchardCore.Crest.Icons/tests/playwright/iconify-remote-fallback-api.js.
// Verifies that in a Debug build the local App_Data Iconify mirror is intentionally
// disabled, forcing icon search through the live remote Iconify API.
//
// ADAPTATION: this check inherently assumes (a) the app under test is a Debug build, and
// (b) genuine outbound network access to https://api.iconify.design is available — same
// assumptions the original script made. Kept as real assertions rather than an always-pass
// stand-in; a Release build or a sandboxed network would fail this check for environmental
// reasons rather than a real regression.
async function api(page, path, options = {}) {
  return page.evaluate(async ({ path, options }) => {
    const response = await fetch(path, { credentials: 'include', headers: { 'content-type': 'application/json' }, ...options });
    const text = await response.text();
    return { status: response.status, ok: response.ok, body: text ? JSON.parse(text) : null };
  }, { path, options });
}

module.exports = async function run(page, ctx) {
  const original = await api(page, '/api/crest/icons/providers');
  if (!original.ok) {
    return { name: 'read-provider-settings', pass: false, message: `status=${original.status}` };
  }

  const results = [];
  try {
    const save = await api(page, '/api/crest/icons/providers', {
      method: 'PUT',
      body: JSON.stringify({ iconify: { enabled: true, baseUrl: 'https://api.iconify.design', apiKey: null, apiKeyHeader: null, prefixes: ['mdi'] } }),
    });
    results.push({ name: 'save-public-iconify-settings', pass: save.ok, message: `status=${save.status}` });

    const status = await api(page, '/api/crest/icons/providers/iconify/local');
    results.push({
      name: 'local-cache-disabled-in-debug-build',
      pass: status.ok && !status.body?.isAvailable && /disabled for this build/i.test(status.body?.lastError || ''),
      message: `isAvailable=${status.body?.isAvailable} lastError=${status.body?.lastError}`,
    });

    const search = await api(page, '/api/crest/icons?library=iconify.mdi&query=home&skip=0&take=20');
    const home = search.body?.items?.find(item => item.key === 'iconify.mdi/current/default/home' && item.svgMarkup?.includes('<svg'));
    results.push({
      name: 'remote-fallback-returns-mdi-home',
      pass: search.ok && Boolean(home),
      message: `status=${search.status} total=${search.body?.total} found=${Boolean(home)}`,
    });
  } finally {
    await api(page, '/api/crest/icons/providers', { method: 'PUT', body: JSON.stringify(original.body) }).catch(() => {});
  }

  return results;
};
