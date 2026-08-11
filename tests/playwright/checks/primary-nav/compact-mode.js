// Converted from OrchardCore.Crest/tests/playwright/admin-primary-nav-menu-compact-mode.js.
// Collapsed (compact/rail) primaryNavMenu: correct rail width/icon sizing, active-item
// accent styling, auto-generated tier separators, animated expand/collapse of children,
// and hover-to-overlay-expand behavior (with a one-second hover delay before it opens,
// and the underlying main content never shifting).
module.exports = async function run(page, ctx) {
  // Must land on a route that IS an admin-menu link: this check asserts active-item accent
  // styling and expand/collapse of children, both of which require a resolved active trail.
  // /Admin/Settings/SecurityHeaders renders fine but appears nowhere in the menu (Settings
  // exposes General/Admin/Localization), so it yields no active item and no expandable item.
  await page.goto(`${ctx.baseUrl}/Admin/Contents/ContentItems`, { waitUntil: 'domcontentloaded' });
  await page.locator('[data-testid="primary-nav-menu"] .crest-panel-menu__item-link--active').waitFor({ timeout: 20000 });

  const primaryNavMenu = page.locator('[data-testid="primary-nav-menu"]');
  const toggle = page.getByRole('button', { name: 'Collapse navigation' });
  await primaryNavMenu.waitFor({ timeout: 20000 });
  // clickForEffect covers the prerendered-inert-button race (harness/interactive.js).
  const { clickForEffect } = require('../../harness/interactive');
  await clickForEffect(toggle, page.locator('[data-testid="primary-nav-menu"].primary-nav-menu--compact'));
  await page.locator('.admin-dashboard__main').hover();
  await page.waitForTimeout(240);
  await page.locator('[data-testid="primary-nav-menu"].primary-nav-menu--compact').waitFor({ timeout: 5000 });

  const compact = await primaryNavMenu.evaluate(element => ({
    width: element.getBoundingClientRect().width,
    textOpacity: getComputedStyle(element.querySelector('.crest-panel-menu__text-rail')).opacity,
    textWidth: element.querySelector('.crest-panel-menu__text-rail')?.getBoundingClientRect().width || 0,
    iconRailWidth: element.querySelector('.crest-panel-menu__icon-rail')?.getBoundingClientRect().width || 0,
    scrollable: getComputedStyle(element.querySelector('.primary-nav-menu__menu')).overflowY,
    scrollbarWidth: getComputedStyle(element.querySelector('.primary-nav-menu__menu')).scrollbarWidth,
  }));
  const compactStylingOk =
    compact.width <= 80 && compact.textOpacity === '0' && compact.textWidth <= 1 && Math.abs(compact.iconRailWidth - 64) <= 1 &&
    compact.scrollable === 'auto' && compact.scrollbarWidth === 'none';

  const activeStyle = await primaryNavMenu.locator('.crest-panel-menu__item-link--active').first().evaluate(element => {
    const icon = element.querySelector('.crest-panel-menu__icon-rail');
    const styles = getComputedStyle(element);
    const iconStyles = getComputedStyle(icon);
    return { backgroundColor: styles.backgroundColor, color: styles.color, iconColor: iconStyles.color };
  });
  const activeStyleOk = activeStyle.backgroundColor !== 'rgba(0, 0, 0, 0)' && activeStyle.iconColor !== activeStyle.color;

  const autoSeparators = await primaryNavMenu.evaluate(element =>
    [...element.querySelectorAll('.crest-panel-menu__item--auto-separator')].map(item => ({
      level0: item.classList.contains('crest-panel-menu__item--level-0'),
      level1: item.classList.contains('crest-panel-menu__item--level-1'),
      lineWidth: item.querySelector('.crest-panel-menu__separator-line')?.getBoundingClientRect().width || 0,
    })),
  );
  const separatorsOk = autoSeparators.length > 0 && autoSeparators.every(separator => separator.level0 && !separator.level1 && separator.lineWidth > 0);

  const expandableItem = primaryNavMenu.locator('.crest-panel-menu__item-link[aria-expanded]').first();
  await expandableItem.waitFor({ timeout: 10000 });
  // Blazor emits aria-expanded="" (empty string) for a true bool and OMITS the attribute
  // when false — never the literal "true"/"false". Expanded == present and not "false".
  const expandedAttr = await expandableItem.getAttribute('aria-expanded');
  if (expandedAttr === null || expandedAttr === 'false') {
    await expandableItem.click();
  }
  const expandedChevron = await expandableItem.locator('.crest-panel-menu__expand-icon').evaluate(element => getComputedStyle(element).transform);

  // CrestPanelMenuItems renders a children container ONLY while the item is expanded
  // (`hasExpandableChildren && IsExpanded(item)`), so .crest-panel-menu__children--collapsed
  // no longer exists in the DOM and waiting for it timed out. The safety property this
  // asserted — collapsed children must not be reachable by keyboard or screen reader — is
  // now guaranteed structurally: an unexpanded item has a collapsed parent and no children
  // in the DOM at all. Assert that instead.
  const collapsedChildren = await primaryNavMenu.evaluate(root => {
    // Blazor emits aria-expanded="" (empty string) for a true bool and OMITS the attribute
    // when false — it never writes "false". So "collapsed" means the attribute is absent
    // (or literally "false"), never the empty string.
    const isExpanded = link => link !== null && link.hasAttribute('aria-expanded') && link.getAttribute('aria-expanded') !== 'false';
    const collapsedParents = [...root.querySelectorAll('.crest-panel-menu__item--has-children')].filter(
      item => !isExpanded(item.querySelector(':scope > .crest-panel-menu__item-wrapper > .crest-panel-menu__item-link')),
    );
    return {
      collapsedParentCount: collapsedParents.length,
      // No collapsed parent may carry a rendered children container.
      noRenderedChildren: collapsedParents.every(item => !item.querySelector(':scope > .crest-panel-menu__children')),
    };
  });

  await page.reload({ waitUntil: 'domcontentloaded' });
  await page.locator('[data-testid="primary-nav-menu"].primary-nav-menu--compact').waitFor({ timeout: 20000 });

  const visibleNestedIcons = await primaryNavMenu.evaluate(element =>
    [...element.querySelectorAll('.primary-nav-menu__item--level-1, .primary-nav-menu__item--level-2, .primary-nav-menu__item--level-3')]
      .filter(item => item.getClientRects().length > 0)
      .map(item => {
        const icon = item.querySelector('.primary-nav-menu__icon');
        const rect = icon?.getBoundingClientRect();
        return { width: rect?.width || 0, rowHeight: item.querySelector('.crest-panel-menu__item-link')?.getBoundingClientRect().height || 0 };
      }),
  );

  const mainBeforeHover = await page.locator('.admin-dashboard__main').boundingBox();
  await primaryNavMenu.hover();
  await page.waitForTimeout(400);
  const preDelayWidth = await primaryNavMenu.locator('.primary-nav-menu__content').evaluate(element => element.getBoundingClientRect().width);
  const openedBeforeDelay = preDelayWidth > 80;

  await page.waitForTimeout(1050 + 380);
  const hover = await primaryNavMenu.locator('.primary-nav-menu__content').evaluate(element => ({
    width: element.getBoundingClientRect().width,
    textOpacity: getComputedStyle(element.querySelector('.crest-panel-menu__text-rail')).opacity,
    textWidth: element.querySelector('.crest-panel-menu__text-rail')?.getBoundingClientRect().width || 0,
    iconRailWidth: element.querySelector('.crest-panel-menu__icon-rail')?.getBoundingClientRect().width || 0,
  }));
  const mainAfterHover = await page.locator('.admin-dashboard__main').boundingBox();
  const expandedNestedIcons = await primaryNavMenu.evaluate(element =>
    [...element.querySelectorAll('.primary-nav-menu__item--level-1, .primary-nav-menu__item--level-2, .primary-nav-menu__item--level-3')]
      .filter(item => item.getClientRects().length > 0)
      .map(item => ({ rowHeight: item.querySelector('.crest-panel-menu__item-link')?.getBoundingClientRect().height || 0 })),
  );

  // Re-pin expanded before leaving: the pinned state is a persisted user preference, so
  // a collapsed nav left behind here changes the starting state of every later check.
  await clickForEffect(
    page.getByRole('button', { name: 'Keep navigation expanded' }),
    page.locator('[data-testid="primary-nav-menu"]:not(.primary-nav-menu--compact)'),
  ).catch(() => {});
  const pinned = await page.locator('[data-testid="primary-nav-menu"].primary-nav-menu--pinned').count().catch(() => 0);

  return [
    { name: 'compact-rail-styling', pass: compactStylingOk, message: JSON.stringify(compact) },
    { name: 'active-item-accent-styling', pass: activeStyleOk, message: JSON.stringify(activeStyle) },
    { name: 'auto-separators-tier1-only', pass: separatorsOk, message: JSON.stringify(autoSeparators.slice(0, 8)) },
    { name: 'expanded-chevron-rotates', pass: expandedChevron !== 'none', message: `transform=${expandedChevron}` },
    {
      name: 'collapsed-children-mounted-safely',
      pass: collapsedChildren.collapsedParentCount > 0 && collapsedChildren.noRenderedChildren,
      message: JSON.stringify(collapsedChildren),
    },
    {
      name: 'nested-icons-retain-geometry-across-reload',
      pass:
        visibleNestedIcons.length > 0 &&
        visibleNestedIcons.every(icon => icon.width > 0 && Math.abs(icon.rowHeight - 40) <= 1) &&
        expandedNestedIcons.every(icon => Math.abs(icon.rowHeight - 40) <= 1),
      message: `visibleCount=${visibleNestedIcons.length}`,
    },
    { name: 'hover-expand-waits-for-delay', pass: !openedBeforeDelay, message: `preDelayWidth=${preDelayWidth}` },
    {
      name: 'hover-expand-overlays-without-shifting-main',
      pass: hover.width >= 250 && hover.textOpacity === '1' && hover.textWidth >= 100 && Math.abs(hover.iconRailWidth - 64) <= 1 && mainBeforeHover.x === mainAfterHover.x,
      message: JSON.stringify({ hover, mainBeforeHover, mainAfterHover }),
    },
    { name: 'pin-button-locks-expanded-mode', pass: pinned > 0, message: `pinnedCount=${pinned}` },
  ];
};
