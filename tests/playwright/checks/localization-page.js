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
    // The dropdown root IS the clickable combobox since the Radzen source merge
    // (ad8db47) — the old .crest-dropdown__trigger inner button no longer exists.
    // Radzen renders the dropdown's <li role="option"> list into the DOM up front and
    // only toggles panel visibility on click, so the options can be counted without
    // opening anything. That matters here: clicking TOGGLES the panel, so a
    // click-then-retry loop closes it again on every second attempt, and the options
    // are display:none while closed — which is why waiting for one to become *visible*
    // timed out. Count them directly instead.
    //
    // nth(1) is the tenant's default-culture selector (4 configured cultures). nth(0) is
    // the "add culture" picker with the full ~850-culture list — do not confuse them.
    const defaultDropdown = dropdowns.nth(1);
    const options = defaultDropdown.locator('[role="option"]');
    await options.first().waitFor({ state: 'attached', timeout: 10000 }).catch(() => {});
    defaultOptionCount = await options.count();
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
