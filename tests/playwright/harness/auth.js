// /login is a Blazor WASM component (Crest.Admin), not a plain server-rendered MVC
// form - its submit button starts disabled and only enables once Blazor's own
// validation re-render catches up with the filled values. Clicking immediately after
// page.fill() races that re-render and can hit the button while it's still disabled
// (a silent no-op click), leaving the page on /login. Every login helper below waits
// for the button to actually become enabled first.
async function clickLoginButton(page) {
  const button = page.locator('button:has-text("Login")');
  await button.waitFor({ state: 'visible', timeout: 10000 });
  await page.waitForFunction(
    (el) => !el.disabled,
    await button.elementHandle(),
    { timeout: 10000 },
  ).catch(() => {});
  await button.click();
}

// Log into /Admin once per suite run. Every existing script hand-rolled this same
// function — it now lives in exactly one place.
async function loginAsAdmin(page, baseUrl, creds = {}) {
  const username = creds.username || process.env.ADMIN_USER || 'admin';
  const password = creds.password || process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

  await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
  if (await page.locator('#UserName').count()) {
    await page.fill('#UserName', username);
    await page.fill('#Password', password);
    await clickLoginButton(page);
    await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle').catch(() => {});
  }
  await page.goto(`${baseUrl}/Admin`, { waitUntil: 'networkidle' });
}

// Placeholder for the public/front-end site's auth flow. No client-site feature checks
// exist yet — this exists so run-client-suite.js has a real login step to call once the
// first front-end check is written, instead of inventing the shape then. Same Orchard
// /login mechanism as admin, just no forced redirect into /Admin afterward.
async function loginAsClient(page, baseUrl, creds = {}) {
  const username = creds.username || process.env.CLIENT_USER;
  const password = creds.password || process.env.CLIENT_PASSWORD;

  await page.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });

  if (username && password && (await page.locator('#UserName').count())) {
    await page.fill('#UserName', username);
    await page.fill('#Password', password);
    await clickLoginButton(page);
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

// Named-identity login for the multi-user localization checks (see
// plans/user-localization-testing.md): logs a specific, already-provisioned non-admin
// user into /Admin (they may have limited/no admin permissions - the culture resolution
// checks only need an authenticated session, not admin access to every page). Distinct
// from loginAsAdmin (fixed admin/ADMIN_* credentials) and loginAsClient (front-end-only,
// no forced /Admin redirect) since the localization checks need an authenticated
// arbitrary identity that can still reach DisplayManager's manifest/resolution flow.
async function loginAsUser(page, baseUrl, { username, password }) {
  await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
  if (await page.locator('#UserName').count()) {
    await page.fill('#UserName', username);
    await page.fill('#Password', password);
    await clickLoginButton(page);
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

// Logs the current session out via Crest's own auth API (not a UI click), so callers
// don't need to know which page/menu currently hosts the logout action.
// CrestAuthController is [AutoValidateAntiforgeryToken] - see harness/antiforgery.js for
// why a DOM meta-tag scrape (the MVC convention) doesn't work against this WASM shell.
async function logout(page, baseUrl) {
  const { fetchAntiforgeryToken } = require('./antiforgery');
  const antiforgery = await fetchAntiforgeryToken(page, baseUrl);
  await page.evaluate(async ({ baseUrl, antiforgery }) => {
    await fetch(`${baseUrl}/api/crest/auth/logout`, {
      method: 'POST',
      credentials: 'include',
      headers: { [antiforgery.headerName]: antiforgery.requestToken },
    }).catch(() => {});
  }, { baseUrl, antiforgery });
}

module.exports = { loginAsAdmin, loginAsClient, loginAsUser, logout };
