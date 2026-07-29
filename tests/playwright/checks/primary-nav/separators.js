// Converted from OrchardCore.Crest/tests/playwright/admin-menu-separators.js.
// Adding a separator renders a visible themed line, left-aligns its handle/text/line, can
// be drag-reordered above another entry, and can be deleted back to the original count.
// (The original script's CLEANUP_EXTRA_SEPARATORS maintenance mode was dev-only tooling,
// not a test, and was dropped in this conversion.)
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/AdminMenus`, { waitUntil: 'domcontentloaded' });
  await page.getByRole('heading', { name: 'Primary Navigation', exact: true }).waitFor({ timeout: 20000 });

  const initialCount = await page.locator('.admin-menu-separator').count();
  await page.getByRole('button', { name: /\+ Add/i }).click();
  await page.getByRole('button', { name: /Separator/i }).click();
  await page.waitForFunction(count => document.querySelectorAll('.admin-menu-separator').length > count, initialCount, { timeout: 20000 });

  const separator = page.locator('.admin-menu-separator').last();
  const lineColor = await separator.locator('.admin-menu-separator__line').evaluate(element => getComputedStyle(element).backgroundColor);

  const separatorLayout = await separator.evaluate(element => {
    const handle = element.querySelector('.admin-menu-node__handle');
    const text = Array.from(element.querySelectorAll('*')).find(child => child.textContent?.trim() === 'Separator');
    const line = element.querySelector('.admin-menu-separator__line');
    return {
      hasHandle: Boolean(handle),
      handleLeft: handle?.getBoundingClientRect().left ?? 0,
      textLeft: text?.getBoundingClientRect().left ?? 0,
      lineLeft: line?.getBoundingClientRect().left ?? 0,
    };
  });

  let dragReorderWorked = false;
  try {
    await page.evaluate(() => {
      const source = document.querySelector('.admin-menu-tree__list--root > .admin-menu-tree__item[data-entry-type="separator"]');
      const handle = source?.querySelector('.admin-menu-node__handle');
      const target = document.querySelector('.admin-menu-tree__list--root > .admin-menu-tree__item[data-entry-type="node"]');
      if (!source || !handle || !target) throw new Error('Could not resolve separator drag source or target geometry.');

      const dataTransfer = new DataTransfer();
      const targetRect = target.getBoundingClientRect();
      const eventInit = { bubbles: true, cancelable: true, dataTransfer, clientX: targetRect.left + targetRect.width / 2, clientY: targetRect.top + 3 };

      handle.dispatchEvent(new DragEvent('dragstart', { ...eventInit, clientY: handle.getBoundingClientRect().top + 3 }));
      target.dispatchEvent(new DragEvent('dragover', eventInit));
      target.dispatchEvent(new DragEvent('drop', eventInit));
      handle.dispatchEvent(new DragEvent('dragend', eventInit));
    });
    await page.waitForFunction(
      () => document.querySelector('.admin-menu-tree__list--root > .admin-menu-tree__item')?.dataset.entryType === 'separator',
      null,
      { timeout: 20000 },
    );
    dragReorderWorked = true;
  } catch {
    dragReorderWorked = false;
  }

  let finalCount = await page.locator('.admin-menu-separator').count();
  if (dragReorderWorked) {
    await page.locator('.admin-menu-tree__list--root > .admin-menu-tree__item[data-entry-type="separator"]').first().locator('button').click();
    await page.waitForFunction(count => document.querySelectorAll('.admin-menu-separator').length === count, initialCount, { timeout: 20000 });
    finalCount = await page.locator('.admin-menu-separator').count();
  }

  return [
    { name: 'separator-has-visible-line-color', pass: Boolean(lineColor) && lineColor !== 'rgba(0, 0, 0, 0)', message: `lineColor=${lineColor}` },
    {
      name: 'separator-row-left-aligned',
      pass: separatorLayout.hasHandle && separatorLayout.handleLeft < separatorLayout.textLeft && separatorLayout.textLeft < separatorLayout.lineLeft,
      message: JSON.stringify(separatorLayout),
    },
    { name: 'separator-drag-reorder-works', pass: dragReorderWorked, message: `dragReorderWorked=${dragReorderWorked}` },
    { name: 'separator-delete-restores-count', pass: finalCount === initialCount, message: `initialCount=${initialCount} finalCount=${finalCount}` },
  ];
};
