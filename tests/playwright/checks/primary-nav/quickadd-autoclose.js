// Converted from OrchardCore.Crest/tests/playwright/admin-primary-nav-menu-quickadd-autoclose.js.
// The Quick Add popover must exactly overlay the primaryNavMenu bounds (both expanded and
// compact), auto-close on outside pointer movement, and expand wider than the rail in
// compact mode. Also checks the default iconless-item placeholder dot renders visibly
// when present.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'domcontentloaded' });

  const primaryNavMenu = page.locator('[data-testid="primary-nav-menu"]');
  await primaryNavMenu.waitFor({ timeout: 20000 });

  const placeholders = primaryNavMenu.locator('.primary-nav-menu__icon-placeholder--dot');
  const placeholderCount = await placeholders.count();
  const placeholderStyle =
    placeholderCount === 0
      ? null
      : await placeholders.first().evaluate(element => {
          const rect = element.getBoundingClientRect();
          const style = getComputedStyle(element);
          return { width: rect.width, height: rect.height, border: style.border };
        });

  const quickAddButton = primaryNavMenu.locator('.primary-nav-menu__quickadd-toggle, .primary-nav-menu__quickadd-row').first();
  await quickAddButton.waitFor({ timeout: 10000 });
  await quickAddButton.click();
  const popover = primaryNavMenu.locator('.primary-nav-menu__quickadd-popover');
  await popover.waitFor({ timeout: 10000 });
  const popoverBox = await popover.boundingBox();
  const primaryNavMenuBox = await primaryNavMenu.boundingBox();
  await popover.locator('.primary-nav-menu__quickadd-header').waitFor({ timeout: 10000 });
  await popover.locator('.primary-nav-menu__quickadd-close-button').waitFor({ timeout: 10000 });

  const popoverOverlaysMenu =
    Boolean(popoverBox) &&
    Boolean(primaryNavMenuBox) &&
    Math.abs(popoverBox.x - primaryNavMenuBox.x) <= 1 &&
    Math.abs(popoverBox.y - primaryNavMenuBox.y) <= 1 &&
    Math.abs(popoverBox.width - primaryNavMenuBox.width) <= 1 &&
    Math.abs(popoverBox.height - primaryNavMenuBox.height) <= 1;

  await page.mouse.move(640, 320);
  await popover.waitFor({ state: 'detached', timeout: 10000 });

  await page.getByRole('button', { name: 'Collapse navigation' }).click();
  await page.locator('[data-testid="primary-nav-menu"].primary-nav-menu--compact').waitFor({ timeout: 10000 });

  const compactQuickAddButton = primaryNavMenu.locator('.primary-nav-menu__quickadd-toggle, .primary-nav-menu__quickadd-row').first();
  await compactQuickAddButton.click();
  const compactPopover = primaryNavMenu.locator('.primary-nav-menu__quickadd-popover');
  await compactPopover.waitFor({ timeout: 10000 });
  const compactPopoverBox = await compactPopover.boundingBox();
  const compactPrimaryNavMenuBox = await primaryNavMenu.boundingBox();
  const compactExpandsWider =
    Boolean(compactPopoverBox) && Boolean(compactPrimaryNavMenuBox) && compactPopoverBox.width >= 250 && compactPopoverBox.width > compactPrimaryNavMenuBox.width;

  await page.mouse.move(640, 320);
  await compactPopover.waitFor({ state: 'detached', timeout: 10000 });
  const stayedCompactAndClosed = await page
    .locator('[data-testid="primary-nav-menu"].primary-nav-menu--compact:not(.primary-nav-menu--expanded)')
    .count()
    .then(count => count > 0)
    .catch(() => false);

  return [
    {
      name: 'iconless-placeholder-dot-visible',
      pass: placeholderStyle === null || (placeholderStyle.width > 0 && placeholderStyle.height > 0 && placeholderStyle.border !== '0px none rgb(0, 0, 0)'),
      message: placeholderStyle ? JSON.stringify(placeholderStyle) : 'no placeholder currently rendered',
    },
    { name: 'quickadd-popover-overlays-expanded-menu', pass: popoverOverlaysMenu, message: JSON.stringify({ popoverBox, primaryNavMenuBox }) },
    { name: 'quickadd-popover-wider-in-compact-mode', pass: compactExpandsWider, message: JSON.stringify({ compactPopoverBox, compactPrimaryNavMenuBox }) },
    { name: 'quickadd-autocloses-and-stays-compact', pass: stayedCompactAndClosed, message: `stayedCompactAndClosed=${stayedCompactAndClosed}` },
  ];
};
