// Converted from the old admin-users-page.js. Verifies the Users admin page renders
// natively (heading + Add User button) with no legacy iframe fallback.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Users/Index`, { waitUntil: 'networkidle' });

  const rendered = await page.locator('[data-testid="users-page"]').waitFor({ timeout: 20000 })
    .then(() => true).catch(() => false);
  if (!rendered) {
    return { name: 'renders-users-page', pass: false, message: `page did not render at ${page.url()}` };
  }

  const hasHeading = await page.getByRole('heading', { name: 'Users' }).waitFor({ timeout: 10000 })
    .then(() => true).catch(() => false);
  const hasAddUserButton = await page.getByRole('button', { name: 'Add User' }).waitFor({ timeout: 10000 })
    .then(() => true).catch(() => false);
  const iframeCount = await page.locator('iframe').count();

  return [
    { name: 'renders-users-page', pass: rendered && hasHeading, message: `heading=${hasHeading}` },
    { name: 'has-add-user-button', pass: hasAddUserButton, message: hasAddUserButton ? 'ok' : 'missing' },
    { name: 'no-legacy-iframe', pass: iframeCount === 0, message: `iframe count=${iframeCount}` },
  ];
};
