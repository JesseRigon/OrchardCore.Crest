const { ensureTestUser } = require('../harness/testUsers');
const { loginAsUser } = require('../harness/auth');
const { createInstance } = require('../harness/instance');

// Verifies plans/user-localization.md's phase 16: a same-origin new tab opened from an
// existing session (window.open/target="_blank") should inherit the source tab's
// sessionStorage - and therefore its session culture override - per standard browser
// behavior, distinct from an independently-opened tab which correctly starts blank
// (covered by localization-tab-scoping.js). This exercises the actual browser mechanism
// (window.open) rather than a specific Crest link, since the claim being verified is
// about browser storage-cloning semantics, not any one link's wiring.
async function pickCulture(page, label) {
  const trigger = page.locator('.admin-titlebar__culture-selector');
  await trigger.click();
  await page.locator('[role="option"]', { hasText: label }).first().click();
  await page.waitForTimeout(300);
}

async function resolvedCulture(page) {
  const hook = page.locator('[data-testid="resolved-culture"]');
  await hook.waitFor({ state: 'attached', timeout: 15000 }).catch(() => {});
  return hook.textContent().catch(() => null);
}

module.exports = async function run(page, ctx) {
  const testUser = await ensureTestUser(page, ctx.baseUrl, '');
  const results = [];

  const source = await createInstance();
  try {
    await loginAsUser(source.page, ctx.baseUrl, testUser);
    await source.page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });
    await pickCulture(source.page, 'français');

    const [newPage] = await Promise.all([
      source.context.waitForEvent('page'),
      source.page.evaluate((url) => window.open(url, '_blank'), `${ctx.baseUrl}/Admin`),
    ]);
    await newPage.waitForLoadState('networkidle');

    const inheritedCulture = await resolvedCulture(newPage);
    results.push({
      name: 'same-origin-new-tab-inherits-override',
      pass: inheritedCulture === 'fr-FR',
      message: `resolved=${inheritedCulture} (expected fr-FR from the source tab's override)`,
    });
  } finally {
    await source.browser.close();
  }

  return results;
};
