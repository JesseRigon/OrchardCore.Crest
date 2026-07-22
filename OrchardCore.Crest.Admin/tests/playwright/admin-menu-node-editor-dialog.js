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
  page.on('console', message => console.log(`[browser:${message.type()}] ${message.text()}`));
  page.on('pageerror', error => console.log(`[browser:error] ${error.message}`));

  await login(page);
  await page.goto(`${baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
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
    const node = Array.from(document.querySelectorAll('.admin-menu-node')).find(element => /(^|\n)Content(\n|$)/.test((element instanceof HTMLElement ? element.innerText : element.textContent) || ''));
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

  console.log(JSON.stringify({
    addHasTitle: /Add admin menu node/.test(addText),
    dialogCountAfterAdd,
    editTextPreview: editText.slice(0, 200),
    iconInputValue,
    editorTop,
    nodeTop
  }, null, 2));

  if (!/Add admin menu node/.test(addText)) {
    throw new Error('Expected inline Add admin menu node editor title.');
  }
  if (dialogCountAfterAdd !== 0) {
    throw new Error('Expected editor to be inline, not a Radzen dialog.');
  }
  if (!/Edit admin menu node: Content/.test(editText)) {
    throw new Error('Expected inline Edit admin menu node title with node name.');
  }
  if (!/fa-/.test(iconInputValue)) {
    throw new Error(`Expected existing Content icon to be pre-populated, got '${iconInputValue}'.`);
  }
  if (editorTop <= nodeTop) {
    throw new Error('Expected inline editor to render under the edited node.');
  }

  await browser.close();
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest.Admin.
