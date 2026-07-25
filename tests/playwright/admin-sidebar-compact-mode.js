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

async function main() {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    await login(page);
    await page.goto(`${baseUrl}/Admin/Settings/SecurityHeaders`, { waitUntil: 'domcontentloaded' });
    await page.locator('[data-testid="security-headers-page"]').waitFor({ timeout: 20000 });

    const sidebar = page.locator('[data-testid="admin-menu-sidebar"]');
    const toggle = page.getByRole('button', { name: 'Collapse navigation' });
    await sidebar.waitFor({ timeout: 20000 });
    await toggle.click();
    await page.locator('.admin-dashboard__main').hover();
    await page.waitForTimeout(240);

    await page.locator('[data-testid="admin-menu-sidebar"].admin-menu-sidebar--compact').waitFor({ timeout: 5000 });
    const compact = await sidebar.evaluate(element => ({
      width: element.getBoundingClientRect().width,
      textOpacity: getComputedStyle(element.querySelector('.crest-panel-menu__text-rail')).opacity,
      textWidth: element.querySelector('.crest-panel-menu__text-rail')?.getBoundingClientRect().width || 0,
      iconRailWidth: element.querySelector('.crest-panel-menu__icon-rail')?.getBoundingClientRect().width || 0,
      scrollable: getComputedStyle(element.querySelector('.admin-menu-sidebar__menu')).overflowY,
      scrollbarWidth: getComputedStyle(element.querySelector('.admin-menu-sidebar__menu')).scrollbarWidth,
    }));
    if (compact.width > 80 || compact.textOpacity !== '0' || compact.textWidth > 1 || Math.abs(compact.iconRailWidth - 64) > 1 || compact.scrollable !== 'auto' || compact.scrollbarWidth !== 'none') {
      throw new Error(`Compact sidebar styling is incorrect: ${JSON.stringify(compact)}`);
    }

    const activeStyle = await sidebar.locator('.crest-panel-menu__item-link--active').first().evaluate(element => {
      const icon = element.querySelector('.crest-panel-menu__icon-rail');
      const styles = getComputedStyle(element);
      const iconStyles = getComputedStyle(icon);
      return {
        backgroundColor: styles.backgroundColor,
        color: styles.color,
        iconColor: iconStyles.color,
      };
    });
    if (activeStyle.backgroundColor === 'rgba(0, 0, 0, 0)' || activeStyle.iconColor === activeStyle.color) {
      throw new Error(`Active sidebar item did not apply accent background/icon color: ${JSON.stringify(activeStyle)}`);
    }

    const autoSeparators = await sidebar.evaluate(element =>
      [...element.querySelectorAll('.crest-panel-menu__item--auto-separator')]
        .map(item => ({
          level0: item.classList.contains('crest-panel-menu__item--level-0'),
          level1: item.classList.contains('crest-panel-menu__item--level-1'),
          lineWidth: item.querySelector('.crest-panel-menu__separator-line')?.getBoundingClientRect().width || 0,
        }))
    );
    if (autoSeparators.length === 0 || autoSeparators.some(separator => !separator.level0 || separator.level1 || separator.lineWidth <= 0)) {
      throw new Error(`Generated tier separators were not rendered only for tier 1: ${JSON.stringify(autoSeparators.slice(0, 8))}`);
    }

    const expandableItem = sidebar.locator('.crest-panel-menu__item-link[aria-expanded]').first();
    await expandableItem.waitFor({ timeout: 10000 });
    if (await expandableItem.getAttribute('aria-expanded') !== 'true') {
      await expandableItem.click();
    }

    const expandedChevron = await expandableItem.locator('.crest-panel-menu__expand-icon').evaluate(element =>
      getComputedStyle(element).transform
    );
    if (expandedChevron === 'none') {
      throw new Error('Expanded panel menu chevron did not rotate.');
    }

    const collapsedChildren = await sidebar.locator('.crest-panel-menu__children--collapsed').first().evaluate(element => ({
      rows: getComputedStyle(element).gridTemplateRows,
      inert: element.hasAttribute('inert'),
      hidden: element.getAttribute('aria-hidden'),
    }));
    if (!collapsedChildren.inert || collapsedChildren.hidden !== 'true') {
      throw new Error(`Collapsed panel menu children are not mounted safely for animation: ${JSON.stringify(collapsedChildren)}`);
    }

    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.locator('[data-testid="admin-menu-sidebar"].admin-menu-sidebar--compact').waitFor({ timeout: 20000 });

    const visibleNestedIcons = await sidebar.evaluate(element =>
      [...element.querySelectorAll('.admin-menu-sidebar__item--level-1, .admin-menu-sidebar__item--level-2, .admin-menu-sidebar__item--level-3')]
        .filter(item => item.getClientRects().length > 0)
        .map(item => {
          const icon = item.querySelector('.admin-menu-sidebar__icon');
          const rect = icon?.getBoundingClientRect();
          return {
            label: item.querySelector('.crest-panel-menu__text-rail')?.textContent?.trim(),
            level: [...item.classList].find(name => name.startsWith('admin-menu-sidebar__item--level-')),
            width: rect?.width || 0,
            center: (rect?.top || 0) + (rect?.height || 0) / 2,
            rowHeight: item.querySelector('.crest-panel-menu__item-link')?.getBoundingClientRect().height || 0,
          };
        }));
    if (!visibleNestedIcons.length || visibleNestedIcons.some(icon => icon.width === 0)) {
      throw new Error(`Expanded nested menu items lost their icons in compact mode: ${JSON.stringify(visibleNestedIcons)}`);
    }

    const mainBeforeHover = await page.locator('.admin-dashboard__main').boundingBox();
    const compactTopLevelIconCenter = await sidebar.locator('.admin-menu-sidebar__item--level-0 .admin-menu-sidebar__icon').first().evaluate(element => {
      const rect = element.getBoundingClientRect();
      return rect.left + rect.width / 2;
    });
    const compactMenuScrollTop = await sidebar.locator('.admin-menu-sidebar__menu').evaluate(element => element.scrollTop);
    const compactSidebarBox = await sidebar.boundingBox();
    const compactToggleBox = await page.getByRole('button', { name: 'Keep navigation expanded' }).boundingBox();
    if (Math.abs((compactSidebarBox.x + compactSidebarBox.width) - compactToggleBox.x) > 1) {
      throw new Error(`Compact mode toggle is detached from the sidebar: ${JSON.stringify({ compactSidebarBox, compactToggleBox })}`);
    }

    await sidebar.hover();
    await page.waitForTimeout(400);
    const preDelayWidth = await sidebar.locator('.admin-menu-sidebar__content').evaluate(element => element.getBoundingClientRect().width);
    if (preDelayWidth > 80) {
      throw new Error(`Compact sidebar opened before its one-second hover delay elapsed: ${preDelayWidth}`);
    }

    await page.waitForTimeout(1050);
    const transitionTopLevelIconCenter = await sidebar.locator('.admin-menu-sidebar__item--level-0 .admin-menu-sidebar__icon').first().evaluate(element => {
      const rect = element.getBoundingClientRect();
      return rect.left + rect.width / 2;
    });
    await page.waitForTimeout(380);
    const hover = await sidebar.locator('.admin-menu-sidebar__content').evaluate(element => ({
      width: element.getBoundingClientRect().width,
      textOpacity: getComputedStyle(element.querySelector('.crest-panel-menu__text-rail')).opacity,
      textWidth: element.querySelector('.crest-panel-menu__text-rail')?.getBoundingClientRect().width || 0,
      iconRailWidth: element.querySelector('.crest-panel-menu__icon-rail')?.getBoundingClientRect().width || 0,
    }));
    const mainAfterHover = await page.locator('.admin-dashboard__main').boundingBox();
    const hoverContentBox = await sidebar.locator('.admin-menu-sidebar__content').boundingBox();
    const hoverToggleBox = await page.getByRole('button', { name: 'Keep navigation expanded' }).boundingBox();
    const hoverTopLevelIconCenter = await sidebar.locator('.admin-menu-sidebar__item--level-0 .admin-menu-sidebar__icon').first().evaluate(element => {
      const rect = element.getBoundingClientRect();
      return rect.left + rect.width / 2;
    });
    const hoverMenuScrollTop = await sidebar.locator('.admin-menu-sidebar__menu').evaluate(element => element.scrollTop);
    const expandedNestedIcons = await sidebar.evaluate(element =>
      [...element.querySelectorAll('.admin-menu-sidebar__item--level-1, .admin-menu-sidebar__item--level-2, .admin-menu-sidebar__item--level-3')]
        .filter(item => item.getClientRects().length > 0)
        .map(item => {
          const icon = item.querySelector('.admin-menu-sidebar__icon');
          const rect = icon?.getBoundingClientRect();
          return {
            label: item.querySelector('.crest-panel-menu__text-rail')?.textContent?.trim(),
            center: (rect?.top || 0) + (rect?.height || 0) / 2,
            rowHeight: item.querySelector('.crest-panel-menu__item-link')?.getBoundingClientRect().height || 0,
          };
        }));
    if (visibleNestedIcons.some(icon => Math.abs(icon.rowHeight - 40) > 1) || expandedNestedIcons.some(icon => Math.abs(icon.rowHeight - 40) > 1)) {
      throw new Error(`Nested icon rows do not keep the shared 40px vertical geometry: ${JSON.stringify({ compact: visibleNestedIcons.slice(0, 5), expanded: expandedNestedIcons.slice(0, 5), compactMenuScrollTop, hoverMenuScrollTop })}`);
    }
    if (hover.width < 250 || hover.textOpacity !== '1' || hover.textWidth < 100 || Math.abs(hover.iconRailWidth - 64) > 1 || mainBeforeHover.x !== mainAfterHover.x || Math.abs((hoverContentBox.x + hoverContentBox.width) - hoverToggleBox.x) > 1 || Math.abs(compactTopLevelIconCenter - transitionTopLevelIconCenter) > 1 || Math.abs(compactTopLevelIconCenter - hoverTopLevelIconCenter) > 1) {
      throw new Error(`Compact sidebar did not overlay-expand correctly: ${JSON.stringify({ hover, mainBeforeHover, mainAfterHover, hoverContentBox, hoverToggleBox, compactTopLevelIconCenter, transitionTopLevelIconCenter, hoverTopLevelIconCenter })}`);
    }

    await page.getByRole('button', { name: 'Keep navigation expanded' }).click();
    await page.locator('[data-testid="admin-menu-sidebar"].admin-menu-sidebar--pinned').waitFor({ timeout: 5000 });
    console.log(JSON.stringify({ compactWidth: compact.width, nestedIcons: visibleNestedIcons.length, hoverWidth: hover.width, mode: 'pinned' }));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
