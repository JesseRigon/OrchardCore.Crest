// Converted from OrchardCore.Crest.Icons/tests/playwright/iconify-remote-fallback-api.js.
//
// REWRITTEN (Phase 8 triage): the original asserted that a Debug build disables the
// local App_Data Iconify mirror ("disabled for this build" lastError). That premise no
// longer exists in this codebase — IconifyLocalMirrorBuildOptions.Enabled is
// unconditionally true and no build flavor disables the mirror, so the old assertion
// could never pass. What remains worth proving here: with the PUBLIC Iconify provider
// configured, icon search end-to-end returns a real mdi:home SVG — via the local
// mirror when its App_Data cache exists, via the remote API otherwise. The
// local-vs-custom-provider cache semantics are covered by iconify-local-mirror.js.
const { fetchAntiforgeryToken } = require('../harness/antiforgery');

async function api(page, path, options = {}) {
  return page.evaluate(async ({ path, options }) => {
    const response = await fetch(path, { credentials: 'include', ...options, headers: { 'content-type': 'application/json', ...(options.headers || {}) } });
    const text = await response.text();
    return { status: response.status, ok: response.ok, body: text ? JSON.parse(text) : null };
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
    const save = await api(page, '/api/crest/icons/providers', {
      method: 'PUT',
      headers: antiforgeryHeaders,
      body: JSON.stringify({ iconify: { enabled: true, baseUrl: 'https://api.iconify.design', apiKey: null, apiKeyHeader: null, prefixes: ['mdi'] } }),
    });
    results.push({ name: 'save-public-iconify-settings', pass: save.ok, message: `status=${save.status}` });

    // The local mirror is compiled into every build now, but its App_Data cache is an
    // opt-in sync that a fresh tenant has never run (dev.sh clean wipes App_Data, and
    // the mirror is absent from source too). So three states are all legitimate:
    // available; unavailable after a failed sync (lastError set); or unavailable and
    // never synced (lastError null, lastSyncUtc null). What must always hold is that
    // the endpoint answers with a coherent, self-consistent status.
    const status = await api(page, '/api/crest/icons/providers/iconify/local');
    const body = status.body ?? {};
    const neverSynced = body.isAvailable === false && !body.lastError && !body.lastSyncUtc;
    const failedSync = body.isAvailable === false && Boolean(body.lastError);
    const coherent = status.ok && (body.isAvailable === true || neverSynced || failedSync);
    results.push({
      name: 'local-mirror-status-is-coherent',
      pass: coherent,
      message: `status=${status.status} isAvailable=${body.isAvailable} lastError=${body.lastError ?? 'null'} lastSyncUtc=${body.lastSyncUtc ?? 'null'}`,
    });

    const search = await api(page, '/api/crest/icons?library=iconify.mdi&query=home&skip=0&take=20');
    const home = search.body?.items?.find(item => item.key === 'iconify.mdi/current/default/home' && item.svgMarkup?.includes('<svg'));
    results.push({
      name: 'public-provider-search-returns-mdi-home',
      pass: search.ok && Boolean(home),
      message: `status=${search.status} total=${search.body?.total} found=${Boolean(home)}`,
    });
  } finally {
    await api(page, '/api/crest/icons/providers', { method: 'PUT', headers: antiforgeryHeaders, body: JSON.stringify(original.body) }).catch(() => {});
  }

  return results;
};
