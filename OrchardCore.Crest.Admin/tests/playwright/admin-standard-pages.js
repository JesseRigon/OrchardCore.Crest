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
    await page.locator('button:has-text("Login")').click();
    await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

async function validatePage(page, route, expected) {
  const consoleMessages = [];
  page.on('console', message => consoleMessages.push(`[${message.type()}] ${message.text()}`));
  page.on('pageerror', error => consoleMessages.push(`[pageerror] ${error.message}`));

  await page.goto(`${baseUrl}${route}`, { waitUntil: 'networkidle' });

  try {
    await page.locator('.admin-shell').waitFor({ timeout: 20000 });
    await page.locator('.primary-nav-menu').waitFor({ timeout: 20000 });
    await page.locator('.admin-dashboard__main').waitFor({ timeout: 20000 });
    await page.getByRole('heading', { name: expected.heading, exact: true, level: 4 }).waitFor({ timeout: 20000 });
    for (const text of expected.texts) {
      await page.getByText(text, { exact: false }).first().waitFor({ timeout: 10000 });
    }
  } catch (error) {
    console.log(`route: ${route}`);
    console.log(`current url: ${page.url()}`);
    console.log(`title: ${await page.title().catch(() => '')}`);
    console.log(`body preview: ${(await page.locator('body').innerText().catch(() => '')).slice(0, 1200)}`);
    console.log(`console messages:\n${consoleMessages.join('\n')}`);
    throw error;
  }

  const result = await page.evaluate(() => ({
    shellCount: document.querySelectorAll('.admin-shell').length,
    primaryNavMenuVisible: !!document.querySelector('.primary-nav-menu') && getComputedStyle(document.querySelector('.primary-nav-menu')).display !== 'none',
    mainText: document.querySelector('.admin-dashboard__main')?.innerText?.replace(/\s+/g, ' ').trim().slice(0, 500) || '',
  }));

  console.log(`${route}: ${JSON.stringify(result)}`);

  if (result.shellCount !== 1 || !result.primaryNavMenuVisible) {
    throw new Error(`Expected visible Crest admin shell for ${route}.`);
  }

  const severeConsole = consoleMessages.filter(line => /\[(error|pageerror)\]/i.test(line) && !/favicon|Failed to load resource.*404|browser refresh server|WebSocket connection to .*localhost/i.test(line));
  if (severeConsole.length) {
    throw new Error(`Unexpected browser errors on ${route}:\n${severeConsole.join('\n')}`);
  }
}

async function main() {
  const browser = await chromium.launch({ headless: !headed });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });

  await login(page);
  await validatePage(page, '/Admin/Menus', {
    heading: 'Menus',
    texts: ['Manage standard Orchard site menus', 'Refresh'],
  });
  await validatePage(page, '/Admin/Settings/admin', {
    heading: 'Admin Settings',
    texts: ['Admin', 'Site Map', 'Enable theme toggler', 'Enable Admin Menu filter', 'Display New menu', 'Display titles in top bar'],
  });
  await page.getByRole('tab', { name: /Site Map/i }).click();
  try {
    await page.getByRole('heading', { name: 'Site Map', exact: true, level: 5 }).waitFor({ timeout: 10000 });
    await page.getByText('Route access management placeholder', { exact: false }).waitFor({ timeout: 10000 });
    const siteMapTree = await page.evaluate(() => ({
      dragIcons: [...document.querySelectorAll('.admin-menu-node__handle')].filter(handle => handle.textContent.trim() === 'drag_indicator').length,
      editButtons: document.querySelectorAll('.admin-menu-node button[title*="Edit"]').length,
      toggleButtons: document.querySelectorAll('.admin-menu-node button[title*="Toggle"]').length,
      deleteButtons: document.querySelectorAll('.admin-menu-node button[title*="Delete"]').length,
    }));
    if (siteMapTree.dragIcons < 1 || siteMapTree.editButtons < 1 || siteMapTree.toggleButtons < 1 || siteMapTree.deleteButtons < 1) {
      throw new Error(`Expected site map drag indicators and action buttons, got ${JSON.stringify(siteMapTree)}`);
    }
  } catch (error) {
    console.log(`Site Map click body preview: ${(await page.locator('body').innerText().catch(() => '')).slice(0, 1500)}`);
    throw error;
  }
  console.log('/Admin/Settings/admin Site Map tab: visible with tree actions');

  await validatePage(page, '/Admin/Settings/general', {
    heading: 'General Settings',
    texts: ['General', 'Resources', 'Cache', 'Site name'],
  });
  await page.getByRole('tab', { name: /Resources/i }).click();
  await page.getByText('Use resources cache busting', { exact: false }).waitFor({ timeout: 10000 });
  await page.getByText('Resource Debug Mode', { exact: false }).waitFor({ timeout: 10000 });
  await page.getByRole('tab', { name: /Cache/i }).click();
  await page.getByText('Cache Mode', { exact: false }).waitFor({ timeout: 10000 });
  console.log('/Admin/Settings/general Resources and Cache tabs: visible');

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
