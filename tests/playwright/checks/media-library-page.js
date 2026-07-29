const { severeConsoleErrors, drainConsoleErrors } = require('../harness/instance');

// Converted from the old admin-media-library-page.js. Same assertions, minus the
// per-script browser launch/login boilerplate — that now lives once in the harness.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Media`, { waitUntil: 'networkidle' });
  await page.locator('[data-testid="media-library-page"]').waitFor({ timeout: 20000 });
  await page.getByRole('heading', { name: 'Media Library', exact: true, level: 4 }).waitFor({ timeout: 20000 });
  await page.locator('[data-testid="media-breadcrumbs"]').waitFor({ timeout: 20000 });

  const iframeCount = await page.locator('iframe').count();
  const empty = await page.getByText('This folder is empty. Upload a file or create a folder to get started.').count();
  const grid = await page.locator('[data-testid="media-library-grid"]').count();
  const errors = severeConsoleErrors(drainConsoleErrors(ctx.consoleErrors));

  return [
    { name: 'no-legacy-iframe', pass: iframeCount === 0, message: `iframe count=${iframeCount}` },
    {
      name: 'renders-entries-or-empty-state',
      pass: Boolean(empty) || Boolean(grid),
      message: `empty=${Boolean(empty)} grid=${Boolean(grid)}`,
    },
    { name: 'no-console-errors', pass: errors.length === 0, message: errors.join(' | ') || 'clean' },
  ];
};
