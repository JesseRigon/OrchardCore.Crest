const { createInstance } = require('../harness/instance');

// Verifies translated content actually renders on the front-end site
// (OrchardCore.Crest.Site) for a non-English resolved culture - the "simple component
// just to test the translations actually work" from plans/user-localization-testing.md.
// The smoke block lives in Content-Page.liquid (data-testid="localization-smoke"),
// rendering {{ "Welcome" | t }} - a hand-written test string backed by
// OrchardCore.Crest.Site/Localization/{es,fr,de}.po (mirrored under
// tests/playwright/fixtures/localization-smoke/ - see that directory's README).
//
// Requires at least one published Page content item to exist at ctx.baseUrl so
// Content-Page.liquid actually renders - if none exists yet on this tenant, the check
// reports 'no-page-content-item' rather than a false pass/fail.
async function welcomeTextFor(baseUrl, locale) {
  const { browser, page } = await createInstance({
    contextOptions: { locale, extraHTTPHeaders: { 'Accept-Language': locale } },
  });
  try {
    await page.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    // Best-effort: try the homepage first (many Page-type homepages render
    // Content-Page.liquid directly); if the smoke hook isn't there, this check reports
    // 'not found' rather than guessing at content navigation.
    const hook = page.locator('[data-testid="localization-smoke"]');
    const found = await hook.waitFor({ timeout: 5000 }).then(() => true).catch(() => false);
    if (!found) return { found: false, text: null };
    return { found: true, text: await hook.textContent() };
  } finally {
    await browser.close();
  }
}

module.exports = async function run(page, ctx) {
  const es = await welcomeTextFor(ctx.baseUrl, 'es-ES');
  if (!es.found) {
    return [{
      name: 'localization-smoke-hook-present',
      pass: false,
      status: 'no-base',
      message: 'data-testid="localization-smoke" not found on homepage - requires a published Page content item using Content-Page.liquid',
    }];
  }

  return [
    { name: 'localization-smoke-hook-present', pass: true, message: 'ok' },
    { name: 'spanish-translation-renders', pass: es.text === 'Bienvenido', message: `text=${es.text}` },
  ];
};
