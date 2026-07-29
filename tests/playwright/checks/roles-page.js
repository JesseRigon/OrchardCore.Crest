const { drainConsoleErrors } = require('../harness/instance');

// Converted from the old admin-roles-page.js. The original didn't assert on console
// errors, so no `no-console-errors` result is added here — but the shared buffer is
// still drained so this page's own noise doesn't bleed into later checks in the suite
// that do assert on it.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Roles/Index`, { waitUntil: 'networkidle' });
  await page.locator('[data-testid="roles-page"]').waitFor({ timeout: 20000 });
  await page.getByRole('heading', { name: 'Roles', exact: true, level: 4 }).waitFor({ timeout: 20000 });

  const rows = page.locator('.crest-model-list__item');
  const count = await rows.count();

  const results = [{ name: 'renders-roles-list', pass: count >= 1, message: `${count} roles` }];

  if (count >= 1) {
    const name = await rows.first().locator('.crest-model-list__item-title').innerText();
    await rows.first().click();
    const detailShown = await page.locator('[data-testid="role-detail"]').waitFor({ timeout: 10000 }).then(() => true).catch(() => false);
    const headingShown = await page.getByRole('heading', { name, exact: true, level: 5 }).waitFor({ timeout: 10000 }).then(() => true).catch(() => false);
    results.push({
      name: 'selecting-role-shows-detail',
      pass: detailShown && headingShown,
      message: `role=${name} detail=${detailShown} heading=${headingShown}`,
    });
  }

  const legacyFrame = await page.locator('iframe').count();
  results.push({ name: 'no-legacy-iframe', pass: legacyFrame === 0, message: `iframe count=${legacyFrame}` });

  drainConsoleErrors(ctx.consoleErrors);

  return results;
};
