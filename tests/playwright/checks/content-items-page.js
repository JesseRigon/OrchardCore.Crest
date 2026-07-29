const { severeConsoleErrors, drainConsoleErrors } = require('../harness/instance');

// Converted from the old admin-content-items-page.js. Same assertions as the original;
// also clears the search field afterward (the original didn't bother, since it launched
// a fresh browser per script) so it doesn't leave filtered state behind for whatever
// check runs next against the shared page.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Contents/ContentItems`, { waitUntil: 'networkidle' });
  await page.locator('[data-testid="content-items-page"]').waitFor({ timeout: 20000 });
  await page.getByRole('heading', { name: 'Content Items', exact: true, level: 4 }).waitFor({ timeout: 20000 });
  await page.locator('[data-testid="content-items-grid"]').waitFor({ timeout: 20000 });

  const rows = page.locator('[data-testid="content-items-grid"] tbody tr');
  const count = await rows.count();

  let filtered = 0;
  let title = '';
  if (count >= 1) {
    title = (await rows.first().locator('td').first().innerText()).split('\n')[0].trim();
    const search = page.getByPlaceholder('Search content titles...');
    await search.fill(title);
    await page.waitForTimeout(700);
    filtered = await rows.count();
    await search.fill('');
    await page.waitForTimeout(700);
  }

  const legacyFrame = await page.locator('iframe').count();
  const errors = severeConsoleErrors(drainConsoleErrors(ctx.consoleErrors));

  return [
    { name: 'renders-content-items-grid', pass: count >= 1, message: `${count} content items` },
    {
      name: 'search-filters-by-title',
      pass: filtered >= 1 && filtered <= count,
      message: `count=${count} filtered=${filtered} title=${title || 'n/a'}`,
    },
    { name: 'no-legacy-iframe', pass: legacyFrame === 0, message: `iframe count=${legacyFrame}` },
    { name: 'no-console-errors', pass: errors.length === 0, message: errors.join(' | ') || 'clean' },
  ];
};
