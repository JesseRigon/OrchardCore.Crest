// Converted from OrchardCore.Crest.Admin/tests/playwright/primary-nav-menu-fourth-tier-flyout.js.
// A deeply-nested primaryNavMenu item's flyout must render detached (fixed-position,
// portalled outside the menu tree) rather than as an inline submenu, and must actually
// hit-test at its rendered screen position on hover.
module.exports = async function run(page, ctx) {
  const parentText = 'Queries';
  const childText = 'All Queries';

  await page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });
  const primaryNavMenu = page.locator('.primary-nav-menu');
  await primaryNavMenu.waitFor({ timeout: 20000 });

  for (const label of ['Platform', 'Search']) {
    await primaryNavMenu.evaluate((root, label) => {
      const textOf = element => (element.querySelector('.primary-nav-menu__item-content')?.textContent || '').replace(/\s+/g, ' ').trim();
      const item = Array.from(root.querySelectorAll('.primary-nav-menu__item')).find(
        item => textOf(item) === label && !item.closest('.rz-expander.rz-state-collapsed'),
      );
      if (item?.getAttribute('aria-expanded') !== 'true') {
        item
          ?.querySelector(
            ':scope > .rz-expander > .rz-navigation-item-wrapper > .rz-navigation-item-link, :scope > .rz-navigation-item-wrapper > .rz-navigation-item-link',
          )
          ?.click();
      }
    }, label);
    await page.waitForTimeout(250);
  }

  const before = await primaryNavMenu.evaluate(
    (root, { parentText, childText }) => {
      const textOf = element => (element.querySelector('.primary-nav-menu__item-content')?.textContent || '').replace(/\s+/g, ' ').trim();
      const isReachable = element => !element.closest('.rz-expander.rz-state-collapsed');
      const parent = Array.from(root.querySelectorAll('.primary-nav-menu__item')).find(item => textOf(item) === parentText && isReachable(item));
      if (!parent) return { found: false };
      const inlineFlyout = parent.querySelector('.primary-nav-menu__flyout');
      const detachedFlyout = root.parentElement?.querySelector(':scope > .primary-nav-menu__flyout--detached');
      const inlineChildren = Array.from(
        parent.querySelectorAll(':scope > .rz-expander > .rz-expander-content .primary-nav-menu__item, :scope > .rz-navigation-menu .primary-nav-menu__item'),
      )
        .map(item => textOf(item))
        .filter(Boolean);
      return { found: true, hasInlineFlyout: !!inlineFlyout, hasDetachedFlyout: !!detachedFlyout, childInInline: inlineChildren.includes(childText) };
    },
    { parentText, childText },
  );

  if (!before.found) {
    return [{ name: 'flyout-parent-found', pass: false, message: `Could not find primaryNavMenu item "${parentText}".` }];
  }

  await primaryNavMenu.evaluate(
    (root, { parentText }) => {
      const textOf = element => (element.querySelector('.primary-nav-menu__item-content')?.textContent || '').replace(/\s+/g, ' ').trim();
      const isReachable = element => !element.closest('.rz-expander.rz-state-collapsed');
      root.querySelectorAll('[data-primaryNavMenu-flyout-target]').forEach(element => element.removeAttribute('data-primaryNavMenu-flyout-target'));
      const parent = Array.from(root.querySelectorAll('.primary-nav-menu__item')).find(item => textOf(item) === parentText && isReachable(item));
      parent.setAttribute('data-primaryNavMenu-flyout-target', 'true');
      parent.scrollIntoView({ block: 'center', inline: 'nearest' });
    },
    { parentText },
  );
  await page.locator('[data-primaryNavMenu-flyout-target="true"] .primary-nav-menu__item-content').hover();
  await page.waitForTimeout(250);

  const after = await primaryNavMenu.evaluate(
    (root, { parentText }) => {
      const textOf = element => (element.querySelector('.primary-nav-menu__item-content')?.textContent || '').replace(/\s+/g, ' ').trim();
      const isReachable = element => !element.closest('.rz-expander.rz-state-collapsed');
      const parent = Array.from(root.querySelectorAll('.primary-nav-menu__item')).find(item => textOf(item) === parentText && isReachable(item));
      const parentBox = parent.getBoundingClientRect();
      const flyout = root.parentElement?.querySelector(':scope > .primary-nav-menu__flyout--detached');
      const flyoutStyle = getComputedStyle(flyout);
      const flyoutBox = flyout.getBoundingClientRect();
      const flyoutCenterX = flyoutBox.left + flyoutBox.width / 2;
      const flyoutCenterY = flyoutBox.top + Math.min(flyoutBox.height / 2, 20);
      const pointElement = document.elementFromPoint(flyoutCenterX, flyoutCenterY);
      return {
        flyoutDisplay: flyoutStyle.display,
        flyoutPosition: flyoutStyle.position,
        parentLeft: parentBox.left,
        parentWidth: parentBox.width,
        flyoutLeft: flyoutBox.left,
        flyoutHit: !!pointElement?.closest?.('.primary-nav-menu__flyout'),
      };
    },
    { parentText },
  );

  return [
    {
      name: 'flyout-not-rendered-inline',
      pass: !before.hasInlineFlyout && !before.hasDetachedFlyout && !before.childInInline,
      message: JSON.stringify(before),
    },
    { name: 'flyout-visible-on-hover', pass: after.flyoutDisplay !== 'none', message: `display=${after.flyoutDisplay}` },
    { name: 'flyout-uses-fixed-positioning', pass: after.flyoutPosition === 'fixed', message: `position=${after.flyoutPosition}` },
    {
      name: 'flyout-renders-as-right-side-popup',
      pass: after.flyoutLeft >= after.parentLeft + after.parentWidth * 0.75,
      message: JSON.stringify(after),
    },
    { name: 'flyout-hit-tests-at-its-position', pass: after.flyoutHit, message: JSON.stringify(after) },
  ];
};
