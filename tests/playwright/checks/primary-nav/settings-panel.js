// Converted from OrchardCore.Crest/tests/playwright/admin-menu-primary-nav-menu-settings.js.
// The primaryNavMenu settings flyout: tier-4 fields render correctly (no indentation
// input, but background/base-size inputs still present), an unsaved checkbox edit
// doesn't get reverted by a background reload, and saved settings actually apply to the
// live menu (collapse toggle removed, expansion duration + tier size applied). Restores
// the original settings in `finally` either way.
module.exports = async function run(page, ctx) {
  async function savePrimaryNavMenuSettings(menuId, settings, action) {
    await page.evaluate(
      async ({ menuId, settings, action }) => {
        const tokenResponse = await fetch('/api/crest/antiforgery/token', { credentials: 'include' });
        if (!tokenResponse.ok) throw new Error(`Unable to load antiforgery token before ${action}: ${tokenResponse.status}`);
        const token = await tokenResponse.json();
        const response = await fetch(`/api/crest/admin-menus/${encodeURIComponent(menuId)}/primary-nav-menu-settings`, {
          method: 'POST',
          credentials: 'include',
          headers: { 'content-type': 'application/json', [token.headerName || 'RequestVerificationToken']: token.requestToken },
          body: JSON.stringify(settings),
        });
        if (!response.ok) throw new Error(`Unable to ${action}: ${response.status} ${await response.text()}`);
      },
      { menuId, settings, action },
    );
  }

  let originalSettings = null;
  const results = [];

  try {
    await page.goto(`${ctx.baseUrl}/Admin/AdminMenus`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: 'Sidebar', exact: true }).waitFor({ timeout: 20000 });

    const state = await page.evaluate(async () => {
      const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
      if (!response.ok) throw new Error(`Unable to load admin menu state: ${response.status}`);
      return response.json();
    });
    const menu = state.menus?.find(menu => menu.isDefault) || state.menus?.find(menu => menu.id === 'default');
    if (!menu?.id) throw new Error('No admin menu was returned.');
    originalSettings = JSON.parse(JSON.stringify(menu.primaryNavMenuSettings));

    await page.getByRole('button', { name: /Export JSON/i }).waitFor({ timeout: 10000 });
    // There are TWO .admin-menu-actions "add" buttons: the icon-only one that creates a
    // MENU (its popover offers Admin/Local/User Profile Menu) and the labelled "Add" that
    // adds a NODE or separator to the selected menu. :has-text("Add") matched both and
    // .first() picked the menu-creation one, whose popover has no "Node" entry — hence the
    // timeout. Match the accessible name exactly, as separators.js does.
    const addButton = page.getByRole('button', { name: 'Add', exact: true });
    await addButton.waitFor({ timeout: 10000 });
    // clickForEffect covers the prerendered-inert-button race (harness/interactive.js) —
    // a bare click here lands before handlers attach and the popover never opens.
    const { clickForEffect } = require('../../harness/interactive');
    await clickForEffect(addButton, page.locator('.admin-menu-actions__popover'));
    await page.locator('.admin-menu-actions__popover button:has-text("Node")').waitFor({ timeout: 5000 });
    await page.locator('.admin-menu-actions__popover button:has-text("Separator")').waitFor({ timeout: 5000 });

    // The button title and dialog aria-label are both "Primary navigation settings" —
    // the old "PrimaryNavMenu settings" wording no longer appears anywhere in the product.
    const settingsDialog = page.getByRole('dialog', { name: 'Primary navigation settings' });
    await clickForEffect(page.getByTitle('Primary navigation settings'), settingsDialog);

    const settingsSections = await page.evaluate(() => {
      const result = {};
      const dialog = document.querySelector('.admin-menu-settings');
      const headings = Array.from(dialog?.querySelectorAll('.rz-text-subtitle2') || []);
      for (const heading of headings) {
        const label = heading.textContent?.trim();
        const grid = heading.nextElementSibling;
        result[label] = Array.from(grid?.querySelectorAll('label') || []).map(item => item.textContent?.trim());
      }
      return result;
    });

    results.push({
      name: 'tier-4-fields-render-correctly',
      pass:
        !(settingsSections['Tier indentation'] || []).includes('Tier 4') &&
        (settingsSections['Tier backgrounds'] || []).includes('Tier 4') &&
        (settingsSections['Tier base size'] || []).includes('Tier 3-4'),
      message: JSON.stringify(settingsSections),
    });

    const tier2GeneratedSeparator = page.locator('.admin-menu-settings .rz-chkbox').nth(2);
    const separatorInitialClass = await tier2GeneratedSeparator.getAttribute('class');
    await tier2GeneratedSeparator.click();
    const separatorChangedClass = await tier2GeneratedSeparator.getAttribute('class');
    await page.waitForTimeout(6500);
    const separatorSettledClass = await tier2GeneratedSeparator.getAttribute('class');

    results.push({
      name: 'unsaved-checkbox-edit-not-reverted-by-reload',
      pass: separatorChangedClass !== separatorInitialClass && separatorSettledClass === separatorChangedClass,
      message: `initial=${separatorInitialClass} changed=${separatorChangedClass} settled=${separatorSettledClass}`,
    });

    const testSettings = {
      ...originalSettings,
      collapsible: false,
      expansionDurationMilliseconds: 333,
      tierIndents: ['0rem', '2px', '4px', '6px'],
      tierBackgrounds: ['transparent', 'transparent', 'transparent', 'transparent'],
      tierSeparators: [true, false, false],
      tierBaseSizes: ['1rem', '13px', '12px'],
      tierBaseRems: originalSettings.tierBaseRems || [1, 0.95, 0.9],
    };
    await savePrimaryNavMenuSettings(menu.id, testSettings, 'save test primaryNavMenu settings');

    await page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'domcontentloaded' });
    await page.locator('[data-testid="primary-nav-menu"]').waitFor({ timeout: 20000 });
    await page.waitForFunction(
      () => {
        const primaryNavMenu = document.querySelector('[data-testid="primary-nav-menu"]');
        return primaryNavMenu && !primaryNavMenu.querySelector('.primary-nav-menu__mode-toggle');
      },
      null,
      { timeout: 10000 },
    );

    const applied = await page.evaluate(() => {
      const primaryNavMenu = document.querySelector('[data-testid="primary-nav-menu"]');
      const menu = document.querySelector('.primary-nav-menu__menu');
      const computed = menu ? getComputedStyle(menu) : null;
      return {
        hasToggle: Boolean(primaryNavMenu?.querySelector('.primary-nav-menu__mode-toggle')),
        expansionDuration: getComputedStyle(primaryNavMenu).getPropertyValue('--primary-nav-menu-expansion-duration').trim(),
        tier2BaseSize: computed?.getPropertyValue('--crest-panel-menu-tier-2-base-size').trim(),
      };
    });

    results.push({
      name: 'saved-settings-apply-to-live-menu',
      pass: !applied.hasToggle && applied.expansionDuration === '333ms' && applied.tier2BaseSize === '13px',
      message: JSON.stringify(applied),
    });
  } finally {
    if (originalSettings) {
      const state = await page.evaluate(async () => {
        const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
        if (!response.ok) throw new Error(`Unable to reload admin menu state for restore: ${response.status}`);
        return response.json();
      });
      const menu = state.menus?.find(menu => menu.isDefault) || state.menus?.find(menu => menu.id === 'default');
      if (menu?.id) {
        await savePrimaryNavMenuSettings(menu.id, originalSettings, 'restore primaryNavMenu settings').catch(() => {});
      }
    }
  }

  return results;
};
