// Converted from OrchardCore.Crest/tests/playwright/admin-menu-new-branch-editable.js.
// The synthetic "New" node is fixed-position (locked/non-draggable) but must still be
// editable — its edit button opens the node editor with the expected fields.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/AdminMenus`, { waitUntil: 'domcontentloaded' });
  await page.getByRole('heading', { name: 'Sidebar', exact: true }).waitFor({ timeout: 20000 });

  const newNode = page.locator('.admin-menu-tree__item[data-node-id="new"]').first();
  await newNode.waitFor({ timeout: 10000 });

  const fixed = await newNode.evaluate(element => ({
    locked: element.getAttribute('data-locked'),
    draggable: element.querySelector('.admin-menu-node__handle')?.getAttribute('draggable'),
  }));

  // clickForEffect covers the prerendered-inert-button race — a raw DOM click can
  // land before the interactive runtime attaches handlers (harness/interactive.js).
  const { clickForEffect } = require('../../harness/interactive');
  let editButtonUsable = false;
  try {
    const editButton = newNode.locator('button', { hasText: 'edit' }).first();
    if (await editButton.isDisabled()) throw new Error('New node edit button is disabled.');
    await clickForEffect(editButton, page.locator('.admin-menu-node-editor'));
    editButtonUsable = true;
  } catch {
    editButtonUsable = false;
  }

  let editorFieldsPresent = false;
  if (editButtonUsable) {
    await page.locator('.admin-menu-node-editor').waitFor({ timeout: 10000 });
    await page.locator('.admin-menu-node-editor input[name="Text"]').waitFor({ timeout: 10000 });
    await page.locator('.admin-menu-node-editor .icon-selector').waitFor({ timeout: 10000 });
    editorFieldsPresent = true;
  }

  return [
    {
      name: 'new-node-fixed-position',
      pass: fixed.locked === 'true' && fixed.draggable === 'false',
      message: JSON.stringify(fixed),
    },
    { name: 'new-node-edit-button-usable', pass: editButtonUsable, message: `editButtonUsable=${editButtonUsable}` },
    { name: 'new-node-editor-opens-with-fields', pass: editorFieldsPresent, message: `editorFieldsPresent=${editorFieldsPresent}` },
  ];
};
