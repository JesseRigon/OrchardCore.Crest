// Cheap, fast checks that the site is actually up — run once per suite, before login.
// These must only touch unauthenticated surface area: the admin shell/manifest API
// aren't reachable yet at this point in the flow, so checking them here would always
// fail regardless of whether the site is healthy.
async function runHealthChecks(page, baseUrl) {
  const results = [];

  const rootResponse = await page.request.get(baseUrl).catch(() => null);
  results.push({
    name: 'site-reachable',
    pass: !!rootResponse && rootResponse.status() < 500,
    message: rootResponse ? `HTTP ${rootResponse.status()}` : 'request failed',
  });

  const loginResponse = await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' }).catch(() => null);
  const loginFormVisible = await page.locator('#UserName').count().catch(() => 0);
  results.push({
    name: 'login-page-renders',
    pass: !!loginResponse && loginResponse.status() < 400 && loginFormVisible > 0,
    message: loginResponse ? `HTTP ${loginResponse.status()}, username field present: ${loginFormVisible > 0}` : 'no response',
  });

  return results;
}

module.exports = { runHealthChecks };
