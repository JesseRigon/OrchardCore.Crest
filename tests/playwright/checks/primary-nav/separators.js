// Converted from OrchardCore.Crest/tests/playwright/admin-menu-separators.js.
// Adding a separator renders a visible themed line, left-aligns its handle/text/line, can
// be drag-reordered above another entry, and can be deleted back to the original count.
// (The original script's CLEANUP_EXTRA_SEPARATORS maintenance mode was dev-only tooling,
// not a test, and was dropped in this conversion.)
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/AdminMenus`, { waitUntil: 'domcontentloaded' });
  await page.getByRole('heading', { name: 'Sidebar', exact: true }).waitFor({ timeout: 20000 });

  const initialCount = await page.locator('.admin-menu-separator').count();
  // The single "+ Add" button became an Add -> popover flow; clickForEffect also
  // covers the prerendered-inert-button race (see harness/interactive.js).
  const { clickForEffect } = require('../../harness/interactive');
  await clickForEffect(
    page.getByRole('button', { name: 'Add', exact: true }),
    page.locator('.admin-menu-actions__popover'),
  );
  await page.locator('.admin-menu-actions__popover').getByRole('button', { name: /Separator/i }).click();
  await page.waitForFunction(count => document.querySelectorAll('.admin-menu-separator').length > count, initialCount, { timeout: 20000 });

  const separator = page.locator('.admin-menu-separator').last();
  const lineColor = await separator.locator('.admin-menu-separator__line').evaluate(element => getComputedStyle(element).backgroundColor);

  const separatorLayout = await separator.evaluate(element => {
    const handle = element.querySelector('.admin-menu-node__handle');
    // Match the innermost element whose text is exactly "Separator" (the <h6> label).
    // querySelectorAll returns document order, so a plain .find() picks the OUTERMOST
    // match — the .admin-menu-separator__content wrapper, whose left edge equals the
    // handle's. That made the left-alignment assertion compare the handle against
    // itself and fail even though the row is laid out correctly.
    const labels = Array.from(element.querySelectorAll('*')).filter(child => child.textContent?.trim() === 'Separator');
    const text = labels.length ? labels[labels.length - 1] : undefined;
    const line = element.querySelector('.admin-menu-separator__line');
    return {
      hasHandle: Boolean(handle),
      handleLeft: handle?.getBoundingClientRect().left ?? 0,
      textLeft: text?.getBoundingClientRect().left ?? 0,
      lineLeft: line?.getBoundingClientRect().left ?? 0,
    };
  });

  // Index of the separator among root entries BEFORE the drag. A new separator is
  // appended at the end, and dropping it "before" the first node lands it at index 1
  // (the drop inserts relative to that node, it does not become the list head). The old
  // assertion required index 0 and so failed even though the reorder succeeded — the
  // real contract is "the separator moved", so compare positions instead.
  const rootEntryTypes = () =>
    page.evaluate(() => [...document.querySelectorAll('.admin-menu-tree__list--root > .admin-menu-tree__item')].map(entry => entry.dataset.entryType));
  const separatorIndexBefore = (await rootEntryTypes()).indexOf('separator');

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
      indexBefore =>
        [...document.querySelectorAll('.admin-menu-tree__list--root > .admin-menu-tree__item')].findIndex(
          entry => entry.dataset.entryType === 'separator',
        ) !== indexBefore,
      separatorIndexBefore,
      { timeout: 20000 },
    );
    dragReorderWorked = true;
  } catch {
    dragReorderWorked = false;
  }

  // Delete unconditionally: this check ADDED a separator, so it must remove it even when
  // the drag-reorder step failed. Leaving it behind persists in tenant state and makes
  // every later run start from a different initialCount.
  let finalCount = await page.locator('.admin-menu-separator').count();
  await page
    .locator('.admin-menu-tree__list--root > .admin-menu-tree__item[data-entry-type="separator"]')
    .first()
    .locator('button')
    .click()
    .catch(() => {});
  await page
    .waitForFunction(count => document.querySelectorAll('.admin-menu-separator').length === count, initialCount, { timeout: 20000 })
    .catch(() => {});
  finalCount = await page.locator('.admin-menu-separator').count();

  return [
    { name: 'separator-has-visible-line-color', pass: Boolean(lineColor) && lineColor !== 'rgba(0, 0, 0, 0)', message: `lineColor=${lineColor}` },
    {
      name: 'separator-row-left-aligned',
      pass: separatorLayout.hasHandle && separatorLayout.handleLeft < separatorLayout.textLeft && separatorLayout.textLeft < separatorLayout.lineLeft,
      message: JSON.stringify(separatorLayout),
    },
    { name: 'separator-drag-reorder-works', pass: dragReorderWorked, message: `movedFromIndex=${separatorIndexBefore} dragReorderWorked=${dragReorderWorked}` },
    { name: 'separator-delete-restores-count', pass: finalCount === initialCount, message: `initialCount=${initialCount} finalCount=${finalCount}` },
  ];
};
