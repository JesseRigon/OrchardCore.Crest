const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const headed = process.env.HEADED === '1';
const parentText = process.env.FLYOUT_PARENT || 'Queries';
const childText = process.env.FLYOUT_CHILD || 'All Queries';

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
  await page.goto(`${baseUrl}/Admin`, { waitUntil: 'networkidle' });
  const primaryNavMenu = page.locator('.primary-nav-menu');
  await primaryNavMenu.waitFor({ timeout: 20000 });

  for (const label of ['Platform', 'Search']) {
    await primaryNavMenu.evaluate((root, label) => {
      const textOf = element => (element.querySelector('.primary-nav-menu__item-content')?.textContent || '').replace(/\s+/g, ' ').trim();
      const item = Array.from(root.querySelectorAll('.primary-nav-menu__item'))
        .find(item => textOf(item) === label && !item.closest('.rz-expander.rz-state-collapsed'));
      if (item?.getAttribute('aria-expanded') !== 'true') {
        item?.querySelector(':scope > .rz-expander > .rz-navigation-item-wrapper > .rz-navigation-item-link, :scope > .rz-navigation-item-wrapper > .rz-navigation-item-link')?.click();
      }
    }, label);
    await page.waitForTimeout(250);
  }

  const before = await primaryNavMenu.evaluate((root, { parentText, childText }) => {
    const textOf = element => (element.querySelector('.primary-nav-menu__item-content')?.textContent || '').replace(/\s+/g, ' ').trim();
    const isReachable = element => !element.closest('.rz-expander.rz-state-collapsed');
    const parent = Array.from(root.querySelectorAll('.primary-nav-menu__item')).find(item => textOf(item) === parentText && isReachable(item));
    if (!parent) return { found: false };
    const parentBox = parent.getBoundingClientRect();
    const inlineFlyout = parent.querySelector('.primary-nav-menu__flyout');
    const detachedFlyout = root.parentElement?.querySelector(':scope > .primary-nav-menu__flyout--detached');
    const inlineChildren = Array.from(parent.querySelectorAll(':scope > .rz-expander > .rz-expander-content .primary-nav-menu__item, :scope > .rz-navigation-menu .primary-nav-menu__item'))
      .map(item => textOf(item))
      .filter(Boolean);

    return {
      found: true,
      className: parent.className,
      parentBox: { left: parentBox.left, right: parentBox.right, top: parentBox.top, width: parentBox.width },
      hasInlineFlyout: !!inlineFlyout,
      hasDetachedFlyout: !!detachedFlyout,
      inlineChildren,
      childInInline: inlineChildren.includes(childText)
    };
  }, { parentText, childText });

  console.log('before hover:', JSON.stringify(before, null, 2));

  if (!before.found) {
    throw new Error(`Could not find primaryNavMenu item "${parentText}".`);
  }

  if (before.hasInlineFlyout) {
    throw new Error(`Expected "${parentText}" not to render its flyout inside the primaryNavMenu item.`);
  }

  if (before.hasDetachedFlyout) {
    throw new Error('Expected detached primaryNavMenu flyout to be absent before hover.');
  }

  if (before.childInInline) {
    throw new Error(`Expected "${childText}" not to render as an inline stable-primaryNavMenu child of "${parentText}".`);
  }

  await primaryNavMenu.evaluate((root, { parentText }) => {
    const textOf = element => (element.querySelector('.primary-nav-menu__item-content')?.textContent || '').replace(/\s+/g, ' ').trim();
    const isReachable = element => !element.closest('.rz-expander.rz-state-collapsed');
    root.querySelectorAll('[data-primaryNavMenu-flyout-target]').forEach(element => element.removeAttribute('data-primaryNavMenu-flyout-target'));
    const parent = Array.from(root.querySelectorAll('.primary-nav-menu__item')).find(item => textOf(item) === parentText && isReachable(item));
    parent.setAttribute('data-primaryNavMenu-flyout-target', 'true');
    parent.scrollIntoView({ block: 'center', inline: 'nearest' });
  }, { parentText });
  await page.locator('[data-primaryNavMenu-flyout-target="true"] .primary-nav-menu__item-content').hover();
  await page.waitForTimeout(250);

  const after = await primaryNavMenu.evaluate((root, { parentText }) => {
    const textOf = element => (element.querySelector('.primary-nav-menu__item-content')?.textContent || '').replace(/\s+/g, ' ').trim();
    const isReachable = element => !element.closest('.rz-expander.rz-state-collapsed');
    const parent = Array.from(root.querySelectorAll('.primary-nav-menu__item')).find(item => textOf(item) === parentText && isReachable(item));
    const parentBox = parent.getBoundingClientRect();
    const flyout = root.parentElement?.querySelector(':scope > .primary-nav-menu__flyout--detached');
    const flyoutStyle = getComputedStyle(flyout);
    const flyoutBox = flyout.getBoundingClientRect();
    const hover = Array.from(document.querySelectorAll(':hover')).map(element => element.className || element.tagName).slice(-8);
    const flyoutCenterX = flyoutBox.left + flyoutBox.width / 2;
    const flyoutCenterY = flyoutBox.top + Math.min(flyoutBox.height / 2, 20);
    const pointElement = document.elementFromPoint(flyoutCenterX, flyoutCenterY);
    return {
      flyoutDisplay: flyoutStyle.display,
      flyoutPosition: flyoutStyle.position,
      parentBox: { left: parentBox.left, right: parentBox.right, top: parentBox.top, width: parentBox.width, height: parentBox.height },
      flyoutBox: { left: flyoutBox.left, right: flyoutBox.right, top: flyoutBox.top, width: flyoutBox.width },
      hover,
      pointElement: pointElement ? { tagName: pointElement.tagName, className: pointElement.className, text: pointElement.textContent?.trim().slice(0, 80) } : null,
      flyoutCenter: { x: flyoutCenterX, y: flyoutCenterY },
      flyoutHit: !!pointElement?.closest?.('.primary-nav-menu__flyout')
    };
  }, { parentText });

  console.log('after hover:', JSON.stringify(after, null, 2));

  if (after.flyoutDisplay === 'none') {
    throw new Error('Expected flyout to become visible on hover.');
  }

  if (after.flyoutPosition !== 'fixed') {
    throw new Error(`Expected detached flyout popup to use fixed positioning, got ${after.flyoutPosition}.`);
  }

  if (after.flyoutBox.left < after.parentBox.left + after.parentBox.width * 0.75) {
    throw new Error(`Expected flyout popup to render as a right-side popup instead of an inline submenu. parent=${JSON.stringify(after.parentBox)} flyout=${JSON.stringify(after.flyoutBox)}`);
  }

  if (!after.flyoutHit) {
    throw new Error(`Expected flyout to be visible at its screen position, but elementFromPoint saw ${JSON.stringify(after.pointElement)} at ${JSON.stringify(after.flyoutCenter)}.`);
  }

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
