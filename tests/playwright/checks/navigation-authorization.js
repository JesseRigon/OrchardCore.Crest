// Converted from the old admin-navigation-authorization.js. This is a security-boundary
// check, so it stays close to the original's literal logic rather than simplifying it:
//   - an anonymous session must get 403 from the navigation API and be redirected to
//     /login when requesting an admin route directly
//   - the authenticated session must get real navigation/manifest data back, with every
//     navigation node carrying required fields (no malformed/empty authorization payload)
//
// One piece is intentionally NOT carried over: the original asserted that a SignalR
// permissions-hub negotiation request (/api/crest/permissions/negotiate) fires as part of
// the live login POST. That assertion is inherently tied to the act of logging in, which
// now happens exactly once in the shared harness before any check runs — re-triggering a
// full login here (or logging the shared page out and back in) would defeat the point of
// the shared instance and could disturb state for every check that runs after this one.
// Flagging this as dropped rather than silently discarding it: if that negotiation-on-login
// behavior still needs coverage, it belongs in a dedicated pre-login/harness-level check,
// not a per-check one that shares an already-authenticated page.
function flatten(items) {
  return items.flatMap(item => [item, ...flatten(item.items || [])]);
}

module.exports = async function run(page, ctx) {
  const results = [];

  // --- Anonymous session: navigation API and route must both refuse access ---
  const browser = page.context().browser();
  const anonContext = await browser.newContext();
  const anonymous = await anonContext.newPage();
  try {
    await anonymous.goto(`${ctx.baseUrl}/login`, { waitUntil: 'domcontentloaded' });

    const anonymousStatus = await anonymous.evaluate(async () =>
      (await fetch('/api/crest/navigation/admin')).status);
    results.push({
      name: 'anonymous-navigation-api-forbidden',
      pass: anonymousStatus === 403,
      message: `status=${anonymousStatus}`,
    });

    const anonymousRoute = await anonymous.goto(`${ctx.baseUrl}/Admin/Features`, { waitUntil: 'domcontentloaded' });
    const redirectedToLogin = anonymousRoute?.status() === 200 && /\/login$/i.test(anonymous.url());
    results.push({
      name: 'anonymous-admin-route-redirects-to-login',
      pass: redirectedToLogin,
      message: `status=${anonymousRoute?.status()} url=${anonymous.url()}`,
    });

    const loginFormShown = await anonymous.locator('input[name="UserName"]').waitFor({ timeout: 20000 })
      .then(() => true).catch(() => false);
    results.push({
      name: 'anonymous-redirect-shows-login-form',
      pass: loginFormShown,
      message: loginFormShown ? 'ok' : 'login form did not appear',
    });
  } finally {
    await anonContext.close();
  }

  // --- Authenticated session (already logged in): navigation/manifest must be real ---
  const menu = await page.evaluate(async () => {
    const response = await fetch('/api/crest/navigation/admin');
    return {
      status: response.status,
      contentType: response.headers.get('content-type'),
      text: await response.text(),
    };
  });
  const menuIsJson = menu.status === 200 && Boolean(menu.contentType?.includes('application/json'));
  results.push({
    name: 'authorized-navigation-is-json',
    pass: menuIsJson,
    message: `status=${menu.status} contentType=${menu.contentType}`,
  });

  let nodes = [];
  let validPayload = false;
  if (menuIsJson) {
    try {
      const payload = JSON.parse(menu.text);
      if (Array.isArray(payload.items)) {
        nodes = flatten(payload.items);
        validPayload = nodes.length > 0 && nodes.every(node => node.text && node.key);
      }
    } catch {
      validPayload = false;
    }
  }
  results.push({
    name: 'authorized-navigation-nodes-valid',
    pass: validPayload,
    message: `nodes=${nodes.length}`,
  });

  const manifest = await page.evaluate(async () => {
    const response = await fetch('/api/crest/app/manifest');
    return { status: response.status, payload: await response.json().catch(() => null) };
  });
  const manifestOk = manifest.status === 200 &&
    Array.isArray(manifest.payload?.authorizedRoutes) &&
    manifest.payload.authorizedRoutes.length > 0;
  results.push({
    name: 'authorized-manifest-has-route-batch',
    pass: manifestOk,
    message: `status=${manifest.status} routes=${manifest.payload?.authorizedRoutes?.length ?? 0}`,
  });

  await page.goto(`${ctx.baseUrl}/Admin/Settings/SecurityHeaders`, { waitUntil: 'domcontentloaded' });
  const securityHeadersRendered = await page.locator('[data-testid="security-headers-page"]').waitFor({ timeout: 20000 })
    .then(() => true).catch(() => false);
  results.push({
    name: 'authorized-session-reaches-protected-page',
    pass: securityHeadersRendered,
    message: securityHeadersRendered ? 'ok' : `did not render at ${page.url()}`,
  });

  return results;
};
