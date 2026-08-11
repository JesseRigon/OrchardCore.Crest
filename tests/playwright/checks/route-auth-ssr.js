// Phase 8: BlazorAdminThemeMiddleware still gates every admin page request ahead of
// the Blazor Web App document render (auth check + CrestRouteAuthorizationService).
// This check proves that gating holds for direct server-side GETs — the SSR path —
// not just for client-side navigation:
//   1. an ANONYMOUS request for a protected admin URL never receives admin markup
//      (it's redirected to the login shell), and
//   2. the AUTHENTICATED session (the suite's shared page context) receives a real
//      200 admin document for the same URL.
module.exports = async function run(page, ctx) {
  const protectedUrl = `${ctx.baseUrl}/Admin/Features`;
  const results = [];

  // Fresh context = no cookies = anonymous. maxRedirects 0 exposes the redirect itself.
  const anonymous = await page.context().browser().newContext();
  try {
    const anonResponse = await anonymous.request.get(protectedUrl, { maxRedirects: 0 });
    const anonStatus = anonResponse.status();
    const location = anonResponse.headers()['location'] || '';
    const redirectsToLogin = anonStatus >= 300 && anonStatus < 400 && /login/i.test(location);

    // Some pipelines render the login shell directly instead of 302ing; accept that
    // too, as long as no admin content leaks to the anonymous request.
    let servesLoginDocument = false;
    if (anonStatus === 200) {
      const body = await anonResponse.text();
      servesLoginDocument = body.includes('id="UserName"') && !body.includes('admin-shell');
    }

    results.push({
      name: 'anonymous-admin-request-is-gated',
      pass: redirectsToLogin || servesLoginDocument,
      message: `status=${anonStatus} location=${location || 'none'}`,
    });
  } finally {
    await anonymous.close();
  }

  const authResponse = await page.request.get(protectedUrl);
  const authStatus = authResponse.status();
  const authBody = authStatus === 200 ? await authResponse.text() : '';
  results.push({
    name: 'authenticated-admin-request-renders-document',
    pass: authStatus === 200 && authBody.includes('_framework/blazor.web.js'),
    message: `status=${authStatus}`,
  });

  return results;
};
