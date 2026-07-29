// Converted from OrchardCore.Crest.Admin/tests/playwright/admin-menu-node-editor-dialog.js.
// The add/edit node editor must be an inline panel under the clicked node, not a modal
// Radzen dialog, and must pre-populate existing values when editing.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
  await page.locator('h4', { hasText: 'Admin Menus' }).waitFor({ timeout: 20000 });
  await page.locator('.admin-menu-tree').waitFor({ timeout: 20000 });

  await page.getByRole('button', { name: /add node/i }).click();
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
    const button = node?.querySelectorAll('button')[1];
    if (!(button instanceof HTMLElement)) throw new Error('Content edit button not found.');
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
    { name: 'edit-editor-prepopulates-icon', pass: /fa-/.test(iconInputValue), message: `iconInputValue=${iconInputValue}` },
    { name: 'edit-editor-renders-under-node', pass: editorTop > nodeTop, message: `editorTop=${editorTop} nodeTop=${nodeTop}` },
  ];
};
