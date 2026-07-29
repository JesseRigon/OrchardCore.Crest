// Converted from the old admin-login-settings-page.js
// (modules/OrchardCore.Crest/tests/playwright/admin-login-settings-page.js). Same
// assertions: the User Login Settings page renders natively (test id + heading), shows the
// remember-me / 2FA / external-login sections, and never falls back to the legacy iframe.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Settings/userLogin`, { waitUntil: 'networkidle' });

  const rendersOk = await Promise.all([
    page.locator('[data-testid="login-settings-page"]').waitFor({ timeout: 20000 }).then(() => true).catch(() => false),
    page.getByRole('heading', { name: 'User Login Settings' }).waitFor({ timeout: 20000 }).then(() => true).catch(() => false),
  ]).then(results => results.every(Boolean));

  const rememberMeVisible = await page.getByText('Allow user to be remembered during login').waitFor({ timeout: 10000 }).then(() => true).catch(() => false);
  const twoFactorVisible = await page.getByText('Two-Factor Authentication', { exact: true }).waitFor({ timeout: 10000 }).then(() => true).catch(() => false);
  const externalLoginVisible = await page.getByText('External Login', { exact: true }).waitFor({ timeout: 10000 }).then(() => true).catch(() => false);
  const iframeCount = await page.locator('iframe').count();

  return [
    { name: 'renders-login-settings-page', pass: rendersOk, message: rendersOk ? 'ok' : 'test id or heading not found' },
    { name: 'shows-remember-me-option', pass: rememberMeVisible, message: rememberMeVisible ? 'ok' : 'remember-me text not found' },
    { name: 'shows-2fa-section', pass: twoFactorVisible, message: twoFactorVisible ? 'ok' : 'Two-Factor Authentication text not found' },
    { name: 'shows-external-login-section', pass: externalLoginVisible, message: externalLoginVisible ? 'ok' : 'External Login text not found' },
    { name: 'no-legacy-iframe', pass: iframeCount === 0, message: `iframe count=${iframeCount}` },
  ];
};
