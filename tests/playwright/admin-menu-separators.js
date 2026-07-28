const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
const cleanupExtra = Number.parseInt(process.env.CLEANUP_EXTRA_SEPARATORS || '0', 10);

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
    await page.getByRole('heading', { name: 'Primary Navigation', exact: true }).waitFor({ timeout: 20000 });

    if (cleanupExtra > 0) {
      for (let index = 0; index < cleanupExtra; index++) {
        const count = await page.locator('.admin-menu-separator').count();
        if (count === 0) {
          break;
        }

        await page.locator('.admin-menu-separator').last().locator('button').click();
        await page.waitForFunction(previousCount => document.querySelectorAll('.admin-menu-separator').length === previousCount - 1, count, { timeout: 20000 });
      }

      console.log(JSON.stringify({ cleaned: cleanupExtra, remaining: await page.locator('.admin-menu-separator').count() }));
      return;
    }

    const initialCount = await page.locator('.admin-menu-separator').count();
    await page.getByRole('button', { name: /\+ Add/i }).click();
    await page.getByRole('button', { name: /Separator/i }).click();
    await page.waitForFunction(count => document.querySelectorAll('.admin-menu-separator').length > count, initialCount, { timeout: 20000 });

    const separator = page.locator('.admin-menu-separator').last();
    const lineColor = await separator.locator('.admin-menu-separator__line').evaluate(element => getComputedStyle(element).backgroundColor);
    if (!lineColor || lineColor === 'rgba(0, 0, 0, 0)') {
      throw new Error(`Separator did not receive a visible line color: ${lineColor}`);
    }

    const separatorLayout = await separator.evaluate(element => {
      const handle = element.querySelector('.admin-menu-node__handle');
      const text = Array.from(element.querySelectorAll('*')).find(child => child.textContent?.trim() === 'Separator');
      const line = element.querySelector('.admin-menu-separator__line');
      const handleRect = handle?.getBoundingClientRect();
      const textRect = text?.getBoundingClientRect();
      const lineRect = line?.getBoundingClientRect();
      return {
        hasHandle: Boolean(handle),
        handleLeft: handleRect?.left ?? 0,
        textLeft: textRect?.left ?? 0,
        lineLeft: lineRect?.left ?? 0,
      };
    });
    if (!separatorLayout.hasHandle || !(separatorLayout.handleLeft < separatorLayout.textLeft && separatorLayout.textLeft < separatorLayout.lineLeft)) {
      throw new Error(`Separator editor row is not left-aligned with handle/text before the line: ${JSON.stringify(separatorLayout)}`);
    }

    await page.evaluate(() => {
      const source = document.querySelector('.admin-menu-tree__list--root > .admin-menu-tree__item[data-entry-type="separator"]');
      const handle = source?.querySelector('.admin-menu-node__handle');
      const target = document.querySelector('.admin-menu-tree__list--root > .admin-menu-tree__item[data-entry-type="node"]');
      if (!source || !handle || !target) {
        throw new Error('Could not resolve separator drag source or target geometry.');
      }

      const dataTransfer = new DataTransfer();
      const targetRect = target.getBoundingClientRect();
      const eventInit = {
        bubbles: true,
        cancelable: true,
        dataTransfer,
        clientX: targetRect.left + targetRect.width / 2,
        clientY: targetRect.top + 3,
      };

      handle.dispatchEvent(new DragEvent('dragstart', { ...eventInit, clientY: handle.getBoundingClientRect().top + 3 }));
      target.dispatchEvent(new DragEvent('dragover', eventInit));
      target.dispatchEvent(new DragEvent('drop', eventInit));
      handle.dispatchEvent(new DragEvent('dragend', eventInit));
    });
    await page.waitForFunction(() => document.querySelector('.admin-menu-tree__list--root > .admin-menu-tree__item')?.dataset.entryType === 'separator', null, { timeout: 20000 });

    await page.locator('.admin-menu-tree__list--root > .admin-menu-tree__item[data-entry-type="separator"]').first().locator('button').click();
    await page.waitForFunction(count => document.querySelectorAll('.admin-menu-separator').length === count, initialCount, { timeout: 20000 });

    console.log(JSON.stringify({ initialCount, finalCount: await page.locator('.admin-menu-separator').count(), lineColor }));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
