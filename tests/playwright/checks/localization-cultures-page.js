// Converted from the old admin-localization-page.js at
// modules/OrchardCore.Crest/tests/playwright/admin-localization-page.js — the PLAIN Crest
// tests dir, NOT modules/OrchardCore.Crest/OrchardCore.Crest.Admin/tests/playwright/
// admin-localization-page.js (a different, more thorough script that asserts culture cards,
// Crest dropdown option counts, etc. — that one is presumed to be converted separately,
// possibly under a name like "localization-page.js"). This file is named
// "localization-cultures-page.js" specifically so it can't collide with that conversion.
//
// This original was a minimal smoke check: the Localization settings page renders natively
// (test id + "Cultures" heading) and never falls back to the legacy iframe.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Settings/localization`, { waitUntil: 'networkidle' });

  const rendersOk = await Promise.all([
    page.locator('[data-testid="localization-page"]').waitFor({ timeout: 20000 }).then(() => true).catch(() => false),
    // exact: true — without it 'Cultures' also matches the "Supported cultures"
    // subheading and the strict-mode violation fails the wait.
    page.getByRole('heading', { name: 'Cultures', exact: true }).waitFor({ timeout: 20000 }).then(() => true).catch(() => false),
  ]).then(results => results.every(Boolean));

  const iframeCount = await page.locator('iframe').count();

  return [
    { name: 'renders-localization-page', pass: rendersOk, message: rendersOk ? 'ok' : 'test id or heading not found' },
    { name: 'no-legacy-iframe', pass: iframeCount === 0, message: `iframe count=${iframeCount}` },
  ];
};
