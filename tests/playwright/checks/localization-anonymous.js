const { createInstance } = require('../harness/instance');

// Verifies anonymous-visitor culture resolution on the front-end site
// (OrchardCore.Crest.Site) - server-rendered Liquid/cshtml, no Blazor WASM client, so
// this exercises the STOCK ASP.NET Core RequestLocalizationOptions pipeline (the
// Accept-Language header provider + the tenant's LocalizationSettings.DefaultCulture
// fallback), not DisplayManager.ResolveCultureAsync's client-side chain - the
// WASM-resolved chain only exists inside .Admin, which an anonymous front-end visitor
// never loads. This is intentionally the right mechanism to test here: it's what
// actually serves anonymous front-end traffic, and plans/user-localization.md's
// "Architectural constraint" section requires stock OrchardCore localization to keep
// working correctly on its own for exactly this kind of request.
//   - a browser locale the tenant DOES support (es-ES) should resolve to es-ES.
//   - a browser locale the tenant does NOT support (ja-JP) should fall back to the
//     tenant default (en-US).
async function resolvedCultureFor(baseUrl, locale) {
  const { browser, context, page } = await createInstance({
    contextOptions: { locale, extraHTTPHeaders: { 'Accept-Language': locale } },
  });
  try {
    await page.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });
    const htmlLang = await page.evaluate(() => document.documentElement.lang || null);
    return htmlLang;
  } finally {
    await browser.close();
  }
}

module.exports = async function run(page, ctx) {
  const results = [];

  const supportedLocaleResult = await resolvedCultureFor(ctx.baseUrl, 'es-ES');
  results.push({
    name: 'anonymous-enabled-culture-resolves-to-browser-locale',
    pass: supportedLocaleResult === 'es-ES',
    message: `lang=${supportedLocaleResult} (expected es-ES)`,
  });

  const unsupportedLocaleResult = await resolvedCultureFor(ctx.baseUrl, 'ja-JP');
  results.push({
    name: 'anonymous-unsupported-culture-falls-back-to-tenant-default',
    pass: unsupportedLocaleResult === 'en-US',
    message: `lang=${unsupportedLocaleResult} (expected en-US, the tenant default)`,
  });

  return results;
};
