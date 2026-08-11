// Converted from OrchardCore.Crest.Admin/tests/playwright/admin-menu-new-node-locked.js.
// The synthetic "New" menu node (and all its children) must render move-locked: greyed,
// non-draggable, badged "Fixed", with delete and add-separator disabled. (The original
// asserted a "Locked" badge and ALL actions disabled — the product deliberately relaxed
// that: the New branch stays editable and hide/show-able, only move/delete/separator are
// locked. See AdminMenus.razor CanEditNode/IsMoveLockedNode.)
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
  await page.locator('.admin-shell').waitFor({ timeout: 20000 });
  await page.getByRole('heading', { name: 'Admin Menus', exact: true }).waitFor({ timeout: 20000 });
  await page.locator('.admin-menu-tree').waitFor({ timeout: 20000 });

  const state = await page.locator('.admin-menu-tree').evaluate(root => {
    const newItem = root.querySelector('li[data-node-id="new"]');
    if (!newItem) return { found: false };

    const newNode = newItem.querySelector(':scope > .admin-menu-node');
    const buttons = Array.from(newNode.querySelectorAll('button'));
    const actionButtons = buttons.filter(button => !button.classList.contains('admin-menu-node__collapse'));
    const childItems = Array.from(newItem.querySelectorAll(':scope > ol li[data-node-id]'));
    const lockedChildItems = childItems.filter(item => item.getAttribute('data-locked') === 'true');
    const handle = newNode.querySelector('.admin-menu-node__handle');

    return {
      found: true,
      locked: newItem.getAttribute('data-locked') === 'true',
      greyed: newNode.classList.contains('admin-menu-node--locked'),
      lockedBadge: newNode.textContent.includes('Fixed'),
      draggable: handle?.getAttribute('draggable'),
      actionButtonCount: actionButtons.length,
      disabledActionButtonCount: actionButtons.filter(button => button.disabled || button.getAttribute('aria-disabled') === 'true').length,
      childItemCount: childItems.length,
      lockedChildItemCount: lockedChildItems.length,
    };
  });

  return [
    { name: 'new-node-exists', pass: state.found, message: JSON.stringify(state) },
    {
      name: 'new-node-locked-and-greyed',
      pass: state.found && state.locked && state.greyed && state.lockedBadge && state.draggable === 'false',
      message: JSON.stringify(state),
    },
    {
      // Move-affecting actions (delete, add-separator) are disabled; edit and
      // hide/show intentionally stay enabled on the New branch.
      name: 'new-node-move-actions-disabled',
      pass: state.found && state.actionButtonCount > 0 && state.disabledActionButtonCount >= 2,
      message: JSON.stringify(state),
    },
    {
      name: 'new-node-children-locked',
      pass: state.found && state.lockedChildItemCount === state.childItemCount,
      message: JSON.stringify(state),
    },
  ];
};
