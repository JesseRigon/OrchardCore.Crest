const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function login(page) {
  await page.goto(`${baseUrl}/login`, { waitUntil: 'domcontentloaded' });
  await page.locator('input[name="UserName"]').waitFor({ timeout: 20000 });
  await page.locator('input[name="UserName"]').fill(username);
  await page.locator('input[name="Password"]').fill(password);
  await page.getByRole('button', { name: 'Login', exact: true }).click();
  await page.waitForURL(/\/admin/i, { timeout: 20000 });
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1366, height: 900 } });
    await login(page);
    await page.goto(`${baseUrl}/Admin/AdminMenus`, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: 'Sidebar Layout', exact: true }).waitFor({ timeout: 20000 });

    const newNode = page.locator('.admin-menu-tree__item[data-node-id="new"]').first();
    await newNode.waitFor({ timeout: 10000 });

    const fixed = await newNode.evaluate(element => ({
      locked: element.getAttribute('data-locked'),
      draggable: element.querySelector('.admin-menu-node__handle')?.getAttribute('draggable'),
    }));
    if (fixed.locked !== 'true' || fixed.draggable !== 'false') {
      throw new Error(`New node should be fixed-position: ${JSON.stringify(fixed)}`);
    }

    await newNode.evaluate(element => {
      const editButton = Array.from(element.querySelectorAll('button'))
        .find(button => button.textContent?.trim() === 'edit');
      if (!editButton || editButton.disabled) {
        throw new Error('New node edit button is missing or disabled.');
      }

      editButton.click();
    });

    await page.locator('.admin-menu-node-editor').waitFor({ timeout: 10000 });
    await page.locator('.admin-menu-node-editor input[name="Text"]').waitFor({ timeout: 10000 });
    await page.locator('.admin-menu-node-editor .icon-selector').waitFor({ timeout: 10000 });

    console.log(JSON.stringify({ newBranchEditable: 'ok' }));
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
