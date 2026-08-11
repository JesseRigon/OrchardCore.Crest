const { createInstance } = require('../harness/instance');

// TODO (Phase 5): this check is anchored to a template that is scheduled for deletion.
// The smoke hook lives in Content-Page.liquid, but the conversion's goal is Blazor all
// the way down - Phase 5 replaces every Views/*.liquid with a .razor component and Phase 7
// deletes the Views/ directory. The site root "/" is already served by Blazor (with an
// empty body until Site gets its own @page "/" component), which is why this check is
// currently red. Rewrite it against the Blazor homepage - assert a translated string that
// reached the page through the Blazor/IStringLocalizer path - rather than making it pass
// by keeping Liquid alive. See the "Site root" section of plans/blazor hybrid conversion.md.
//
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
    const found = await hook.waitFor({ state: 'attached', timeout: 5000 }).then(() => true).catch(() => false);
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
