const { fetchAntiforgeryToken } = require('./antiforgery');

// Adds cultures to the tenant's supported list for the duration of a check, returning a
// restore function for the check's `finally`.
//
// Checks that exercise a specific culture must provision it themselves: a freshly provisioned
// FruitfulSetup tenant supports only en-US/es-ES, so a check that assumes e.g. 'français' is in
// the culture picker only ever worked on tenants where some earlier check had leaked its test
// cultures into the settings (localization-sequential-settings did exactly that before it
// learned to restore them). Self-provisioning keeps every check honest on a fresh tenant and
// transparent on a hand-configured one - the restore puts back whatever was found at entry.
async function ensureCultures(page, baseUrl, cultures) {
  const getJson = () => page.evaluate(async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/crest/localization`, { credentials: 'include' });
    if (!response.ok) throw new Error(`GET /api/crest/localization failed: ${response.status}`);
    return response.json();
  }, baseUrl);

  const putJson = async (body) => {
    const antiforgery = await fetchAntiforgeryToken(page, baseUrl);
    return page.evaluate(async ({ baseUrl, body, antiforgery }) => {
      const response = await fetch(`${baseUrl}/api/crest/localization`, {
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
        body: JSON.stringify(body),
      });
      if (!response.ok) throw new Error(`PUT /api/crest/localization failed: ${response.status} ${await response.text()}`);
      return response.json();
    }, { baseUrl, body, antiforgery });
  };

  const original = await getJson();
  await putJson({
    ...original,
    supportedCultures: Array.from(new Set([...(original.supportedCultures || []), ...cultures])),
  });

  return async () => {
    await putJson(original).catch(() => {});
  };
}

module.exports = { ensureCultures };
