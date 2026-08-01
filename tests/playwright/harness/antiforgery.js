// The Crest.Admin Blazor WASM shell has no server-rendered antiforgery meta tag/form
// input (that's an MVC-view convention this SPA doesn't use) - it fetches its own token
// from GET api/crest/antiforgery/token (CrestAntiforgeryController) and attaches it via
// a dedicated header (see OrchardCore.Crest.Admin/wasm/Api/CrestAntiforgeryHandler.cs).
// Any test code driving [AutoValidateAntiforgeryToken]-protected Crest APIs directly via
// fetch() must do the same - scraping a DOM meta tag (the MVC convention) silently finds
// nothing here and every mutating request gets a generic 400 with no detail.
async function fetchAntiforgeryToken(page, baseUrl) {
  return page.evaluate(async (baseUrl) => {
    const response = await fetch(`${baseUrl}/api/crest/antiforgery/token`, { credentials: 'include' });
    if (!response.ok) {
      throw new Error(`GET api/crest/antiforgery/token failed: ${response.status}`);
    }
    return response.json();
  }, baseUrl);
}

module.exports = { fetchAntiforgeryToken };
