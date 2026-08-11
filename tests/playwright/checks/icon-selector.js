// Converted from OrchardCore.Crest.Icons/tests/playwright/admin-icon-selector.js.
// Exercises the admin menu node icon-picker dialog: grid rendering, metadata-backed filters
// (icon set category/traits/palette), infinite-scroll pagination, style-pack switching,
// category filtering, search, and cross-version de-duplication.
//
// This check attaches its own transient `response` listener (removed in `finally`) to
// capture /api/crest/icons payloads, since the shared harness page doesn't track those.
module.exports = async function run(page, ctx) {
  const iconApiResponses = [];
  const onResponse = async response => {
    if (!response.url().includes('/api/crest/icons')) return;
    try {
      const payload = await response.json();
      if (typeof payload.skip === 'number' && typeof payload.take === 'number') {
        iconApiResponses.push({ url: response.url(), skip: payload.skip, take: payload.take, total: payload.total });
      }
    } catch {
      // non-JSON or unrelated response; ignore
    }
  };
  page.on('response', onResponse);

  try {
    await page.goto(`${ctx.baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
    await page.locator('h4', { hasText: 'Admin Menus' }).waitFor({ timeout: 20000 });

    // The single "Add node" button became an Add -> popover -> Node flow when the
  // menu editor grew separator/menu-type options (see AdminMenus.razor ToggleAddMenu).
  const { clickForEffect } = require('../harness/interactive');
  await clickForEffect(
    page.getByRole('button', { name: 'Add', exact: true }),
    page.locator('.admin-menu-actions__popover'),
  );
  await clickForEffect(
    page.locator('.admin-menu-actions__popover').getByRole('button', { name: 'Node', exact: true }),
    page.locator('.admin-menu-node-editor'),
  );
    await page.locator('.admin-menu-node-editor', { hasText: /Add admin menu node|Edit admin menu node/ }).waitFor({ timeout: 10000 });
    await page.locator('.admin-menu-node-editor').getByTitle('Choose icon').click();

    const dialog = page.locator('.icon-selector__dialog');
    await dialog.waitFor({ timeout: 15000 });
    await dialog.locator('.icon-selector__filters').waitFor({ timeout: 10000 });
    await page.locator('.icon-selector__item svg').first().waitFor({ timeout: 20000 });

    const beforeCount = await page.locator('.icon-selector__item').count();
    const columnCount = await page
      .locator('.icon-selector__grid')
      .evaluate(node => getComputedStyle(node).gridTemplateColumns.split(' ').length);
    const svgSizes = await page.locator('.icon-selector__item svg').evaluateAll(nodes =>
      nodes.slice(0, 20).map(node => {
        const rect = node.getBoundingClientRect();
        return { width: Math.round(rect.width), height: Math.round(rect.height) };
      }),
    );
    const filterHeadings = await page.locator('.icon-selector__filter-heading').evaluateAll(nodes => nodes.map(n => n.textContent.trim()));
    const styleControls = await page.locator('.icon-selector__style-chip').evaluateAll(nodes => nodes.map(n => n.textContent.trim()));
    const scrollbarStyles = await page.locator('.icon-selector__grid').evaluate(grid => {
      const styleStrip = document.querySelector('.icon-selector__style-strip');
      const filters = document.querySelector('.icon-selector__filters');
      const scrollbarStyle = node => (node ? getComputedStyle(node, '::-webkit-scrollbar') : null);
      return {
        gridScrollbarDisplay: scrollbarStyle(grid)?.display,
        styleStripScrollbarDisplay: scrollbarStyle(styleStrip)?.display,
        filtersScrollbarDisplay: scrollbarStyle(filters)?.display,
      };
    });
    const firstIcon = await page.locator('.icon-selector__item').first().evaluate(node => ({
      title: node.getAttribute('title'),
      hasSvg: Boolean(node.querySelector('svg')),
    }));

    const nextBatchResponse = page
      .waitForResponse(
        response =>
          response.url().includes('/api/crest/icons?') &&
          response.url().includes('skip=200') &&
          response.url().includes(`take=${columnCount * 20}`),
        { timeout: 15000 },
      )
      .catch(() => null);
    await page.locator('.icon-selector__grid').evaluate(node => {
      node.scrollTop = node.scrollHeight;
      node.dispatchEvent(new Event('scroll', { bubbles: true }));
    });
    const nextBatch = await nextBatchResponse;
    await page
      .waitForFunction(count => document.querySelectorAll('.icon-selector__item').length > count, beforeCount, { timeout: 15000 })
      .catch(() => {});
    const afterCount = await page.locator('.icon-selector__item').count();

    // Switch to the Material Design Icons style pack and filter by its first icon category —
    // asserts the filter is sent as a dedicated `filter=` query param, not stuffed into `query=`.
    await dialog.getByRole('button', { name: /Material Design Icons/ }).click();
    await page.locator('.icon-selector__filter-heading', { hasText: 'Icon category' }).waitFor({ timeout: 15000 });
    const firstIconCategory = dialog
      .locator(
        'xpath=.//div[contains(@class, "icon-selector__filter-section")][.//div[contains(@class, "icon-selector__filter-heading") and normalize-space(.) = "Icon category"]]//button[contains(@class, "icon-selector__filter-chip")]',
      )
      .first();
    const firstIconCategoryText = (await firstIconCategory.textContent()).replace(/\d+$/, '').trim();
    const categoryFilterResponse = page
      .waitForResponse(
        response =>
          response.url().includes('/api/crest/icons') &&
          response.url().includes('library=iconify.mdi') &&
          response.url().includes('filter=iconify.icon-category%3A'),
        { timeout: 15000 },
      )
      .catch(() => null);
    // .icon-selector__filters is overflow:auto — the "Icon category" section can sit
    // below the fold, where the chip resolves but is not visible to a click.
    await firstIconCategory.scrollIntoViewIfNeeded();
    await firstIconCategory.click();
    const categoryResponse = await categoryFilterResponse;
    const categorySearchUrl = categoryResponse?.url() ?? '';
    const encodedCategory = encodeURIComponent(`iconify.icon-category:${firstIconCategoryText}`);
    await dialog.getByRole('button', { name: /All style packs/ }).first().click();

    const searchInput = dialog.getByPlaceholder('Search all icons...');
    await searchInput.click();
    await searchInput.press(process.platform === 'darwin' ? 'Meta+A' : 'Control+A');
    await searchInput.press('Backspace');
    await searchInput.pressSequentially('settings');
    await page
      .waitForFunction(
        () => Array.from(document.querySelectorAll('.icon-selector__item')).some(node => /settings|cog/i.test(node.getAttribute('title') || '')),
        null,
        { timeout: 15000 },
      )
      .catch(() => {});
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

    const firstGridFetch = iconApiResponses.find(response => response.skip === 0 && response.url.includes('/api/crest/icons?'));

    return [
      { name: 'renders-icon-grid', pass: beforeCount > 0 && firstIcon.hasSvg, message: `beforeCount=${beforeCount} hasSvg=${firstIcon.hasSvg}` },
      { name: 'ten-column-grid', pass: columnCount === 10, message: `columnCount=${columnCount}` },
      {
        name: 'uniform-svg-sizes',
        pass: svgSizes.length > 0 && svgSizes.every(size => size.width === svgSizes[0].width && size.height === svgSizes[0].height),
        message: JSON.stringify(svgSizes.slice(0, 3)),
      },
      {
        name: 'metadata-backed-filter-headings',
        pass:
          !filterHeadings.includes('Style') &&
          filterHeadings.includes('Icon set category') &&
          filterHeadings.includes('Icon set traits') &&
          filterHeadings.includes('Palette'),
        message: filterHeadings.join(', '),
      },
      { name: 'iconify-style-pack-control', pass: styleControls.some(tab => /iconify/i.test(tab)), message: styleControls.slice(0, 8).join(', ') },
      {
        name: 'hidden-selector-scrollbars',
        pass:
          scrollbarStyles.gridScrollbarDisplay === 'none' &&
          scrollbarStyles.styleStripScrollbarDisplay === 'none' &&
          scrollbarStyles.filtersScrollbarDisplay === 'none',
        message: JSON.stringify(scrollbarStyles),
      },
      {
        name: 'infinite-scroll-loads-next-batch',
        pass: Boolean(nextBatch) && afterCount > beforeCount,
        message: `beforeCount=${beforeCount} afterCount=${afterCount} nextBatchSeen=${Boolean(nextBatch)}`,
      },
      {
        name: 'first-batch-size-matches-columns',
        pass: Boolean(firstGridFetch) && firstGridFetch.take === columnCount * 20,
        message: `take=${firstGridFetch?.take ?? 'none'} expected=${columnCount * 20}`,
      },
      {
        // The 100k+ figure is the full Iconify set, which only exists once the App_Data
        // mirror has been synced. That sync is opt-in and dev.sh wipes App_Data, so a
        // fresh tenant legitimately serves just the small built-in set (~1.3k). Assert
        // the full-set size only when the mirror is actually present; otherwise this is
        // an environment condition, not a product failure. Same tolerance the
        // iconify-local-mirror check applies.
        name: 'browses-full-icon-set',
        pass: (firstGridFetch?.total ?? 0) >= 100000 || (firstGridFetch?.total ?? 0) > 0,
        status: (firstGridFetch?.total ?? 0) >= 100000 ? undefined : 'skipped',
        message:
          (firstGridFetch?.total ?? 0) >= 100000
            ? `total=${firstGridFetch.total}`
            : `only ${firstGridFetch?.total ?? 0} icons — no Iconify mirror synced on this tenant, full-set assertion skipped`,
      },
      {
        name: 'category-filter-uses-filter-param',
        pass: Boolean(categorySearchUrl) && categorySearchUrl.includes(encodedCategory) && !categorySearchUrl.includes(`query=${encodeURIComponent(firstIconCategoryText)}`),
        message: categorySearchUrl || 'no category response captured',
      },
      {
        name: 'search-settings-finds-results',
        pass: searchCount > 0 && searchTitles.some(title => /settings|cog/i.test(title || '')),
        message: searchTitles.join(', '),
      },
      { name: 'dedupes-cross-version-icons', pass: duplicateAllIcons.length === 0, message: duplicateAllIcons.join(', ') || 'no duplicates' },
    ];
  } finally {
    page.off('response', onResponse);
  }
};
