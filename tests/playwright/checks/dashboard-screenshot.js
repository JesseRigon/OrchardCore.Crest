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

  return captureAndCompare(page, 'admin-dashboard', {
    outputRoot: ctx.outputRoot,
    mask: (await featureHash.count()) ? [featureHash] : [],
  });
};
