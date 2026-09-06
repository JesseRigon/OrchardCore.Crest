// Converted from OrchardCore.Crest.Admin/tests/playwright/primary-nav-menu-fourth-tier-flyout.js.
// A deeply-nested primaryNavMenu item's flyout must render detached (fixed-position,
// portalled outside the menu tree) rather than as an inline submenu, and must actually
// hit-test at its rendered screen position on hover.
// RETARGETED three times (Phase 8 triage, the provider-menu import, then layout
// independence): the geometry it needs is "a child-bearing item at rendered level 2
// whose ancestors are inline-expandable" - FlyoutDepth is 2, and ancestors at level
// >= 2 would themselves be flyout parents, unreachable by inline expansion. A tenant's
// layout overlay may already provide that shape (any depth-2 node with children); when
// it does not (a fresh stock tenant is only three levels deep), the check BUILDS the
// geometry: it moves a child-bearing root under some depth-1 leaf via the same move
// API the editor uses, and restores the layout afterwards.
const { fetchAntiforgeryToken } = require('../../harness/antiforgery');
const { clickForEffect } = require('../../harness/interactive');

module.exports = async function run(page, ctx) {
  async function moveNode(nodeId, parentNodeId, position) {
    const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
    return page.evaluate(async ({ nodeId, parentNodeId, position, antiforgery }) => {
      const response = await fetch(`/api/crest/admin-menus/__crest_default_admin_menu/nodes/${encodeURIComponent(nodeId)}/move`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
        body: JSON.stringify({ parentNodeId, position }),
      });
      if (!response.ok) throw new Error(`move failed: ${response.status} ${await response.text()}`);
    }, { nodeId, parentNodeId, position, antiforgery });
  }

  // Resolve geometry from the live menu - keys are UniqueIds, not knowable up front.
  const plan = await page.evaluate(async () => {
    const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
    if (!response.ok) throw new Error(`admin menus failed: ${response.status}`);
    const data = await response.json();
    const menu = data.menus.find(candidate => candidate.id === '__crest_default_admin_menu');
    const enabled = nodes => (nodes || []).filter(node => node.enabled !== false);

    // Preferred: a child-bearing node ALREADY at depth 2 (inline-expandable ancestors).
    for (const root of enabled(menu.nodes)) {
      if (root.id === 'new') continue;
      for (const levelOne of enabled(root.items)) {
        for (const levelTwo of enabled(levelOne.items)) {
          const children = enabled(levelTwo.items);
          if (children.length > 0) {
            return {
              build: null,
              chain: [root.text, levelOne.text],
              parentText: levelTwo.text,
              childText: children[0].text,
            };
          }
        }
      }
    }

    // Otherwise build it: a child-bearing root moved under some depth-1 leaf.
    const movable = enabled(menu.nodes).find(root =>
      root.id !== 'new' && !String(root.id).startsWith('custom-') && enabled(root.items).length > 0);
    for (const root of enabled(menu.nodes)) {
      if (root.id === 'new' || !movable || root.id === movable.id) continue;
      const leaf = enabled(root.items).find(node => enabled(node.items).length === 0 && node.id !== movable.id);
      if (leaf) {
        return {
          build: {
            nodeId: movable.id,
            targetId: leaf.id,
            restoreIndex: Math.max(0, menu.nodes.findIndex(node => node.id === movable.id)),
          },
          chain: [root.text, leaf.text],
          parentText: movable.text,
          childText: enabled(movable.items)[0].text,
        };
      }
    }
    return null;
  });

  if (!plan) {
    return [{ name: 'flyout-parent-found', pass: false, message: 'no depth-2 child-bearing node and no way to build one' }];
  }

  if (plan.build) {
    // Root(0) > leaf(1) > moved node(2, has children): the shallowest arrangement that
    // puts a child-bearing item at FlyoutDepth.
    await moveNode(plan.build.nodeId, plan.build.targetId, 0);
  }

  const { parentText, childText } = plan;

  try {

  await page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });
  const primaryNavMenu = page.locator('.primary-nav-menu');
  await primaryNavMenu.waitFor({ timeout: 20000 });

  // Every check shares one page and one mouse. If the pointer is still resting over a
  // flyout trigger from an earlier check, a flyout stays open and the "no flyout before
  // hover" baseline below is violated. Park the pointer in dead space and wait for the
  // flyout host to go back to display:none (it is never removed from the DOM).
  await page.mouse.move(900, 950);
  await page.locator('.primary-nav-menu__flyout--detached').waitFor({ state: 'hidden', timeout: 5000 }).catch(() => {});

  // Expand the resolved ancestor chain so the flyout parent renders. Collapsed children
  // are not in the DOM at all (CrestPanelMenuItems renders them only when expanded).
  // clickForEffect covers the prerendered-inert-button race.
  const expandLink = label =>
    primaryNavMenu.locator(`button.crest-panel-menu__item-link:has(.crest-panel-menu__text-rail:text-is("${label}"))`).first();
  const itemContent = label =>
    primaryNavMenu.locator(`.crest-panel-menu__item-content:has(.crest-panel-menu__text-rail:text-is("${label}"))`).first();
  await clickForEffect(expandLink(plan.chain[0]), itemContent(plan.chain[1]));
  await clickForEffect(expandLink(plan.chain[1]), itemContent(parentText));
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

  } finally {
    if (plan.build) {
      // Put the moved branch back at the root where it started, whatever happened above.
      await moveNode(plan.build.nodeId, null, plan.build.restoreIndex).catch(() => {});
    }
  }
};
