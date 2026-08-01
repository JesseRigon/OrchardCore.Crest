const { ensureTestUser } = require('../harness/testUsers');
const { loginAsUser, logout } = require('../harness/auth');
const { createInstance } = require('../harness/instance');

// Verifies plans/user-localization.md's "Multiple logins (switch-user)" requirement:
// switching identity within the same browser tab must resolve the NEW user's own
// stored default/override, never carry forward the previous user's session override
// (the override key is scoped by user name - crest-culture-override:{userName} - so a
// stale value under the old user's key must simply not apply to the new user).
async function pickCulture(page, label) {
  const trigger = page.locator('.admin-titlebar__culture-selector .crest-dropdown__trigger');
  await trigger.click();
  await page.locator('[role="option"]', { hasText: label }).first().click();
  await page.waitForTimeout(300);
}

async function resolvedCulture(page) {
  const hook = page.locator('[data-testid="resolved-culture"]');
  await hook.waitFor({ timeout: 10000 }).catch(() => {});
  return hook.textContent().catch(() => null);
}

module.exports = async function run(page, ctx) {
  const userA = await ensureTestUser(page, ctx.baseUrl, '');
  const userB = await ensureTestUser(page, ctx.baseUrl, '2');

  const results = [];
  const session = await createInstance();
  try {
    await loginAsUser(session.page, ctx.baseUrl, userA);
    await session.page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });
    await pickCulture(session.page, 'French');
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
  }

  return results;
};
