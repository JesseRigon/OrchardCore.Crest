const { severeConsoleErrors, drainConsoleErrors } = require('../harness/instance');

async function checkPage(page, ctx, route, expected) {
  await page.goto(`${ctx.baseUrl}${route}`, { waitUntil: 'networkidle' });

  const ok = await Promise.all([
    page.locator('.admin-shell').waitFor({ timeout: 20000 }).then(() => true).catch(() => false),
    page.locator('.primary-nav-menu').waitFor({ timeout: 20000 }).then(() => true).catch(() => false),
    page.getByRole('heading', { name: expected.heading, exact: true, level: 4 }).waitFor({ timeout: 20000 }).then(() => true).catch(() => false),
  ]);

  if (ok.some(v => !v)) {
    return { name: `renders:${route}`, pass: false, message: `shell=${ok[0]} nav=${ok[1]} heading=${ok[2]}` };
  }

  for (const text of expected.texts) {
    const visible = await page.getByText(text, { exact: false }).first().waitFor({ timeout: 10000 }).then(() => true).catch(() => false);
    if (!visible) {
      return { name: `renders:${route}`, pass: false, message: `missing expected text: "${text}"` };
    }
  }

  return { name: `renders:${route}`, pass: true, message: 'ok' };
}

// Converted from the old admin-standard-pages.js. Trimmed to the page-level assertions;
// the original's Site Map / Resources / Cache tab-click sub-checks are still TODO —
// listed as separate work, not silently dropped.
module.exports = async function run(page, ctx) {
  const results = [];

  results.push(await checkPage(page, ctx, '/Admin/Menus', {
    heading: 'Menus',
    texts: ['Manage standard Orchard site menus', 'Refresh'],
  }));

  results.push(await checkPage(page, ctx, '/Admin/Settings/admin', {
    heading: 'Admin Settings',
    texts: ['Site Map', 'Enable theme toggler'],
  }));

  results.push(await checkPage(page, ctx, '/Admin/Settings/general', {
    heading: 'General Settings',
    texts: ['General', 'Resources', 'Cache', 'Site name'],
  }));

  const errors = severeConsoleErrors(drainConsoleErrors(ctx.consoleErrors));
  results.push({ name: 'no-console-errors', pass: errors.length === 0, message: errors.join(' | ') || 'clean' });

  return results;
};
