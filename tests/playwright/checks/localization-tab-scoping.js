const { ensureTestUser } = require('../harness/testUsers');
const { loginAsUser } = require('../harness/auth');
const { createInstance } = require('../harness/instance');

// Verifies the per-tab session override scoping claim in plans/user-localization.md's
// "Per-tab and per-user override scoping": sessionStorage is genuinely per-tab, so two
// independently-opened tabs (here: two separate browser contexts, which get their own
// sessionStorage the same way two independently-opened tabs would) for the SAME user
// must be able to hold two different session overrides simultaneously without either
// one clobbering the other.
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
  const testUser = await ensureTestUser(page, ctx.baseUrl, '');
  const results = [];

  const tabA = await createInstance();
  const tabB = await createInstance();
  try {
    await loginAsUser(tabA.page, ctx.baseUrl, testUser);
    await loginAsUser(tabB.page, ctx.baseUrl, testUser);

    await tabA.page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });
    await tabB.page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });

    await pickCulture(tabA.page, 'French');
    await pickCulture(tabB.page, 'German');

    // Re-navigate (not just re-check in place) so each tab's own resolution runs again
    // against its own sessionStorage, the way a fresh page load would.
    await tabA.page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });
    await tabB.page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });

    const cultureA = await resolvedCulture(tabA.page);
    const cultureB = await resolvedCulture(tabB.page);

    results.push({ name: 'tab-a-keeps-its-own-override', pass: cultureA === 'fr-FR', message: `resolved=${cultureA}` });
    results.push({ name: 'tab-b-keeps-its-own-override', pass: cultureB === 'de-DE', message: `resolved=${cultureB}` });
    results.push({
      name: 'tabs-do-not-clobber-each-other',
      pass: cultureA !== cultureB,
      message: `a=${cultureA} b=${cultureB}`,
    });
  } finally {
    await tabA.browser.close();
    await tabB.browser.close();
  }

  return results;
};
