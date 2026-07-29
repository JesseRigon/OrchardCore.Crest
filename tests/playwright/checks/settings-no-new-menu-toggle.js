// Converted from the old admin-settings-no-new-menu-toggle.js. Regression check: the
// "Display New menu" toggle was removed from Admin Settings and must not reappear.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Settings/admin`, { waitUntil: 'domcontentloaded' });
  await page.getByRole('heading', { name: 'Admin Settings', exact: true }).waitFor({ timeout: 20000 });

  const displayNewMenuControls = await page.getByText('Display New menu').count();

  return {
    name: 'no-display-new-menu-toggle',
    pass: displayNewMenuControls === 0,
    message: `count=${displayNewMenuControls}`,
  };
};
