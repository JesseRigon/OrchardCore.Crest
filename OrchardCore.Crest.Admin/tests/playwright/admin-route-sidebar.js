const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const headed = process.env.HEADED === '1';
const route = process.env.ADMIN_ROUTE || '/Admin/CRM/Customers';
const expectedActiveText = process.env.EXPECT_ACTIVE_TEXT || '';
const expectedInactiveText = process.env.EXPECT_INACTIVE_TEXT || '';

async function login(page) {
  await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
  if (await page.locator('#UserName').count()) {
    await page.fill('#UserName', username);
    await page.fill('#Password', password);
    await page.locator('button:has-text("Login")').click();
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
  await page.goto(`${baseUrl}${route}`, { waitUntil: 'networkidle' });

  try {
    await page.locator('.admin-shell').waitFor({ timeout: 15000 });
    await page.locator('.admin-menu-sidebar').waitFor({ timeout: 15000 });
    await page.locator('.admin-dashboard__main').waitFor({ timeout: 15000 });
  } catch (error) {
    console.log(`current url: ${page.url()}`);
    console.log(`body preview: ${(await page.locator('body').innerText().catch(() => '')).slice(0, 500)}`);
    throw error;
  }

  const shellCount = await page.locator('.admin-shell').count();
  const sidebarVisible = await page.locator('.admin-menu-sidebar').first().isVisible();
  const mainVisible = await page.locator('.admin-dashboard__main').first().isVisible();
  const bodyText = await page.locator('body').innerText();
  const sidebarIconDetails = await page.locator('.admin-menu-sidebar .admin-menu-sidebar__item-content').evaluateAll(items => items.map(item => {
    const text = item.textContent?.trim() || '';
    const icon = item.querySelector('.orchard-icon');
    const svgIcon = icon?.querySelector('svg');
    const style = icon ? getComputedStyle(icon) : null;
    return {
      text,
      hasOrchardIcon: !!icon,
      library: icon?.getAttribute('data-icon-library') || null,
      version: icon?.getAttribute('data-icon-version') || null,
      name: icon?.getAttribute('data-icon-name') || null,
      hasSvg: !!svgIcon,
      width: style?.width || null,
      height: style?.height || null,
      fontSize: style?.fontSize || null,
      opacity: style?.opacity || null,
      iconOuterHtml: icon?.outerHTML?.replace(/\s+/g, ' ').slice(0, 240) || null
    };
  }));

  console.log(`route: ${route}`);
  console.log(`current url after navigation: ${page.url()}`);
  console.log(`admin shell count: ${shellCount}`);
  console.log(`sidebar visible: ${sidebarVisible}`);
  console.log(`main visible: ${mainVisible}`);
  console.log(`body has Customers: ${bodyText.includes('Customers')}`);
  console.log(`sidebar icons: ${JSON.stringify(sidebarIconDetails.filter(item => /CRM|Customers|Content|Settings|Configuration|Menus/i.test(item.text)).slice(0, 12), null, 2)}`);

  const activeItems = await page.locator('.admin-menu-sidebar .admin-menu-sidebar__item-content--active .rz-navigation-item-text').evaluateAll(items => items.map(item => item.textContent?.trim()).filter(Boolean));
  const sidebarStateDetails = await page.locator('.admin-menu-sidebar .admin-menu-sidebar__item-content').evaluateAll(items => items.map(item => {
    const text = item.textContent?.trim() || '';
    const host = item.closest('.rz-navigation-item, .rz-panel-menu-item, li, div');
    const activeHost = item.closest('.admin-menu-sidebar__item-content--active');
    const radzenActiveHost = item.closest('.rz-state-active, .rz-navigation-item-active');
    return { text, itemClass: item.className || '', hostClass: host?.className || '', activeClass: activeHost?.className || '', radzenActiveClass: radzenActiveHost?.className || '' };
  }).filter(item => /Menus|Content Items|Admin Menus/.test(item.text)));
  console.log(`active sidebar items: ${JSON.stringify(activeItems)}`);
  console.log(`sidebar state details: ${JSON.stringify(sidebarStateDetails, null, 2)}`);

  if (expectedActiveText && !activeItems.includes(expectedActiveText)) {
    throw new Error(`Expected active sidebar item ${JSON.stringify(expectedActiveText)} for ${route}; active items were ${JSON.stringify(activeItems)}.`);
  }

  if (expectedInactiveText && activeItems.includes(expectedInactiveText)) {
    throw new Error(`Expected sidebar item ${JSON.stringify(expectedInactiveText)} not to be active for ${route}; active items were ${JSON.stringify(activeItems)}.`);
  }

  if (expectedInactiveText) {
    const inactiveStillHighlighted = sidebarStateDetails.some(item => item.text === expectedInactiveText && /wrapper-active|link-active|\bactive\b/i.test(`${item.hostClass} ${item.radzenActiveClass}`));
    if (inactiveStillHighlighted) {
      throw new Error(`Expected sidebar item ${JSON.stringify(expectedInactiveText)} to have Radzen active classes removed; state was ${JSON.stringify(sidebarStateDetails)}.`);
    }
  }

  if (shellCount !== 1 || !sidebarVisible || !mainVisible) {
    throw new Error(`Expected exactly one Crest admin shell with visible sidebar for ${route}.`);
  }

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
