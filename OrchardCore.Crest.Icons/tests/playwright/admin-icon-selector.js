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
    await page.press('#Password', 'Enter');
    await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

async function main() {
  const browser = await chromium.launch({ headless: !headed });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  const iconApiUrls = [];
  const iconSearchResponses = [];

  page.on('console', message => console.log(`[browser:${message.type()}] ${message.text()}`));
  page.on('pageerror', error => console.log(`[browser:error] ${error.message}`));
  page.on('response', async response => {
    if (!response.url().includes('/api/crest/icons')) return;
    iconApiUrls.push(response.url());
    try {
      const payload = await response.json();
      console.log(`[icons-api] status=${response.status()} skip=${payload.skip} take=${payload.take} total=${payload.total} items=${payload.items?.length ?? 0}`);
      if (typeof payload.skip === 'number' && typeof payload.take === 'number') {
        iconSearchResponses.push({ url: response.url(), skip: payload.skip, take: payload.take, total: payload.total, items: payload.items?.length ?? 0 });
      }
    } catch {
      console.log(`[icons-api] status=${response.status()} non-json`);
    }
  });

  await login(page);
  await page.goto(`${baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
  await page.locator('h4', { hasText: 'Admin Menus' }).waitFor({ timeout: 20000 });

  await page.getByRole('button', { name: /add node/i }).click();
  await page.locator('.admin-menu-node-editor', { hasText: /Add admin menu node|Edit admin menu node/ }).waitFor({ timeout: 10000 });
  await page.locator('.admin-menu-node-editor').getByTitle('Choose icon').click();

  const dialog = page.locator('.icon-selector__dialog');
  await dialog.waitFor({ timeout: 15000 });
  await dialog.locator('.icon-selector__filters').waitFor({ timeout: 10000 });
  await page.locator('.icon-selector__item svg').first().waitFor({ timeout: 20000 });

  const beforeCount = await page.locator('.icon-selector__item').count();
  const columnCount = await page.locator('.icon-selector__grid').evaluate(node => getComputedStyle(node).gridTemplateColumns.split(' ').length);
  const svgSizes = await page.locator('.icon-selector__item svg').evaluateAll(nodes => nodes.slice(0, 20).map(node => {
    const rect = node.getBoundingClientRect();
    return { width: Math.round(rect.width), height: Math.round(rect.height) };
  }));
  const filterHeadings = await page.locator('.icon-selector__filter-heading').evaluateAll(nodes => nodes.map(n => n.textContent.trim()));
  const styleControls = await page.locator('.icon-selector__style-chip').evaluateAll(nodes => nodes.map(n => n.textContent.trim()));
  const selectorScrollbarStyles = await page.locator('.icon-selector__grid').evaluate(grid => {
    const styleStrip = document.querySelector('.icon-selector__style-strip');
    const filters = document.querySelector('.icon-selector__filters');
    const scrollbarStyle = node => node ? getComputedStyle(node, '::-webkit-scrollbar') : null;
    const gridScrollbar = scrollbarStyle(grid);
    const styleStripScrollbar = scrollbarStyle(styleStrip);
    const filtersScrollbar = scrollbarStyle(filters);
    return {
      gridScrollbarDisplay: gridScrollbar?.display,
      gridScrollbarWidth: gridScrollbar?.width,
      styleStripScrollbarDisplay: styleStripScrollbar?.display,
      styleStripScrollbarHeight: styleStripScrollbar?.height,
      filtersScrollbarDisplay: filtersScrollbar?.display,
      filtersScrollbarWidth: filtersScrollbar?.width,
    };
  });
  const firstIcon = await page.locator('.icon-selector__item').first().evaluate(node => ({
    title: node.getAttribute('title'),
    name: node.querySelector('.icon-selector__name')?.textContent?.trim(),
    hasSvg: !!node.querySelector('svg')
  }));

  const nextBatchResponse = page.waitForResponse(response =>
    response.url().includes('/api/crest/icons?') &&
    response.url().includes('skip=200') &&
    response.url().includes(`take=${columnCount * 20}`), { timeout: 15000 });
  await page.locator('.icon-selector__grid').evaluate(node => {
    node.scrollTop = node.scrollHeight;
    node.dispatchEvent(new Event('scroll', { bubbles: true }));
  });
  await nextBatchResponse;
  await page.waitForFunction(count => document.querySelectorAll('.icon-selector__item').length > count, beforeCount, { timeout: 15000 });
  const afterCount = await page.locator('.icon-selector__item').count();

  if (filterHeadings.includes('Style') || !filterHeadings.includes('Icon set category') || !filterHeadings.includes('Icon set traits') || !filterHeadings.includes('Palette')) {
    throw new Error(`Expected metadata-backed modal filter headings, got: ${filterHeadings.join(', ')}`);
  }

  await dialog.getByRole('button', { name: /Material Design Icons/ }).click();
  await page.locator('.icon-selector__filter-heading', { hasText: 'Icon category' }).waitFor({ timeout: 15000 });
  const firstIconCategory = dialog
    .locator('xpath=.//div[contains(@class, "icon-selector__filter-section")][.//div[contains(@class, "icon-selector__filter-heading") and normalize-space(.) = "Icon category"]]//button[contains(@class, "icon-selector__filter-chip")]')
    .first();
  const firstIconCategoryText = (await firstIconCategory.textContent()).replace(/\d+$/, '').trim();
  const categoryFilterResponse = page.waitForResponse(response =>
    response.url().includes('/api/crest/icons') &&
    response.url().includes('library=iconify.mdi') &&
    response.url().includes('filter=iconify.icon-category%3A'), { timeout: 15000 });
  await firstIconCategory.click();
  await categoryFilterResponse;
  const categorySearchUrl = iconApiUrls.find(url => url.includes('filter=iconify.icon-category%3A'));
  const encodedCategory = encodeURIComponent(`iconify.icon-category:${firstIconCategoryText}`);
  if (!categorySearchUrl || !categorySearchUrl.includes(encodedCategory) || categorySearchUrl.includes(`query=${encodeURIComponent(firstIconCategoryText)}`)) {
    throw new Error(`Expected metadata filter query parameter without mutating search text, got: ${categorySearchUrl}`);
  }
  await dialog.getByRole('button', { name: /All style packs/ }).first().click();

  const searchInput = dialog.getByPlaceholder('Search all icons...');
  await searchInput.click();
  await searchInput.press(process.platform === 'darwin' ? 'Meta+A' : 'Control+A');
  await searchInput.press('Backspace');
  await searchInput.pressSequentially('settings');
  await page.waitForFunction(() => Array.from(document.querySelectorAll('.icon-selector__item')).some(node => /settings|cog/i.test(node.getAttribute('title') || '')), null, { timeout: 15000 });
  const searchCount = await page.locator('.icon-selector__item').count();
  const searchTitles = await page.locator('.icon-selector__item').evaluateAll(nodes => nodes.slice(0, 8).map(n => n.getAttribute('title')));
  await searchInput.click();
  await searchInput.press(process.platform === 'darwin' ? 'Meta+A' : 'Control+A');
  await searchInput.press('Backspace');
  await searchInput.pressSequentially('address');
  await page.waitForTimeout(800);
  const duplicateAllIcons = await page.locator('.icon-selector__item').evaluateAll(nodes => {
    const keys = nodes.map(node => {
      const title = node.getAttribute('title') || '';
      const name = node.querySelector('.icon-selector__name')?.textContent?.trim() || '';
      return `${title}|${name}`.toLowerCase();
    });
    return keys.filter((key, index) => keys.indexOf(key) !== index);
  });

  console.log(JSON.stringify({ filterHeadings, styleControls: styleControls.slice(0, 8), selectorScrollbarStyles, beforeCount, afterCount, columnCount, svgSizes: svgSizes.slice(0, 5), firstIcon, searchCount, searchTitles, duplicateAllIcons }, null, 2));

  if (!styleControls.some(tab => /iconify/i.test(tab))) {
    throw new Error(`Expected Iconify style-pack control, got: ${styleControls.join(', ')}`);
  }
  if (beforeCount === 0 || !firstIcon.hasSvg) {
    throw new Error('Expected icon grid with SVG previews.');
  }
  if (afterCount <= beforeCount) {
    throw new Error(`Expected scrolling near the bottom to load the next icon batch, before=${beforeCount}, after=${afterCount}.`);
  }
  if (columnCount !== 10) {
    throw new Error(`Expected 10 icon grid columns on desktop dialog, got ${columnCount}.`);
  }
  const firstGridFetch = iconSearchResponses.find(response => response.skip === 0 && response.url.includes('/api/crest/icons?'));
  if (!firstGridFetch || firstGridFetch.take !== columnCount * 20) {
    throw new Error(`Expected first icon selector batch size to be columns * 20 (${columnCount * 20}), got ${firstGridFetch?.take ?? 'none'}.`);
  }
  if ((firstGridFetch.total ?? 0) < 100000) {
    throw new Error(`Expected unfiltered default icon selector results to browse all icons, not a curated list. Total was ${firstGridFetch.total}.`);
  }
  if (selectorScrollbarStyles.gridScrollbarDisplay !== 'none' || selectorScrollbarStyles.styleStripScrollbarDisplay !== 'none' || selectorScrollbarStyles.filtersScrollbarDisplay !== 'none') {
    throw new Error(`Expected icon selector scrollbars to be hidden, got: ${JSON.stringify(selectorScrollbarStyles)}`);
  }
  if (!svgSizes.every(size => size.width === svgSizes[0].width && size.height === svgSizes[0].height)) {
    throw new Error(`Expected uniform SVG preview sizes, got: ${JSON.stringify(svgSizes)}`);
  }
  if (searchCount === 0 || !searchTitles.some(title => /settings|cog/i.test(title || ''))) {
    throw new Error(`Expected settings search results, got: ${searchTitles.join(', ')}`);
  }
  if (duplicateAllIcons.length > 0) {
    throw new Error(`Expected All icon search to dedupe cross-version icons, got duplicate keys: ${duplicateAllIcons.join(', ')}`);
  }

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Icons.
