const { severeConsoleErrors, drainConsoleErrors } = require('../harness/instance');

// Converted from the old admin-media-options-page.js. Same assertions, minus the
// per-script browser launch/login boilerplate — that now lives once in the harness.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Media/Options`, { waitUntil: 'networkidle' });
  await page.locator('[data-testid="media-options-page"]').waitFor({ timeout: 20000 });

  const hasSupportedSizes = await page.getByText('Supported sizes', { exact: true })
    .waitFor({ timeout: 20000 })
    .then(() => true)
    .catch(() => false);
  const iframeCount = await page.locator('iframe').count();
  const errors = severeConsoleErrors(drainConsoleErrors(ctx.consoleErrors));

  return [
    { name: 'renders-supported-sizes', pass: hasSupportedSizes, message: hasSupportedSizes ? 'ok' : 'text not found' },
    { name: 'no-legacy-iframe', pass: iframeCount === 0, message: `iframe count=${iframeCount}` },
    { name: 'no-console-errors', pass: errors.length === 0, message: errors.join(' | ') || 'clean' },
  ];
};
