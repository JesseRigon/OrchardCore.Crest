const { drainConsoleErrors } = require('../harness/instance');
const { fetchAntiforgeryToken } = require('../harness/antiforgery');

// Converted from the old admin-templates-page.js (originally a dense one-liner). The
// original didn't assert on console errors, so no `no-console-errors` result is added
// here — but the shared buffer is still drained so this page's own noise doesn't bleed
// into later checks in the suite that do assert on it.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Templates`, { waitUntil: 'networkidle' });
  const rendered = await page.locator('[data-testid="templates-page"]').waitFor({ timeout: 20000 }).then(() => true).catch(() => false);

  const legacyFrame = await page.locator('iframe').count();

  // Round-trips a throwaway template through the native API to prove the page sits on
  // top of real Orchard template APIs, then cleans it up immediately.
  // Mutating Crest APIs are antiforgery-protected - see harness/antiforgery.js.
  const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);

  const name = `crest-probe-${Date.now()}`;
  const saved = await page.request.put(`${ctx.baseUrl}/api/crest/templates/${name}`, {
    headers: { [antiforgery.headerName]: antiforgery.requestToken },
    data: { description: 'probe', content: 'Hello' },
  });
  let cleanupOk = false;
  if (saved.ok()) {
    const deleted = await page.request.delete(`${ctx.baseUrl}/api/crest/templates/${name}`, {
      headers: { [antiforgery.headerName]: antiforgery.requestToken },
    });
    cleanupOk = deleted.ok();
  }

  drainConsoleErrors(ctx.consoleErrors);

  return [
    { name: 'renders-templates-page', pass: rendered, message: rendered ? 'ok' : 'templates-page testid not found' },
    { name: 'no-legacy-iframe', pass: legacyFrame === 0, message: `iframe count=${legacyFrame}` },
    {
      name: 'native-create-and-delete',
      pass: saved.ok() && cleanupOk,
      message: saved.ok() ? `probe=${name}` : `save failed: ${saved.status()}`,
    },
  ];
};
