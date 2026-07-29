const { severeConsoleErrors, drainConsoleErrors } = require('../harness/instance');

// Converted from the old admin-features-page.js. Same assertions, minus the per-script
// browser launch/login boilerplate — that now lives once in the harness.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Features`, { waitUntil: 'networkidle' });
  await page.locator('[data-testid="features-page"]').waitFor({ timeout: 20000 });
  await page.getByRole('heading', { name: 'Features', exact: true, level: 4 }).waitFor({ timeout: 20000 });

  const cards = page.locator('[data-feature-id]');
  const initialCount = await cards.count();

  const featureIds = await cards.evaluateAll(elements => elements.map(el => el.getAttribute('data-feature-id')));
  const targetId = featureIds.find(Boolean);

  let filteredIds = [];
  if (targetId) {
    const search = page.getByPlaceholder('Search name, ID, category, description, or dependency...');
    await search.fill(targetId);
    await search.press('Tab');
    await page.waitForTimeout(250);
    filteredIds = await cards.evaluateAll(elements => elements.map(el => el.getAttribute('data-feature-id')));
    await search.fill('');
    await search.press('Tab');
    await page.waitForTimeout(250);
  }

  const categories = await page.locator('[data-testid="feature-category"]').count();
  const statusBadges = await page.getByText(/^(Enabled|Disabled)$/).count();
  const legacyFrame = await page.locator('iframe').count();
  const errors = severeConsoleErrors(drainConsoleErrors(ctx.consoleErrors));

  return [
    { name: 'renders-feature-cards', pass: initialCount >= 2, message: `${initialCount} features` },
    {
      name: 'search-filters-by-id',
      pass: Boolean(targetId) && filteredIds.includes(targetId) && filteredIds.length < initialCount,
      message: `initial=${initialCount} filtered=${filteredIds.length} target=${targetId ?? 'none'}`,
    },
    {
      name: 'grouped-with-status',
      pass: categories >= 1 && statusBadges >= initialCount,
      message: `categories=${categories} badges=${statusBadges}`,
    },
    { name: 'no-legacy-iframe', pass: legacyFrame === 0, message: `iframe count=${legacyFrame}` },
    { name: 'no-console-errors', pass: errors.length === 0, message: errors.join(' | ') || 'clean' },
  ];
};
