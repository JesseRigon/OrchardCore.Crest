// Converted from OrchardCore.Crest.Admin/tests/playwright/admin-menu-node-editor-dialog.js.
// The add/edit node editor must be an inline panel under the clicked node, not a modal
// Radzen dialog, and must pre-populate existing values when editing.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
  await page.locator('h4', { hasText: 'Admin Menus' }).waitFor({ timeout: 20000 });
  await page.locator('.admin-menu-tree').waitFor({ timeout: 20000 });

  // The single "Add node" button became an Add -> popover -> Node flow when the
  // menu editor grew separator/menu-type options (see AdminMenus.razor ToggleAddMenu).
  const { clickForEffect } = require('../../harness/interactive');
  await clickForEffect(
    page.getByRole('button', { name: 'Add', exact: true }),
    page.locator('.admin-menu-actions__popover'),
  );
  // Plain click, NOT clickForEffect: by this point the popover is already interactive, and
  // clickForEffect's retry is actively harmful here — the first click opens the editor and
  // dismisses the popover, so a retry lands on nothing (or re-toggles) and the editor is
  // gone by the time the effect is rechecked.
  await page.locator('.admin-menu-actions__popover').getByRole('button', { name: 'Node', exact: true }).click();
  const addEditor = page.locator('.admin-menu-node-editor').first();
  await addEditor.waitFor({ timeout: 10000 });
  const addText = await addEditor.innerText();
  const dialogCountAfterAdd = await page.locator('.rz-dialog').count();
  await addEditor.getByRole('button', { name: /cancel/i }).first().click();

  await page.locator('.admin-menu-node').filter({ hasText: 'Content' }).first().waitFor({ timeout: 10000 });
  await page.evaluate(() => {
    const node = Array.from(document.querySelectorAll('.admin-menu-node')).find(element =>
      /(^|\n)Content(\n|$)/.test((element instanceof HTMLElement ? element.innerText : element.textContent) || ''),
    );
    // Select the edit button by its icon label, not by position. The node's buttons are
    // [chevron_right, horizontal_rule, edit, visibility_off, delete], so index 1 was the
    // add-separator button — clicking it never opened the edit editor.
    const buttons = Array.from(node?.querySelectorAll('button') || []);
    const button = buttons.find(candidate => (candidate.textContent || '').trim() === 'edit');
    if (!(button instanceof HTMLElement)) {
      throw new Error(`Content edit button not found among: ${buttons.map(x => (x.textContent || '').trim()).join(', ')}`);
    }
    button.click();
  });
  const contentNode = page.locator('.admin-menu-node').filter({ hasText: 'Content' }).first();
  const editEditor = page.locator('.admin-menu-node-editor').first();
  await editEditor.waitFor({ timeout: 10000 });
  const editText = await editEditor.innerText();
  const iconInputValue = await editEditor.locator('.icon-selector input').inputValue();
  const editorTop = await editEditor.evaluate(node => node.getBoundingClientRect().top);
  const nodeTop = await contentNode.evaluate(node => node.getBoundingClientRect().top);

  return [
    { name: 'add-editor-inline-title', pass: /Add admin menu node/.test(addText), message: addText.slice(0, 100) },
    { name: 'add-editor-not-a-dialog', pass: dialogCountAfterAdd === 0, message: `dialogCount=${dialogCountAfterAdd}` },
    { name: 'edit-editor-shows-node-name', pass: /Edit admin menu node: Content/.test(editText), message: editText.slice(0, 100) },
    {
      // The /fa-/ pattern predates the Iconify migration — no admin menu node uses a Font
      // Awesome class any more. Icons are now either "@iconify:<prefix>:<name>" (the
      // recipe-seeded form, e.g. Content's "@iconify:mdi:folder") or the resolved
      // "iconify.<prefix>/<variant>/..." form. The property under test is simply that the
      // editor pre-populates the node's existing icon rather than opening blank.
      name: 'edit-editor-prepopulates-icon',
      pass: /^(@iconify:|iconify\.|fa-)/.test(iconInputValue.trim()),
      message: `iconInputValue=${iconInputValue}`,
    },
    { name: 'edit-editor-renders-under-node', pass: editorTop > nodeTop, message: `editorTop=${editorTop} nodeTop=${nodeTop}` },
  ];
};
