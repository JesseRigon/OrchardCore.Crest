const { ensureTestUser } = require('../harness/testUsers');
const { loginAsUser } = require('../harness/auth');
const { createInstance } = require('../harness/instance');
const { fetchAntiforgeryToken } = require('../harness/antiforgery');

// Drives the exact sequence requested in plans/user-localization-testing.md against a
// running tenant: enable es/fr/de alongside en, then walk the 5-rung priority chain
// (plans/user-localization.md's "Resolution architecture") one setting at a time -
// tenant default -> admin default -> user default -> session override - checking the
// resolved culture after each stage before moving to the next.
//
// Settings are driven via the same API the Localization admin page calls
// (api/crest/localization, api/crest/localization/me) rather than the dropdown UI, since
// that's faster and less brittle than driving three CrestDropDown widgets - the UI itself
// is covered separately by localization-page.js. The session override (rung 1) genuinely
// needs the titlebar picker since it's a client-only sessionStorage concept with no API.
const CULTURES = ['en-US', 'es-ES', 'fr-FR', 'de-DE'];

async function putJson(page, baseUrl, path, body) {
  const antiforgery = await fetchAntiforgeryToken(page, baseUrl);

  return page.evaluate(async ({ baseUrl, path, body, antiforgery }) => {
    const headers = { 'Content-Type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken };
    const response = await fetch(`${baseUrl}${path}`, {
      method: 'PUT',
      credentials: 'include',
      headers,
      body: JSON.stringify(body),
    });
    if (!response.ok) {
      throw new Error(`PUT ${path} failed: ${response.status} ${await response.text()}`);
    }
    return response.json();
  }, { baseUrl, path, body, antiforgery });
}

async function getJson(page, baseUrl, path) {
  return page.evaluate(async ({ baseUrl, path }) => {
    const response = await fetch(`${baseUrl}${path}`, { credentials: 'include' });
    if (!response.ok) throw new Error(`GET ${path} failed: ${response.status}`);
    return response.json();
  }, { baseUrl, path });
}

async function resolvedCultureFor(page, baseUrl, adminPath) {
  await page.goto(`${baseUrl}${adminPath}`, { waitUntil: 'networkidle' });
  const hook = page.locator('[data-testid="resolved-culture"]');
  const found = await hook.waitFor({ timeout: 10000 }).then(() => true).catch(() => false);
  if (!found) return null;
  return hook.textContent();
}

module.exports = async function run(page, ctx) {
  const results = [];
  const localization = await getJson(page, ctx.baseUrl, '/api/crest/localization');

  const withAllCultures = { ...localization, supportedCultures: CULTURES, defaultCulture: 'en-US', adminDefaultCulture: null };
  await putJson(page, ctx.baseUrl, '/api/crest/localization', withAllCultures);
  results.push({ name: 'enables-es-fr-de', pass: true, message: `supported=${CULTURES.join(',')}` });

  // Stage 1: tenant default = en, nothing else set -> admin user with no personal
  // default and no override should resolve to en everywhere.
  const admin1 = await resolvedCultureFor(page, ctx.baseUrl, '/Admin/Settings/localization');
  results.push({ name: 'stage1-tenant-default-en', pass: admin1 === 'en-US', message: `resolved=${admin1}` });

  // Stage 2: admin default = fr -> same admin user (no personal default, no override)
  // should now resolve to fr under /admin, since rung 3 (admin default) beats rung 5
  // (tenant default) once the user default rung is empty.
  await putJson(page, ctx.baseUrl, '/api/crest/localization', { ...withAllCultures, adminDefaultCulture: 'fr-FR' });
  const admin2 = await resolvedCultureFor(page, ctx.baseUrl, '/Admin/Settings/localization');
  results.push({ name: 'stage2-admin-default-fr', pass: admin2 === 'fr-FR', message: `resolved=${admin2}` });

  // Stage 3+4: testuser's stored default = es, then a session override = de - both need
  // a genuinely separate authenticated session (own cookies/sessionStorage), so this
  // opens its own browser context rather than reusing the shared admin page/session.
  const testUser = await ensureTestUser(page, ctx.baseUrl, '');
  results.push({ name: 'testuser-provisioned', pass: !!testUser.id, message: `id=${testUser.id}` });

  const { browser: userBrowser, page: userPage } = await createInstance();
  try {
    await loginAsUser(userPage, ctx.baseUrl, testUser);

    // Stage 3: testuser's stored default = es -> es wins over the admin default (rung 2
    // outranks rung 3) even though admin default is still fr from stage 2.
    await putJson(userPage, ctx.baseUrl, '/api/crest/localization/me', { culture: 'es-ES' });
    const user3 = await resolvedCultureFor(userPage, ctx.baseUrl, '/Admin');
    results.push({ name: 'stage3-user-default-es', pass: user3 === 'es-ES', message: `resolved=${user3}` });

    // Stage 4: session override = de, via the titlebar culture picker (rung 1 - purely
    // client-side, no API - see plans/user-localization.md's "Per-tab and per-user
    // override scoping"). de must win over the stored es default from stage 3.
    const trigger = userPage.locator('.admin-titlebar__culture-selector .crest-dropdown__trigger');
    await trigger.click();
    await userPage.locator('[role="option"]', { hasText: 'German' }).first().click().catch(async () => {
      await userPage.getByText('de-DE', { exact: false }).first().click();
    });
    await userPage.waitForTimeout(300);
    const user4 = await resolvedCultureFor(userPage, ctx.baseUrl, '/Admin');
    results.push({ name: 'stage4-session-override-de', pass: user4 === 'de-DE', message: `resolved=${user4}` });
  } finally {
    await userBrowser.close();
  }

  return results;
};
