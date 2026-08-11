// Converted from OrchardCore.Crest.Admin/tests/playwright/primary-nav-menu-fourth-tier-flyout.js.
// A deeply-nested primaryNavMenu item's flyout must render detached (fixed-position,
// portalled outside the menu tree) rather than as an inline submenu, and must actually
// hit-test at its rendered screen position on hover.
// RETARGETED (Phase 8 triage): this used to drive Platform > Search > Queries > "All
// Queries". The admin menu no longer has a "Queries" node at all — Search's only child
// is now "Indexes", which is childless and therefore cannot produce a flyout. Flyouts
// need a level >= FlyoutDepth (2) item that HAS children, and the current menu contains
// exactly one such node: Platform > Settings > Localization > Cultures. The behaviour
// under test (detached, hit-testable flyout) is unchanged; only the node exercising it is.
module.exports = async function run(page, ctx) {
  const parentText = 'Localization';
  const childText = 'Cultures';

  await page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });
  const primaryNavMenu = page.locator('.primary-nav-menu');
  await primaryNavMenu.waitFor({ timeout: 20000 });

  // Every check shares one page and one mouse. If the pointer is still resting over a
  // flyout trigger from an earlier check, a flyout stays open and the "no flyout before
  // hover" baseline below is violated. Park the pointer in dead space and wait for the
  // flyout host to go back to display:none (it is never removed from the DOM).
  await page.mouse.move(900, 950);
  await page.locator('.primary-nav-menu__flyout--detached').waitFor({ state: 'hidden', timeout: 5000 }).catch(() => {});

  // Expand Platform -> Settings so the nested Localization item renders. Collapsed children
  // are not in the DOM at all (CrestPanelMenuItems renders them only when expanded),
  // and the old .rz-expander/.rz-navigation-item-link structure is gone since the
  // panel-menu refactor. clickForEffect covers the prerendered-inert-button race.
  const { clickForEffect } = require('../../harness/interactive');
  const expandLink = label =>
    primaryNavMenu.locator(`button.crest-panel-menu__item-link:has(.crest-panel-menu__text-rail:text-is("${label}"))`).first();
  const itemContent = label =>
    primaryNavMenu.locator(`.crest-panel-menu__item-content:has(.crest-panel-menu__text-rail:text-is("${label}"))`).first();
  await clickForEffect(expandLink('Platform'), itemContent('Settings'));
  await clickForEffect(expandLink('Settings'), itemContent('Localization'));
  await page.waitForTimeout(250);

  const before = await primaryNavMenu.evaluate(
    (root, { parentText, childText }) => {
      const textOf = element => (element.querySelector('.crest-panel-menu__item-content')?.textContent || '').replace(/\s+/g, ' ').trim();
      const isReachable = element => !element.closest('.rz-expander.rz-state-collapsed');
      const parent = Array.from(root.querySelectorAll('.primary-nav-menu__item')).find(item => textOf(item) === parentText && isReachable(item));
      if (!parent) return { found: false };
      const inlineFlyout = parent.querySelector('.primary-nav-menu__flyout');
      // The detached flyout host is ALWAYS in the DOM, portalled to the menu root; it just
      // sits at display:none with no items until a hover populates it. So presence alone
      // means nothing — "showing" is display !== none.
      const detachedHost = root.querySelector('.primary-nav-menu__flyout--detached');
      const detachedFlyout = detachedHost && getComputedStyle(detachedHost).display !== 'none' ? detachedHost : null;
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
      const textOf = element => (element.querySelector('.crest-panel-menu__item-content')?.textContent || '').replace(/\s+/g, ' ').trim();
      const isReachable = element => !element.closest('.rz-expander.rz-state-collapsed');
      root.querySelectorAll('[data-primaryNavMenu-flyout-target]').forEach(element => element.removeAttribute('data-primaryNavMenu-flyout-target'));
      const parent = Array.from(root.querySelectorAll('.primary-nav-menu__item')).find(item => textOf(item) === parentText && isReachable(item));
      parent.setAttribute('data-primaryNavMenu-flyout-target', 'true');
      parent.scrollIntoView({ block: 'center', inline: 'nearest' });
    },
    { parentText },
  );
  await page.locator('[data-primaryNavMenu-flyout-target="true"] .crest-panel-menu__item-content').hover();
  // The flyout opens on a hover delay (~1s), so a fixed 250ms wait sampled too early.
  // Wait for the host to actually become visible instead.
  await page.locator('.primary-nav-menu__flyout--detached').waitFor({ state: 'visible', timeout: 10000 }).catch(() => {});

  const after = await primaryNavMenu.evaluate(
    (root, { parentText }) => {
      const textOf = element => (element.querySelector('.crest-panel-menu__item-content')?.textContent || '').replace(/\s+/g, ' ').trim();
      const isReachable = element => !element.closest('.rz-expander.rz-state-collapsed');
      const parent = Array.from(root.querySelectorAll('.primary-nav-menu__item')).find(item => textOf(item) === parentText && isReachable(item));
      if (!parent) return { error: `no reachable item titled "${parentText}"` };
      const parentBox = parent.getBoundingClientRect();
      // The detached flyout is portalled to the .primary-nav-menu root itself (which is
      // `root` here), NOT to root.parentElement — "detached" means it escapes the menu
      // item's own subtree, not the menu container. Searching the parent found nothing
      // and getComputedStyle(null) threw.
      const flyout = root.querySelector('.primary-nav-menu__flyout--detached');
      if (!flyout || getComputedStyle(flyout).display === 'none') {
        return { error: 'detached flyout host never became visible on hover' };
      }
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

  if (after.error) {
    return [{ name: 'flyout-appears-on-hover', pass: false, message: after.error }];
  }

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
