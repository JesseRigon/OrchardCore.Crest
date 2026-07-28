// Playwright probe owned by OrchardCore.Crest.Admin.
// Validates the Blazor-managed Admin Menu editor against the Crest design-token path.
const { chromium } = require('playwright');
const fs = require('fs/promises');
const path = require('path');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const headed = process.env.HEADED === '1';
const captureScreenshot = process.env.CAPTURE_SCREENSHOT === '1';
const outputDir = process.env.OUTPUT_DIR || 'modules/OrchardCore.Crest/OrchardCore.Crest.Admin/tests/playwright/output';

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
  const consoleMessages = [];
  page.on('console', message => consoleMessages.push(`[${message.type()}] ${message.text()}`));
  page.on('pageerror', error => consoleMessages.push(`[pageerror] ${error.message}`));

  try {
    await login(page);
    await page.goto(`${baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });

    await page.locator('.admin-shell').waitFor({ timeout: 20000 });
    await page.getByRole('heading', { name: 'Admin Menus', exact: true }).waitFor({ timeout: 20000 });
    await page.locator('.admin-menu-tree').waitFor({ timeout: 20000 });

    const iframeCount = await page.locator('iframe').count();
    if (iframeCount !== 0) {
      throw new Error(`Admin Menu editor must be a Blazor page, but rendered ${iframeCount} iframe(s).`);
    }

    const loadedStyles = await page.evaluate(() => Array.from(document.querySelectorAll('link[rel="stylesheet"]'))
      .map(link => link.getAttribute('href') || ''));
    const requiredStyles = [
      '/CrestAdmin.DesignSystem.Default.css',
      '/CrestAdmin.css',
      '/OrchardCore.Crest.Admin.styles.css'
    ];
    const missingStyles = requiredStyles.filter(required => !loadedStyles.some(href => href.endsWith(required)));
    if (missingStyles.length) {
      throw new Error(`Missing Admin design/scoped stylesheet(s): ${missingStyles.join(', ')}. Loaded: ${JSON.stringify(loadedStyles)}`);
    }

    const initialSelected = page.locator('.admin-menu-list-item--selected').first();
    await initialSelected.waitFor({ timeout: 10000 });
    const initialDisplay = await initialSelected.evaluate(element => getComputedStyle(element).display);
    if (initialDisplay !== 'flex') {
      throw new Error(`AdminMenus scoped CSS did not apply. Expected selected menu display flex, got ${initialDisplay}.`);
    }

    await page.evaluate(() => {
      const root = document.querySelector('.admin-shell') || document.documentElement;
      root.style.setProperty('--crest-color-accent-1', 'rgb(190, 20, 120)');
      root.style.setProperty('--crest-color-active-surface-1', 'rgba(190, 20, 120, 0.25)');
      root.style.setProperty('--crest-color-surface-1', 'rgb(250, 252, 240)');
      root.style.setProperty('--crest-color-border-1', 'rgb(18, 52, 86)');
      root.style.setProperty('--crest-radius-sm', '18px');
    });

    const selectedMetrics = await initialSelected.evaluate(element => {
      const style = getComputedStyle(element);
      return {
        borderTopColor: style.borderTopColor,
        backgroundColor: style.backgroundColor,
        borderTopLeftRadius: style.borderTopLeftRadius
      };
    });

    if (selectedMetrics.borderTopColor !== 'rgb(190, 20, 120)') {
      throw new Error(`Selected Admin Menu item did not consume --crest-color-accent-1: ${JSON.stringify(selectedMetrics)}`);
    }

    if (selectedMetrics.backgroundColor !== 'rgba(190, 20, 120, 0.25)') {
      throw new Error(`Selected Admin Menu item did not consume --crest-color-active-surface-1: ${JSON.stringify(selectedMetrics)}`);
    }

    if (selectedMetrics.borderTopLeftRadius !== '18px') {
      throw new Error(`Admin Menu item did not consume --crest-radius-sm: ${JSON.stringify(selectedMetrics)}`);
    }

    const settingsButton = page.locator('button[title="PrimaryNavMenu settings"]').first();
    if (await settingsButton.count()) {
      await settingsButton.click();
      const settingsFlyout = page.locator('.admin-menu-settings').first();
      await settingsFlyout.waitFor({ timeout: 10000 });
      const settingsMetrics = await settingsFlyout.evaluate(element => {
        const style = getComputedStyle(element);
        return {
          backgroundColor: style.backgroundColor,
          borderTopColor: style.borderTopColor,
          borderTopLeftRadius: style.borderTopLeftRadius
        };
      });

      if (settingsMetrics.backgroundColor !== 'rgb(250, 252, 240)' ||
          settingsMetrics.borderTopColor !== 'rgb(18, 52, 86)' ||
          settingsMetrics.borderTopLeftRadius !== '18px') {
        throw new Error(`PrimaryNavMenu settings flyout did not consume Crest tokens: ${JSON.stringify(settingsMetrics)}`);
      }
    }

    if (captureScreenshot) {
      await fs.mkdir(outputDir, { recursive: true });
      await page.screenshot({ path: path.join(outputDir, 'admin-menu-design-system.png'), fullPage: true });
    }

    const severeConsole = consoleMessages.filter(line => /\[(error|pageerror)\]/i.test(line) &&
      !/favicon|Failed to load resource.*404|browser refresh server|WebSocket connection to .*localhost/i.test(line));
    if (severeConsole.length) {
      throw new Error(`Unexpected browser errors:\n${severeConsole.join('\n')}`);
    }

    console.log(JSON.stringify({ loadedStyles, selectedMetrics }, null, 2));
  } catch (error) {
    console.log(`current url: ${page.url()}`);
    console.log(`body preview: ${(await page.locator('body').innerText().catch(() => '')).slice(0, 1800)}`);
    console.log(`console messages:\n${consoleMessages.join('\n')}`);
    throw error;
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
