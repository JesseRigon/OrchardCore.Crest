const { ensureCultures } = require('../harness/localization');
const { ensureTestUser } = require('../harness/testUsers');
const { loginAsUser, logout } = require('../harness/auth');
const { createInstance } = require('../harness/instance');
const { fetchAntiforgeryToken } = require('../harness/antiforgery');

// Verifies plans/user-localization.md's "Multiple logins (switch-user)" requirement:
// switching identity within the same browser tab must resolve the NEW user's own
// stored default/override, never carry forward the previous user's session override
// (the override key is scoped by user name - crest-culture-override:{userName} - so a
// stale value under the old user's key must simply not apply to the new user).
//
// User A's override is deliberately set to a culture that ISN'T also the tenant's
// current AdminDefaultCulture (rung 3) - if AdminDefaultCulture happened to point at the
// same culture, user B (who has no override/stored default of their own) would
// legitimately resolve to that same value via rung 3, and the assertion below couldn't
// tell a real override leak apart from coincidental agreement with the tenant setting.
// This clear keeps the check self-contained rather than depending on tenant state: it
// holds whether the tenant was configured by hand or by another check.
async function clearAdminDefaultCulture(page, baseUrl) {
  const localization = await page.evaluate(async (baseUrl) => {
    const r = await fetch(`${baseUrl}/api/crest/localization`, { credentials: 'include' });
    return r.json();
  }, baseUrl);
  const antiforgery = await fetchAntiforgeryToken(page, baseUrl);
  await page.evaluate(async ({ baseUrl, antiforgery, body }) => {
    const headers = { 'Content-Type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken };
    await fetch(`${baseUrl}/api/crest/localization`, {
      method: 'PUT', credentials: 'include', headers,
      body: JSON.stringify({ ...body, adminDefaultCulture: null }),
    });
  }, { baseUrl, antiforgery, body: localization });
}
async function pickCulture(page, label, expectedCulture) {
  // Both clicks can land on prerendered, not-yet-interactive elements — retry the
  // whole pick until the resolved-culture hook reflects the expected value (or the
  // attempts run out, letting the caller's assertion report the real resolved value).
  const trigger = page.locator('.admin-titlebar__culture-selector');
  for (let attempt = 0; attempt < 4; attempt++) {
    await trigger.click().catch(() => {});
    const option = page.locator('[role="option"]', { hasText: label }).first();
    const optionShown = await option.waitFor({ timeout: 3000 }).then(() => true).catch(() => false);
    if (optionShown) {
      await option.click().catch(() => {});
    }
    await page.waitForTimeout(400);
    if (!expectedCulture) return;
    const resolved = await page.locator('[data-testid="resolved-culture"]').textContent().catch(() => null);
    if (resolved === expectedCulture) return;
  }
}

async function resolvedCulture(page) {
  const hook = page.locator('[data-testid="resolved-culture"]');
  await hook.waitFor({ state: 'attached', timeout: 15000 }).catch(() => {});
  return hook.textContent().catch(() => null);
}

module.exports = async function run(page, ctx) {
  // fr-FR is not in a fresh tenant's supported cultures - provision it for the check
  // and put the tenant back afterwards (see harness/localization.js).
  const restoreCultures = await ensureCultures(page, ctx.baseUrl, ['fr-FR']);
  const userA = await ensureTestUser(page, ctx.baseUrl, '');
  const userB = await ensureTestUser(page, ctx.baseUrl, '2');
  await clearAdminDefaultCulture(page, ctx.baseUrl);

  const results = [];
  const session = await createInstance();
  try {
    await loginAsUser(session.page, ctx.baseUrl, userA);
    await session.page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });
    await pickCulture(session.page, 'français', 'fr-FR');
    const beforeSwitch = await resolvedCulture(session.page);
    results.push({ name: 'user-a-override-applied', pass: beforeSwitch === 'fr-FR', message: `resolved=${beforeSwitch}` });

    await logout(session.page, ctx.baseUrl);
    await loginAsUser(session.page, ctx.baseUrl, userB);
    await session.page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });
    const afterSwitch = await resolvedCulture(session.page);

    results.push({
      name: 'user-b-does-not-inherit-user-a-override',
      pass: afterSwitch !== 'fr-FR',
      message: `resolved=${afterSwitch} (must not be fr-FR, that was user A's override)`,
    });
  } finally {
    await session.browser.close();
    await restoreCultures();
  }

  return results;
};
