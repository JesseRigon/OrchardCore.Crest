// Converted from OrchardCore.Crest.Admin/tests/playwright/admin-menu-layout-export.js.
// Exercises the "Export layout JSON" button on the built-in admin menu and verifies the
// server wrote a well-formed recipe file to disk.
const fs = require('fs');
const path = require('path');

const repoRoot = path.resolve(__dirname, '..', '..', '..', '..', '..', '..');
const exportFile = path.join(repoRoot, 'recipes', 'crest-admin-menu-layout.json');

module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/AdminMenus`, { waitUntil: 'networkidle' });
  await page.locator('.admin-shell').waitFor({ timeout: 20000 });
  await page.getByRole('heading', { name: 'Admin Menus', exact: true }).waitFor({ timeout: 20000 });

  const builtInMenu = page.getByRole('button', { name: /built-in/i }).first();
  if (await builtInMenu.count()) {
    await builtInMenu.click();
  }

  const button = page.getByRole('button', { name: /export json/i }).first();
  await button.waitFor({ timeout: 20000 });
  await button.click();
  await page.getByText('Primary navigation exported', { exact: false }).waitFor({ timeout: 20000 });

  const fileExists = fs.existsSync(exportFile);
  let hasExpectedShape = false;
  let itemCounts = '';
  if (fileExists) {
    const exported = JSON.parse(fs.readFileSync(exportFile, 'utf8'));
    hasExpectedShape = Array.isArray(exported.items) && Array.isArray(exported.customItems);
    itemCounts = `items=${exported.items?.length ?? 0} customItems=${exported.customItems?.length ?? 0}`;
  }

  return [
    { name: 'export-file-written', pass: fileExists, message: fileExists ? exportFile : `missing: ${exportFile}` },
    { name: 'export-has-items-arrays', pass: hasExpectedShape, message: itemCounts || 'file missing' },
  ];
};
