const { captureAndCompare } = require('../harness/screenshot-diff');

// Converted from the old admin-page-screenshot.js. Visual-only: does the admin
// dashboard render pixel-identical to the last promoted baseline?
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });
  await page.locator('.admin-shell').waitFor({ timeout: 20000 });
  await page.locator('.primary-nav-menu').waitFor({ timeout: 20000 });

  // The feature hash changes on every request (looks like it embeds something
  // non-deterministic) — mask it so it never registers as a false-positive diff.
  const featureHash = page.getByText(/Feature hash:/);
  // "Enabled features: N" is equally volatile, just on a slower clock: features-page runs
  // immediately after this check and enables a feature that stays enabled, so the count is
  // 48 on the first run after a dev.sh reset and 49 on every run thereafter. That made the
  // baseline depend on how many times the suite had been run against the tenant. Roles is
  // masked for the same reason — any check that adds a role would shift it.
  const enabledFeatures = page.getByText(/Enabled features:/);
  const roles = page.getByText(/^Roles:/);

  const mask = [];
  for (const locator of [featureHash, enabledFeatures, roles]) {
    if (await locator.count()) mask.push(locator);
  }

  return captureAndCompare(page, 'admin-dashboard', {
    outputRoot: ctx.outputRoot,
    mask,
  });
};
