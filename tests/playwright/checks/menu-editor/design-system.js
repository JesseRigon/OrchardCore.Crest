// Converted from OrchardCore.Crest.Admin/tests/playwright/admin-menu-design-system.js.
// Validates the Blazor-managed Admin Menu editor consumes Crest design tokens (not
// hardcoded colors) and renders as a native Blazor page (no iframes).
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
  await page.locator('.admin-shell').waitFor({ timeout: 20000 });
  await page.getByRole('heading', { name: 'Admin Menus', exact: true }).waitFor({ timeout: 20000 });
  await page.locator('.admin-menu-tree').waitFor({ timeout: 20000 });

  const iframeCount = await page.locator('iframe').count();

  const loadedStyles = await page.evaluate(() =>
    Array.from(document.querySelectorAll('link[rel="stylesheet"]')).map(link => link.getAttribute('href') || ''),
  );
  // Phase 8: the admin shell is a Razor class library now, so its scoped CSS ships as
  // the RCL bundle _content/OrchardCore.Crest.Admin.Client/*.bundle.scp.css instead of
  // the old exe-style OrchardCore.Crest.Admin.styles.css app bundle.
  const requiredStyles = ['/CrestAdmin.DesignSystem.Default.css', '/CrestAdmin.css', '/OrchardCore.Crest.Admin.Client.bundle.scp.css'];
  const missingStyles = requiredStyles.filter(required => !loadedStyles.some(href => href.endsWith(required)));

  const initialSelected = page.locator('.admin-menu-list-item--selected').first();
  await initialSelected.waitFor({ timeout: 10000 });
  const initialDisplay = await initialSelected.evaluate(element => getComputedStyle(element).display);

  await page.evaluate(() => {
    const root = document.querySelector('.admin-shell') || document.documentElement;
    root.style.setProperty('--crest-color-accent-1', 'rgb(190, 20, 120)');
    root.style.setProperty('--crest-color-active-surface-1', 'rgba(190, 20, 120, 0.25)');
    root.style.setProperty('--crest-color-surface-1', 'rgb(250, 252, 240)');
    root.style.setProperty('--crest-color-border-1', 'rgb(18, 52, 86)');
    root.style.setProperty('--crest-radius-sm', '18px');
  });

  const selectedMetrics = await initialSelected.evaluate(element => {
    const style = getComputedStyle(element);
    return {
      borderTopColor: style.borderTopColor,
      backgroundColor: style.backgroundColor,
      borderTopLeftRadius: style.borderTopLeftRadius,
    };
  });

  const settingsButton = page.locator('button[title="PrimaryNavMenu settings"]').first();
  let settingsMetrics = null;
  if (await settingsButton.count()) {
    await settingsButton.click();
    const settingsFlyout = page.locator('.admin-menu-settings').first();
    await settingsFlyout.waitFor({ timeout: 10000 });
    settingsMetrics = await settingsFlyout.evaluate(element => {
      const style = getComputedStyle(element);
      return {
        backgroundColor: style.backgroundColor,
        borderTopColor: style.borderTopColor,
        borderTopLeftRadius: style.borderTopLeftRadius,
      };
    });
  }

  return [
    { name: 'no-iframes', pass: iframeCount === 0, message: `iframeCount=${iframeCount}` },
    { name: 'required-stylesheets-loaded', pass: missingStyles.length === 0, message: missingStyles.join(', ') || 'all present' },
    { name: 'scoped-css-applied', pass: initialDisplay === 'flex', message: `display=${initialDisplay}` },
    {
      name: 'selected-node-consumes-tokens',
      pass:
        selectedMetrics.borderTopColor === 'rgb(190, 20, 120)' &&
        selectedMetrics.backgroundColor === 'rgba(190, 20, 120, 0.25)' &&
        selectedMetrics.borderTopLeftRadius === '18px',
      message: JSON.stringify(selectedMetrics),
    },
    {
      name: 'settings-flyout-consumes-tokens',
      pass:
        settingsMetrics === null ||
        (settingsMetrics.backgroundColor === 'rgb(250, 252, 240)' &&
          settingsMetrics.borderTopColor === 'rgb(18, 52, 86)' &&
          settingsMetrics.borderTopLeftRadius === '18px'),
      message: settingsMetrics ? JSON.stringify(settingsMetrics) : 'no settings button present',
    },
  ];
};
