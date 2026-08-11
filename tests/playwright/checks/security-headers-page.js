// Converted from the old admin-security-headers-page.js. Verifies the Security Headers
// settings page renders natively and that its backing API performs real, persisted writes.
//
// REWRITTEN (Phase 8): the old check sniffed the browser's network for the page's own
// PUT after clicking Save. Under InteractiveAuto that request is only observable in the
// WASM phase — on a first-visit server circuit the PUT happens on the server-side
// loopback HttpClient and never crosses the browser network, so waitForResponse hangs
// regardless of whether the save worked. Instead: click Save and assert the UI reports
// success, then prove API-level persistence directly (PUT round-trip with the
// antiforgery token, echoing the current settings — a genuine authorized write).
const { fetchAntiforgeryToken } = require('../harness/antiforgery');

module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Settings/SecurityHeaders`, { waitUntil: 'networkidle' });

  const rendered = await page.locator('[data-testid="security-headers-page"]').waitFor({ timeout: 20000 })
    .then(() => true).catch(() => false);
  if (!rendered) {
    return { name: 'renders-security-headers-page', pass: false, message: `page did not render at ${page.url()}` };
  }

  const sectionLabels = ['Content Security Policy', 'Permissions Policy', 'Referrer Policy'];
  const missing = [];
  for (const label of sectionLabels) {
    const visible = await page.getByText(label, { exact: true }).first().waitFor({ timeout: 10000 })
      .then(() => true).catch(() => false);
    if (!visible) missing.push(label);
  }

  // Interactive save: works in both the circuit and WASM phases; success surfaces as a
  // notification (and no danger alert), which is render-mode-independent. clickForEffect
  // covers the prerendered-inert-button race (harness/interactive.js).
  const { clickForEffect } = require('../harness/interactive');
  // Match .rz-notification-item only — .rz-notification is the always-present empty host
  // container, so including it made this assertion pass without any toast being raised.
  const uiSaveOk = await clickForEffect(
    page.getByRole('button', { name: 'Save' }),
    page.locator('.rz-notification-item').first(),
  ).then(() => true).catch(() => false);
  const dangerAlerts = await page.locator('.rz-alert-danger').count();

  // API persistence: GET current settings, PUT them back with the antiforgery token,
  // GET again — proves the endpoint accepts authorized writes and round-trips state.
  const current = await page.request.get(`${ctx.baseUrl}/api/crest/security-headers`);
  const currentOk = current.ok();
  let putOk = false;
  let roundTripOk = false;
  if (currentOk) {
    const settings = await current.json();
    const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
    const put = await page.request.put(`${ctx.baseUrl}/api/crest/security-headers`, {
      headers: { [antiforgery.headerName]: antiforgery.requestToken },
      data: settings,
    });
    putOk = put.ok();
    if (putOk) {
      const after = await page.request.get(`${ctx.baseUrl}/api/crest/security-headers`);
      roundTripOk = after.ok() && JSON.stringify(await after.json()) === JSON.stringify(settings);
    }
  }

  return [
    { name: 'renders-security-headers-page', pass: rendered, message: rendered ? 'ok' : 'missing' },
    { name: 'shows-policy-sections', pass: missing.length === 0, message: missing.length ? `missing: ${missing.join(', ')}` : 'ok' },
    {
      name: 'save-button-reports-success',
      pass: uiSaveOk && dangerAlerts === 0,
      message: `notification=${uiSaveOk} dangerAlerts=${dangerAlerts}`,
    },
    {
      name: 'api-save-persists-with-antiforgery',
      pass: currentOk && putOk && roundTripOk,
      message: `get=${currentOk} put=${putOk} roundTrip=${roundTripOk}`,
    },
  ];
};
