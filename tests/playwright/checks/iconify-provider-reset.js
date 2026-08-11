// Converted from OrchardCore.Crest.Icons/tests/playwright/iconify-provider-reset-defaults.js.
// Verifies PUT /api/crest/icons/providers resets the Iconify provider to its default baseUrl
// and an empty prefix allow-list.
//
// ADAPTATION: the original script only asserted the reset and left the tenant in the reset
// state. Since this check now runs inside a shared, long-lived browser alongside other icon
// checks in the same suite run, it restores whatever settings were configured beforehand in
// a `finally` block so it doesn't leave later checks running against "no allowed prefixes".
const { fetchAntiforgeryToken } = require('../harness/antiforgery');

async function api(page, path, options = {}) {
  return page.evaluate(async ({ path, options }) => {
    const response = await fetch(path, {
      method: options.method,
      credentials: 'include',
      headers: { 'content-type': 'application/json', ...(options.headers || {}) },
      body: options.body,
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

  // Mutating Crest APIs are antiforgery-protected - see harness/antiforgery.js.
  const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
  const antiforgeryHeaders = { [antiforgery.headerName]: antiforgery.requestToken };

  try {
    const result = await api(page, '/api/crest/icons/providers', {
      method: 'PUT',
      headers: antiforgeryHeaders,
      body: JSON.stringify({ iconify: { enabled: true, baseUrl: 'https://api.iconify.design', apiKey: null, apiKeyHeader: null, prefixes: [] } }),
    });

    return {
      name: 'reset-iconify-provider-defaults',
      pass: result.ok && result.body?.iconify?.baseUrl === 'https://api.iconify.design' && result.body?.iconify?.prefixes?.length === 0,
      message: `status=${result.status} baseUrl=${result.body?.iconify?.baseUrl} prefixes=${JSON.stringify(result.body?.iconify?.prefixes)}`,
    };
  } finally {
    if (original.ok) {
      await api(page, '/api/crest/icons/providers', { method: 'PUT', headers: antiforgeryHeaders, body: JSON.stringify(original.body) }).catch(() => {});
    }
  }
};
