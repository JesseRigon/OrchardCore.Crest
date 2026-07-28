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
    const page = await browser.newPage({ viewport: { width: 1366, height: 900 } });
    await login(page);
    await page.goto(`${baseUrl}/Admin`, { waitUntil: 'domcontentloaded' });

    const primaryNavMenu = page.locator('[data-testid="primary-nav-menu"]');
    await primaryNavMenu.waitFor({ timeout: 20000 });

    const placeholders = primaryNavMenu.locator('.primary-nav-menu__icon-placeholder--dot');
    const placeholderCount = await placeholders.count();
    // Closed branches are intentionally not mounted. Validate the dot when an
    // iconless item is currently visible without requiring hidden branches to
    // participate in the rendered DOM.
    const placeholderStyle = placeholderCount === 0 ? null : await placeholders.first().evaluate(element => {
      const rect = element.getBoundingClientRect();
      const style = getComputedStyle(element);
      return { width: rect.width, height: rect.height, border: style.border, borderRadius: style.borderRadius };
    });
    if (placeholderStyle && (placeholderStyle.width <= 0 || placeholderStyle.height <= 0 || placeholderStyle.border === '0px none rgb(0, 0, 0)')) {
      throw new Error(`Default iconless-menu dot is not visible: ${JSON.stringify(placeholderStyle)}`);
    }

    const quickAddButton = primaryNavMenu.locator('.primary-nav-menu__quickadd-toggle, .primary-nav-menu__quickadd-row').first();
    await quickAddButton.waitFor({ timeout: 10000 });
    await quickAddButton.click();

    const popover = primaryNavMenu.locator('.primary-nav-menu__quickadd-popover');
    await popover.waitFor({ timeout: 10000 });

    const popoverBox = await popover.boundingBox();
    const primaryNavMenuBox = await primaryNavMenu.boundingBox();
    const header = popover.locator('.primary-nav-menu__quickadd-header');
    const closeButton = popover.locator('.primary-nav-menu__quickadd-close-button');
    await header.waitFor({ timeout: 10000 });
    await closeButton.waitFor({ timeout: 10000 });
    if (!popoverBox || !primaryNavMenuBox ||
        Math.abs(popoverBox.x - primaryNavMenuBox.x) > 1 ||
        Math.abs(popoverBox.y - primaryNavMenuBox.y) > 1 ||
        Math.abs(popoverBox.width - primaryNavMenuBox.width) > 1 ||
        Math.abs(popoverBox.height - primaryNavMenuBox.height) > 1) {
      const popoverDiagnostic = await popover.evaluate(element => ({
        html: element.outerHTML.slice(0, 1000),
        position: getComputedStyle(element).position,
        inset: getComputedStyle(element).inset,
        display: getComputedStyle(element).display,
        parent: element.parentElement ? {
          html: element.parentElement.outerHTML.slice(0, 300),
          position: getComputedStyle(element.parentElement).position,
          width: getComputedStyle(element.parentElement).width,
          height: getComputedStyle(element.parentElement).height,
        } : null,
      }));
      throw new Error(`Quick Add must replace only the primaryNavMenu bounds: ${JSON.stringify({ popoverBox, primaryNavMenuBox, popoverDiagnostic })}`);
    }

    await page.mouse.move(640, 320);
    await popover.waitFor({ state: 'detached', timeout: 10000 });

    const collapseToggle = page.getByRole('button', { name: 'Collapse navigation' });
    await collapseToggle.click();
    await page.locator('[data-testid="primary-nav-menu"].primary-nav-menu--compact').waitFor({ timeout: 10000 });

    const compactQuickAddButton = primaryNavMenu.locator('.primary-nav-menu__quickadd-toggle, .primary-nav-menu__quickadd-row').first();
    await compactQuickAddButton.click();
    const compactPopover = primaryNavMenu.locator('.primary-nav-menu__quickadd-popover');
    await compactPopover.waitFor({ timeout: 10000 });
    const compactPopoverBox = await compactPopover.boundingBox();
    const compactPrimaryNavMenuBox = await primaryNavMenu.boundingBox();
    if (!compactPopoverBox || !compactPrimaryNavMenuBox || compactPopoverBox.width < 250 || compactPopoverBox.width <= compactPrimaryNavMenuBox.width) {
      throw new Error(`Compact Quick Add did not enter the expanded overlay state: ${JSON.stringify({ compactPopoverBox, compactPrimaryNavMenuBox })}`);
    }

    await page.mouse.move(640, 320);
    await compactPopover.waitFor({ state: 'detached', timeout: 10000 });
    await page.locator('[data-testid="primary-nav-menu"].primary-nav-menu--compact:not(.primary-nav-menu--expanded)').waitFor({ timeout: 10000 });

    console.log(JSON.stringify({ quickAddAutoClose: 'ok', primaryNavMenuWidth: primaryNavMenuBox.width, placeholderStyle, compactQuickAddWidth: compactPopoverBox.width }));
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
