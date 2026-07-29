const { severeConsoleErrors, drainConsoleErrors } = require('../harness/instance');

// Converted from the old admin-content-types-page.js. Same assertions; also clears the
// search field afterward (the original didn't bother, since it launched a fresh browser
// per script) so it doesn't leave filtered state behind for the shared page.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/ContentTypes/List`, { waitUntil: 'networkidle' });
  await page.locator('[data-testid="content-types-page"]').waitFor({ timeout: 20000 });
  await page.getByRole('heading', { name: 'Content Types', exact: true, level: 4 }).waitFor({ timeout: 20000 });

  const rows = page.locator('.crest-model-list__item');
  const count = await rows.count();

  const results = [{ name: 'renders-content-types-list', pass: count >= 1, message: `${count} content types` }];

  if (count >= 1) {
    const selectedName = await rows.first().locator('.crest-model-list__item-title').innerText();
    await rows.first().click();
    const detailShown = await page.locator('[data-testid="content-type-detail"]').waitFor({ timeout: 10000 }).then(() => true).catch(() => false);
    const headingShown = await page.getByRole('heading', { name: selectedName, exact: true, level: 5 }).waitFor({ timeout: 10000 }).then(() => true).catch(() => false);
    results.push({
      name: 'selecting-type-shows-detail',
      pass: detailShown && headingShown,
      message: `type=${selectedName} detail=${detailShown} heading=${headingShown}`,
    });

    const search = page.getByPlaceholder('Search...');
    await search.fill(selectedName);
    await search.press('Tab');
    await page.waitForTimeout(200);
    const filteredCount = await rows.count();
    results.push({
      name: 'search-filters-list',
      pass: filteredCount >= 1 && filteredCount < count,
      message: `count=${count} filtered=${filteredCount} selected=${selectedName}`,
    });
    await search.fill('');
    await search.press('Tab');
    await page.waitForTimeout(200);
  }

  const legacyFrame = await page.locator('iframe').count();
  const errors = severeConsoleErrors(drainConsoleErrors(ctx.consoleErrors));

  results.push({ name: 'no-legacy-iframe', pass: legacyFrame === 0, message: `iframe count=${legacyFrame}` });
  results.push({ name: 'no-console-errors', pass: errors.length === 0, message: errors.join(' | ') || 'clean' });

  return results;
};
