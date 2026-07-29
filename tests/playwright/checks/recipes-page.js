// Converted from the old admin-recipes-page.js
// (modules/OrchardCore.Crest/tests/playwright/admin-recipes-page.js). Same assertions as
// the original minified script: the Recipes page renders natively (test id + heading) and
// never falls back to the legacy iframe.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Recipes`, { waitUntil: 'networkidle' });

  const rendersOk = await Promise.all([
    page.locator('[data-testid="recipes-page"]').waitFor({ timeout: 20000 }).then(() => true).catch(() => false),
    page.getByRole('heading', { name: 'Recipes' }).waitFor({ timeout: 20000 }).then(() => true).catch(() => false),
  ]).then(results => results.every(Boolean));

  const iframeCount = await page.locator('iframe').count();

  return [
    { name: 'renders-recipes-page', pass: rendersOk, message: rendersOk ? 'ok' : 'test id or heading not found' },
    { name: 'no-legacy-iframe', pass: iframeCount === 0, message: `iframe count=${iframeCount}` },
  ];
};
