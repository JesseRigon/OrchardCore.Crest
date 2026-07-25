const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function login(page) {
  await page.goto(`${baseUrl}/login`, { waitUntil: 'domcontentloaded' });
  await page.locator('input[name="UserName"]').waitFor({ timeout: 20000 });
  await page.locator('input[name="UserName"]').fill(username);
  await page.locator('input[name="Password"]').fill(password);
  await page.getByRole('button', { name: 'Login', exact: true }).click();
  await page.waitForURL(/\/admin/i, { timeout: 20000 });
}

async function saveSidebarSettings(page, menuId, settings, action) {
  await page.evaluate(async ({ menuId, settings, action }) => {
    const tokenResponse = await fetch('/api/crest/antiforgery/token', { credentials: 'include' });
    if (!tokenResponse.ok) {
      throw new Error(`Unable to load antiforgery token before ${action}: ${tokenResponse.status} ${await tokenResponse.text()}`);
    }

    const token = await tokenResponse.json();
    const response = await fetch(`/api/crest/admin-menus/${encodeURIComponent(menuId)}/sidebar-settings`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        'content-type': 'application/json',
        [token.headerName || 'RequestVerificationToken']: token.requestToken,
      },
      body: JSON.stringify(settings),
    });

    if (!response.ok) {
      throw new Error(`Unable to ${action}: ${response.status} ${await response.text()}`);
    }
  }, { menuId, settings, action });
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  let originalSettings = null;
  try {
    const page = await browser.newPage({ viewport: { width: 1366, height: 900 } });
    await login(page);
    await page.goto(`${baseUrl}/Admin/AdminMenus`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: 'Sidebar Layout', exact: true }).waitFor({ timeout: 20000 });

    const state = await page.evaluate(async () => {
      const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
      if (!response.ok) {
        throw new Error(`Unable to load admin menu state: ${response.status}`);
      }

      return await response.json();
    });

    const menu = state.menus?.find(menu => menu.isDefault) || state.menus?.find(menu => menu.id === 'default');
    if (!menu?.id) {
      throw new Error('No admin menu was returned.');
    }

    originalSettings = JSON.parse(JSON.stringify(menu.sidebarSettings));

    await page.getByRole('button', { name: /Export JSON/i }).waitFor({ timeout: 10000 });

    const addButton = page.locator('.admin-menu-actions button:has-text("Add")').first();
    await addButton.waitFor({ timeout: 10000 });
    await addButton.click();
    await page.locator('.admin-menu-actions__popover button:has-text("Node")').waitFor({ timeout: 5000 });
    await page.locator('.admin-menu-actions__popover button:has-text("Separator")').waitFor({ timeout: 5000 });

    await page.getByTitle('Sidebar settings').click();
    await page.getByRole('dialog', { name: 'Sidebar settings' }).waitFor({ timeout: 5000 });
    await page.getByText('Allow collapse/hover expand').waitFor({ timeout: 5000 });
    await page.getByText('Expansion speed (ms)').waitFor({ timeout: 5000 });
    await page.getByText('Generated separators').waitFor({ timeout: 5000 });
    await page.getByText('Tier backgrounds').waitFor({ timeout: 5000 });
    await page.getByText('Tier base size').waitFor({ timeout: 5000 });

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

    if ((settingsSections['Tier indentation'] || []).includes('Tier 4')) {
      throw new Error('Tier 4 indentation input should not be rendered.');
    }

    if (!(settingsSections['Tier backgrounds'] || []).includes('Tier 4')) {
      throw new Error('Tier 4 background input should still be rendered.');
    }

    if (!(settingsSections['Tier base size'] || []).includes('Tier 3-4')) {
      throw new Error('Tier base size should label the final input as Tier 3-4.');
    }

    const tier2GeneratedSeparator = page.locator('.admin-menu-settings .rz-chkbox').nth(2);
    const separatorInitialClass = await tier2GeneratedSeparator.getAttribute('class');
    await tier2GeneratedSeparator.click();
    const separatorChangedClass = await tier2GeneratedSeparator.getAttribute('class');
    if (separatorChangedClass === separatorInitialClass) {
      throw new Error('Tier 2 generated separator checkbox did not toggle.');
    }

    await page.waitForTimeout(6500);
    const separatorSettledClass = await tier2GeneratedSeparator.getAttribute('class');
    if (separatorSettledClass !== separatorChangedClass) {
      throw new Error('Sidebar settings flyout reloaded and reverted an unsaved checkbox edit.');
    }

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

    await saveSidebarSettings(page, menu.id, testSettings, 'save test sidebar settings');

    await page.goto(`${baseUrl}/Admin`, { waitUntil: 'domcontentloaded' });
    await page.locator('[data-testid="admin-menu-sidebar"]').waitFor({ timeout: 20000 });
    await page.waitForFunction(() => {
      const sidebar = document.querySelector('[data-testid="admin-menu-sidebar"]');
      return sidebar && !sidebar.querySelector('.admin-menu-sidebar__mode-toggle');
    }, null, { timeout: 10000 });

    const applied = await page.evaluate(() => {
      const sidebar = document.querySelector('[data-testid="admin-menu-sidebar"]');
      const menu = document.querySelector('.admin-menu-sidebar__menu');
      const computed = menu ? getComputedStyle(menu) : null;
      return {
        hasToggle: Boolean(sidebar?.querySelector('.admin-menu-sidebar__mode-toggle')),
        expansionDuration: getComputedStyle(sidebar).getPropertyValue('--admin-menu-sidebar-expansion-duration').trim(),
        tier2BaseSize: computed?.getPropertyValue('--crest-panel-menu-tier-2-base-size').trim(),
      };
    });

    if (applied.hasToggle) {
      throw new Error('Sidebar still rendered a collapse toggle after collapsible=false was saved.');
    }

    if (applied.expansionDuration !== '333ms') {
      throw new Error(`Sidebar expansion duration was not applied. Found ${applied.expansionDuration}`);
    }

    if (applied.tier2BaseSize !== '13px') {
      throw new Error(`Sidebar tier base size was not applied. Found ${applied.tier2BaseSize}`);
    }

    console.log(JSON.stringify({ editor: 'ok' }));
  } finally {
    if (originalSettings) {
      const page = await browser.newPage();
      await login(page);
      const state = await page.evaluate(async () => {
        const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
        if (!response.ok) {
          throw new Error(`Unable to reload admin menu state for restore: ${response.status}`);
        }

        return await response.json();
      });
      const menu = state.menus?.find(menu => menu.isDefault) || state.menus?.find(menu => menu.id === 'default');
      if (menu?.id) {
        await saveSidebarSettings(page, menu.id, originalSettings, 'restore sidebar settings');
      }
      await page.close();
    }
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
