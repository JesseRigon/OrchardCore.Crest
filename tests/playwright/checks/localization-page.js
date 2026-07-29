const { severeConsoleErrors, drainConsoleErrors } = require('../harness/instance');

// Converted from the old admin-localization-page.js. Same assertions, minus the
// per-script browser launch/login boilerplate — that now lives once in the harness.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Settings/localization`, { waitUntil: 'networkidle' });
  await page.locator('[data-testid="localization-page"]').waitFor({ timeout: 20000 });

  const iframeCount = await page.locator('iframe').count();

  const cultureCards = page.locator('.localization-page__culture-card');
  const cultureCardCount = await cultureCards.count();

  const dropdowns = page.locator('.localization-page__culture-dropdown');
  const dropdownCount = await dropdowns.count();

  let defaultOptionCount = 0;
  if (dropdownCount >= 2) {
    const defaultDropdown = dropdowns.nth(1);
    await defaultDropdown.locator('.crest-dropdown__trigger').click();
    const options = defaultDropdown.locator('[role="option"]');
    defaultOptionCount = await options.count();
    await page.keyboard.press('Escape');
  }

  const errors = severeConsoleErrors(drainConsoleErrors(ctx.consoleErrors));

  return [
    { name: 'no-legacy-iframe', pass: iframeCount === 0, message: `iframe count=${iframeCount}` },
    { name: 'renders-culture-cards', pass: cultureCardCount >= 1, message: `${cultureCardCount} culture cards` },
    { name: 'renders-culture-dropdowns', pass: dropdownCount >= 2, message: `${dropdownCount} dropdowns` },
    {
      name: 'default-culture-dropdown-has-options',
      pass: dropdownCount >= 2 && defaultOptionCount >= 1,
      message: `${defaultOptionCount} options`,
    },
    { name: 'no-console-errors', pass: errors.length === 0, message: errors.join(' | ') || 'clean' },
  ];
};
