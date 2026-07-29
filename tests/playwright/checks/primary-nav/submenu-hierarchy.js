// Converted from OrchardCore.Crest.Admin/tests/playwright/primary-nav-menu-submenu-hierarchy.js.
// Verifies mixed icon/no-icon top-level items align their text, and that expanding a
// third-tier submenu (Content Definition) renders a visible, indented, themed container.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Content/ContentItems`, { waitUntil: 'networkidle' });
  await page.locator('.primary-nav-menu').waitFor({ timeout: 20000 });

  await page.locator('.primary-nav-menu').evaluate(primaryNavMenu => {
    const contentDefinition = Array.from(primaryNavMenu.querySelectorAll('.primary-nav-menu__item--level-1')).find(
      item => (item.querySelector('.primary-nav-menu__item-content')?.textContent || '').trim() === 'Content Definition',
    );
    if (contentDefinition?.getAttribute('aria-expanded') !== 'true') {
      contentDefinition?.querySelector('.rz-navigation-item-link')?.click();
    }
  });
  await page.waitForTimeout(250);

  const result = await page.locator('.primary-nav-menu').evaluate(primaryNavMenu => {
    const findItem = (level, text) =>
      Array.from(primaryNavMenu.querySelectorAll(`.primary-nav-menu__item--level-${level}`)).find(
        item => (item.querySelector('.primary-nav-menu__item-content')?.textContent || '').trim() === text,
      );

    const details = text => {
      const item = Array.from(primaryNavMenu.querySelectorAll('.primary-nav-menu__item--level-1')).find(
        element => (element.querySelector('.primary-nav-menu__item-content')?.textContent || '').trim() === text,
      );
      if (!item) return null;
      const textElement = item.querySelector('.rz-navigation-item-text');
      const icon = item.querySelector('.orchard-icon');
      const placeholder = item.querySelector('.primary-nav-menu__icon-placeholder');
      const textBox = textElement?.getBoundingClientRect();
      const iconBox = (icon || placeholder)?.getBoundingClientRect();
      return { text, hasIcon: !!icon, hasPlaceholder: !!placeholder, textLeft: textBox?.left || 0, iconWidth: iconBox?.width || 0 };
    };

    const adminMenus = details('Admin Menus');
    const menus = details('Menus');
    const contentDefinition = findItem(1, 'Content Definition');
    const expander = contentDefinition?.querySelector(':scope > .rz-expander');
    const levelTwoContainer = contentDefinition?.querySelector(':scope > .rz-expander > .rz-expander-content > .rz-navigation-menu');
    const containerStyle = levelTwoContainer ? getComputedStyle(levelTwoContainer) : null;
    const children = levelTwoContainer
      ? Array.from(levelTwoContainer.querySelectorAll(':scope > .primary-nav-menu__item--level-2')).map(item => ({
          text: (item.querySelector('.primary-nav-menu__item-content')?.textContent || '').trim(),
        }))
      : [];

    return {
      adminMenus,
      menus,
      textLeftDelta: adminMenus && menus ? Math.abs(adminMenus.textLeft - menus.textLeft) : null,
      levelTwoContainer: levelTwoContainer
        ? {
            expanded: contentDefinition?.getAttribute('aria-expanded') === 'true',
            ariaHidden: expander?.getAttribute('aria-hidden') || null,
            backgroundColor: containerStyle.backgroundColor,
            marginLeft: containerStyle.marginLeft,
            childCount: children.length,
          }
        : null,
    };
  });

  return [
    { name: 'icon-item-has-icon', pass: Boolean(result.adminMenus?.hasIcon), message: JSON.stringify(result.adminMenus) },
    { name: 'iconless-item-has-placeholder', pass: Boolean(result.menus?.hasPlaceholder), message: JSON.stringify(result.menus) },
    {
      name: 'mixed-icon-items-text-aligned',
      pass: result.textLeftDelta !== null && result.textLeftDelta <= 1,
      message: `textLeftDelta=${result.textLeftDelta}`,
    },
    {
      name: 'third-tier-submenu-renders-expanded',
      pass: Boolean(
        result.levelTwoContainer &&
          result.levelTwoContainer.childCount >= 2 &&
          result.levelTwoContainer.expanded &&
          result.levelTwoContainer.ariaHidden !== 'true',
      ),
      message: JSON.stringify(result.levelTwoContainer),
    },
    {
      name: 'third-tier-submenu-themed-and-nested',
      pass: Boolean(
        result.levelTwoContainer &&
          !/rgba?\(0, 0, 0(?:, 0)?\)/.test(result.levelTwoContainer.backgroundColor) &&
          parseFloat(result.levelTwoContainer.marginLeft) > 0,
      ),
      message: JSON.stringify(result.levelTwoContainer),
    },
  ];
};
