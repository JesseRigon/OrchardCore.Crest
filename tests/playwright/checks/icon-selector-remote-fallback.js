const { severeConsoleErrors, drainConsoleErrors } = require('../harness/instance');

// Converted from OrchardCore.Crest.Icons/tests/playwright/admin-icon-selector-remote-fallback.js.
// Verifies the admin menu node icon picker renders icon previews via the remote Iconify
// fallback (i.e. without relying on a local App_Data mirror) and that searching narrows
// the grid down to matching results.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
  await page.getByRole('heading', { name: 'Admin Menus', exact: true }).waitFor({ timeout: 20000 });
  await page.getByRole('button', { name: /add node/i }).click();
  await page.locator('.admin-menu-node-editor').getByTitle('Choose icon').click();

  const dialog = page.locator('.icon-selector__dialog');
  await dialog.waitFor({ timeout: 15000 });
  await dialog.locator('.icon-selector__item svg').first().waitFor({ timeout: 30000 });
  const initialCount = await dialog.locator('.icon-selector__item').count();

  const search = dialog.getByPlaceholder('Search all icons...');
  await search.fill('home');
  await page
    .waitForFunction(
      () => Array.from(document.querySelectorAll('.icon-selector__item')).some(node => /home/i.test(node.getAttribute('title') || '')),
      null,
      { timeout: 30000 },
    )
    .catch(() => {});
  const result = await dialog.locator('.icon-selector__item').first().evaluate(node => ({
    title: node.getAttribute('title'),
    svg: Boolean(node.querySelector('svg')),
  }));

  const errors = severeConsoleErrors(drainConsoleErrors(ctx.consoleErrors));

  return [
    { name: 'renders-icons-via-remote-fallback', pass: initialCount > 0, message: `initialCount=${initialCount}` },
    {
      name: 'search-returns-home-icon',
      pass: /home/i.test(result.title || '') && result.svg,
      message: `title=${result.title ?? 'none'} svg=${result.svg}`,
    },
    { name: 'no-console-errors', pass: errors.length === 0, message: errors.join(' | ') || 'clean' },
  ];
};
