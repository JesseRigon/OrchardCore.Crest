// Phase 8: the login shell is a Blazor Web App document — /Login must arrive as
// statically prerendered HTML containing the real credential form BEFORE any
// interactive runtime (circuit or WASM) boots. A raw no-JS HTTP GET is the proof:
// page.request bypasses the browser's script execution entirely, so anything in
// the response body is server-rendered by definition.
// The request MUST be anonymous. The suite's shared page is logged in by the time this
// runs, and an authenticated GET of /Login legitimately returns a 200 with no credential
// form (the app does not offer a login form to someone already signed in). Using the
// shared context here tested the wrong thing entirely — it only ever passed when this
// check happened to run before login. A fresh browser context carries no cookies.
module.exports = async function run(page, ctx) {
  const anonymous = await page.context().browser().newContext();
  let status;
  let html;
  try {
    const response = await anonymous.request.get(`${ctx.baseUrl}/Login`);
    status = response.status();
    html = status === 200 ? await response.text() : '';
  } finally {
    await anonymous.close();
  }

  const hasUserName = html.includes('id="UserName"');
  const hasPassword = html.includes('id="Password"');
  const hasWebJs = html.includes('_framework/blazor.web.js');
  const hasLegacyWasmJs = html.includes('blazor.webassembly.js');
  const baseMatch = html.match(/<base href="([^"]*)"/);

  return [
    { name: 'login-returns-200', pass: status === 200, message: `status=${status}` },
    {
      name: 'login-form-is-prerendered',
      pass: hasUserName && hasPassword,
      message: `UserName=${hasUserName} Password=${hasPassword}`,
    },
    {
      name: 'uses-blazor-web-not-legacy-wasm-bootstrap',
      pass: hasWebJs && !hasLegacyWasmJs,
      message: `blazor.web.js=${hasWebJs} blazor.webassembly.js=${hasLegacyWasmJs}`,
    },
    {
      name: 'base-href-is-login-shell',
      pass: Boolean(baseMatch) && /\/login\/$/i.test(baseMatch[1]),
      message: `base=${baseMatch ? baseMatch[1] : 'missing'}`,
    },
  ];
};
