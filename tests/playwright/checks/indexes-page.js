// Converted from the old admin-indexes-page.js
// (modules/OrchardCore.Crest/tests/playwright/admin-indexes-page.js).
// Same assertions as the original minified script: the Indexes page renders natively
// (test id + heading) and never falls back to the legacy iframe.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/indexing`, { waitUntil: 'networkidle' });

  const rendersOk = await Promise.all([
    page.locator('[data-testid="indexes-page"]').waitFor({ timeout: 20000 }).then(() => true).catch(() => false),
    page.getByRole('heading', { name: 'Indexes' }).waitFor({ timeout: 20000 }).then(() => true).catch(() => false),
  ]).then(results => results.every(Boolean));

  const iframeCount = await page.locator('iframe').count();

  return [
    { name: 'renders-indexes-page', pass: rendersOk, message: rendersOk ? 'ok' : 'test id or heading not found' },
    { name: 'no-legacy-iframe', pass: iframeCount === 0, message: `iframe count=${iframeCount}` },
  ];
};
