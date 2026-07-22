const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const headed = process.env.HEADED === '1';

async function login(page) {
  await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
  if (await page.locator('#UserName').count()) {
    await page.fill('#UserName', username);
    await page.fill('#Password', password);
    await page.press('#Password', 'Enter');
    await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

async function main() {
  const browser = await chromium.launch({ headless: !headed });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });

  page.on('console', message => console.log(`[browser:${message.type()}] ${message.text()}`));
  page.on('pageerror', error => console.log(`[browser:error] ${error.message}`));

  await login(page);
  await page.goto(`${baseUrl}/Admin/Content/ContentItems`, { waitUntil: 'networkidle' });
  await page.locator('.admin-menu-sidebar').waitFor({ timeout: 20000 });

  await page.locator('.admin-menu-sidebar').evaluate(sidebar => {
    const contentDefinition = Array.from(sidebar.querySelectorAll('.admin-menu-sidebar__item--level-1'))
      .find(item => (item.querySelector('.admin-menu-sidebar__item-content')?.textContent || '').trim() === 'Content Definition');

    if (contentDefinition?.getAttribute('aria-expanded') !== 'true') {
      contentDefinition?.querySelector('.rz-navigation-item-link')?.click();
    }
  });
  await page.waitForTimeout(250);

  const result = await page.locator('.admin-menu-sidebar').evaluate(sidebar => {
    const findItem = (level, text) => Array.from(sidebar.querySelectorAll(`.admin-menu-sidebar__item--level-${level}`))
      .find(item => (item.querySelector('.admin-menu-sidebar__item-content')?.textContent || '').trim() === text);

    const details = text => {
      const item = Array.from(sidebar.querySelectorAll('.admin-menu-sidebar__item--level-1'))
        .find(element => (element.querySelector('.admin-menu-sidebar__item-content')?.textContent || '').trim() === text);
      if (!item) {
        return null;
      }

      const textElement = item.querySelector('.rz-navigation-item-text');
      const icon = item.querySelector('.orchard-icon');
      const placeholder = item.querySelector('.admin-menu-sidebar__icon-placeholder');
      const textBox = textElement?.getBoundingClientRect();
      const iconBox = (icon || placeholder)?.getBoundingClientRect();

      return {
        text,
        hasIcon: !!icon,
        hasPlaceholder: !!placeholder,
        textLeft: textBox?.left || 0,
        iconWidth: iconBox?.width || 0,
        iconLeft: iconBox?.left || 0
      };
    };

    const adminMenus = details('Admin Menus');
    const menus = details('Menus');
    const contentDefinition = findItem(1, 'Content Definition');
    const expander = contentDefinition?.querySelector(':scope > .rz-expander');
    const levelTwoContainer = contentDefinition?.querySelector(':scope > .rz-expander > .rz-expander-content > .rz-navigation-menu');
    const containerStyle = levelTwoContainer ? getComputedStyle(levelTwoContainer) : null;
    const children = levelTwoContainer ? Array.from(levelTwoContainer.querySelectorAll(':scope > .admin-menu-sidebar__item--level-2')).map(item => ({
      text: (item.querySelector('.admin-menu-sidebar__item-content')?.textContent || '').trim(),
      left: item.getBoundingClientRect().left,
      textLeft: item.querySelector('.rz-navigation-item-text')?.getBoundingClientRect().left || 0
    })) : [];

    return {
      adminMenus,
      menus,
      textLeftDelta: adminMenus && menus ? Math.abs(adminMenus.textLeft - menus.textLeft) : null,
      levelTwoContainer: levelTwoContainer ? {
        expanded: contentDefinition?.getAttribute('aria-expanded') === 'true',
        ariaHidden: expander?.getAttribute('aria-hidden') || null,
        backgroundColor: containerStyle.backgroundColor,
        marginLeft: containerStyle.marginLeft,
        paddingTop: containerStyle.paddingTop,
        borderRadius: containerStyle.borderRadius,
        left: levelTwoContainer.getBoundingClientRect().left,
        parentLeft: contentDefinition.getBoundingClientRect().left,
        childCount: children.length,
        children
      } : null
    };
  });

  console.log(JSON.stringify(result, null, 2));

  if (!result.adminMenus?.hasIcon) {
    throw new Error('Expected Admin Menus submenu item to have an icon.');
  }

  if (!result.menus?.hasPlaceholder) {
    throw new Error('Expected Menus submenu item without an icon to render a placeholder slot.');
  }

  if (result.textLeftDelta === null || result.textLeftDelta > 1) {
    throw new Error(`Expected mixed icon/no-icon submenu text to align, delta ${result.textLeftDelta}.`);
  }

  const container = result.levelTwoContainer;
  if (!container || container.childCount < 2) {
    throw new Error('Expected Content Definition third-tier items to render inside a submenu container.');
  }

  if (!container.expanded || container.ariaHidden === 'true') {
    throw new Error('Expected third-tier submenu container to be expanded and visible.');
  }

  if (/rgba?\(0, 0, 0(?:, 0)?\)/.test(container.backgroundColor)) {
    throw new Error('Expected third-tier submenu container to have a non-transparent theme background.');
  }

  if (parseFloat(container.marginLeft) <= 0) {
    throw new Error(`Expected third-tier submenu container to be visually nested, margin-left ${container.marginLeft}.`);
  }

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
