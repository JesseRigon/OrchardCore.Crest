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

async function main() {
  const browser = await chromium.launch({ headless: !headed });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  const consoleMessages = [];
  page.on('console', message => consoleMessages.push(`[${message.type()}] ${message.text()}`));
  page.on('pageerror', error => consoleMessages.push(`[pageerror] ${error.message}`));

  try {
    await login(page);
    await page.goto(`${baseUrl}/Admin/Features`, { waitUntil: 'networkidle' });
    await page.locator('[data-testid="features-page"]').waitFor({ timeout: 20000 });
    await page.getByRole('heading', { name: 'Features', exact: true, level: 4 }).waitFor({ timeout: 20000 });

    const cards = page.locator('[data-feature-id]');
    const initialCount = await cards.count();
    if (initialCount < 2) {
      throw new Error(`Expected all available Orchard features, got ${initialCount}.`);
    }

    const featureIds = await cards.evaluateAll(elements => elements.map(element => element.getAttribute('data-feature-id')));
    const targetId = featureIds.find(Boolean);
    const search = page.getByPlaceholder('Search name, ID, category, description, or dependency...');
    await search.fill(targetId);
    await search.press('Tab');
    await page.waitForTimeout(250);
    const filteredIds = await cards.evaluateAll(elements => elements.map(element => element.getAttribute('data-feature-id')));
    if (!filteredIds.includes(targetId) || filteredIds.length >= initialCount) {
      throw new Error(`Feature search did not filter by ID: ${JSON.stringify({ initialCount, targetId, filteredIds })}`);
    }

    await search.fill('');
    await search.press('Tab');
    await page.waitForTimeout(250);
    const categories = await page.locator('[data-testid="feature-category"]').count();
    const statusBadges = await page.getByText(/^(Enabled|Disabled)$/).count();
    if (categories < 1 || statusBadges < initialCount) {
      throw new Error(`Expected grouped features with status metadata: ${JSON.stringify({ categories, statusBadges, initialCount })}`);
    }

    const legacyFrame = await page.locator('iframe').count();
    if (legacyFrame) {
      throw new Error('Features route rendered the legacy iframe instead of the dedicated Blazor page.');
    }

    const severeConsole = consoleMessages.filter(line => /\[(error|pageerror)\]/i.test(line) && !/favicon|Failed to load resource.*404|browser refresh server|WebSocket connection to .*localhost/i.test(line));
    if (severeConsole.length) {
      throw new Error(`Unexpected browser errors:\n${severeConsole.join('\n')}`);
    }

    console.log(JSON.stringify({ initialCount, categories, targetId, filteredCount: filteredIds.length }, null, 2));
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
// Playwright probe owned by OrchardCore.Crest.Admin.
