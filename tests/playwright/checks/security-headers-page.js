// Converted from the old admin-security-headers-page.js. Verifies the Security Headers
// settings page renders natively, shows its three policy sections, and that Save performs
// a real write (PUT to /api/crest/security-headers) carrying Orchard's antiforgery token.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Settings/SecurityHeaders`, { waitUntil: 'domcontentloaded' });

  const rendered = await page.locator('[data-testid="security-headers-page"]').waitFor({ timeout: 20000 })
    .then(() => true).catch(() => false);
  if (!rendered) {
    return { name: 'renders-security-headers-page', pass: false, message: `page did not render at ${page.url()}` };
  }

  const sectionLabels = ['Content Security Policy', 'Permissions Policy', 'Referrer Policy'];
  const missing = [];
  for (const label of sectionLabels) {
    const visible = await page.getByText(label, { exact: true }).first().waitFor({ timeout: 10000 })
      .then(() => true).catch(() => false);
    if (!visible) missing.push(label);
  }

  const responsePromise = page.waitForResponse(response =>
    response.url().includes('/api/crest/security-headers') && response.request().method() === 'PUT',
  { timeout: 20000 }).catch(() => null);
  await page.getByRole('button', { name: 'Save' }).click();
  const response = await responsePromise;

  const saveOk = Boolean(response?.ok());
  const hasAntiforgeryToken = Boolean(response?.request().headers().requestverificationtoken);

  return [
    { name: 'renders-security-headers-page', pass: rendered, message: rendered ? 'ok' : 'missing' },
    { name: 'shows-policy-sections', pass: missing.length === 0, message: missing.length ? `missing: ${missing.join(', ')}` : 'ok' },
    { name: 'save-persists-via-api', pass: saveOk, message: `status=${response?.status() ?? 'no response'}` },
    { name: 'save-carries-antiforgery-token', pass: hasAntiforgeryToken, message: hasAntiforgeryToken ? 'ok' : 'requestverificationtoken header missing' },
  ];
};
