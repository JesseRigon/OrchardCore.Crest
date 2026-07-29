// Converted from OrchardCore.Crest.Admin/tests/playwright/admin-menu-editor-collapsed.js.
// Admin menu tree nodes with children should default to collapsed.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
  await page.locator('h4', { hasText: 'Admin Menus' }).waitFor({ timeout: 20000 });
  await page.locator('.admin-menu-tree').waitFor({ timeout: 20000 });

  const state = await page.locator('.admin-menu-tree').evaluate(root => {
    const parentButtons = Array.from(root.querySelectorAll('.admin-menu-node__collapse'));
    const nestedLists = Array.from(root.querySelectorAll('.admin-menu-tree__list .admin-menu-tree__list'));
    return {
      parentButtonCount: parentButtons.length,
      expandedButtonCount: parentButtons.filter(button => button.textContent?.includes('expand_more')).length,
      visibleNestedListCount: nestedLists.filter(list => {
        const rect = list.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
      }).length,
    };
  });

  return [
    { name: 'has-parent-nodes', pass: state.parentButtonCount > 0, message: `parentButtonCount=${state.parentButtonCount}` },
    {
      name: 'defaults-collapsed',
      pass: state.expandedButtonCount === 0 && state.visibleNestedListCount === 0,
      message: JSON.stringify(state),
    },
  ];
};
