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

async function assertNoNestedCrestShell(frame, label) {
  const nestedFrames = await frame.locator('iframe.legacy-admin-frame').count();
  const nestedShellNotice = await frame.getByText('This Orchard admin page is running inside the Crest Admin shell.').count();
  const nestedSidebar = await frame.locator('.admin-menu-sidebar').count();
  if (nestedFrames || nestedShellNotice || nestedSidebar) {
    throw new Error(`${label} rendered nested Crest shell/chrome: frames=${nestedFrames}, notice=${nestedShellNotice}, sidebar=${nestedSidebar}`);
  }
}

async function main() {
  const browser = await chromium.launch({ headless: !headed });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } });

  page.on('console', message => console.log(`[browser:${message.type()}] ${message.text()}`));
  page.on('pageerror', error => console.log(`[browser:error] ${error.message}`));

  try {
    await login(page);
    const response = await page.goto(`${baseUrl}/Admin/Workflows/Types`, { waitUntil: 'networkidle' });
    if (!response || response.status() >= 400) {
      throw new Error(`/Admin/Workflows/Types returned ${response?.status() ?? 'no response'}`);
    }

    const frameElement = page.locator('iframe.legacy-admin-frame').first();
    await frameElement.waitFor({ timeout: 20000 });
    const frameHandle = await frameElement.elementHandle();
    const frame = await frameHandle.contentFrame();
    if (!frame) {
      throw new Error('Legacy frame was not available.');
    }

    await frame.locator('body.crest-legacy-frame').waitFor({ timeout: 20000 });
    await assertNoNestedCrestShell(frame, 'initial workflows frame');

    const initialFrameUrl = new URL(frame.url());
    if (initialFrameUrl.searchParams.get('legacy-frame') !== '1') {
      throw new Error(`Initial frame URL did not keep legacy-frame=1: ${frame.url()}`);
    }

    const editLink = frame.locator('a[href*="/Admin/Workflows/Types/Edit/"]:not([href*="EditProperties"])').first();
    await editLink.waitFor({ timeout: 20000 });
    const editHref = await editLink.getAttribute('href');
    if (!editHref) {
      throw new Error('Workflow edit link did not include an href.');
    }
    await Promise.all([
      frame.waitForURL(/\/Admin\/Workflows\/Types\/Edit\//i, { timeout: 20000 }),
      frame.evaluate(href => {
        const url = new URL(href, window.location.href);
        url.searchParams.delete('legacy-frame');
        window.location.href = url.pathname + url.search + url.hash;
      }, editHref),
    ]);
    await frame.waitForLoadState('networkidle').catch(() => {});
    await frame.locator('body.crest-legacy-frame').waitFor({ timeout: 20000 });
    await assertNoNestedCrestShell(frame, 'workflow edit frame');
    const editFrameUrl = new URL(frame.url());

    await frame.getByRole('button', { name: /add event/i }).click();
    await frame.locator('#activity-picker.show').waitFor({ timeout: 10000 });

    const visibleEventCards = await frame.locator('#activity-picker .activity[data-activity-type="Event"]').evaluateAll(elements =>
      elements.filter(element => {
        const style = window.getComputedStyle(element);
        const rect = element.getBoundingClientRect();
        return style.display !== 'none'
          && style.visibility !== 'hidden'
          && rect.width > 0
          && rect.height > 0;
      }).map(element => element.textContent?.trim()).filter(Boolean)
    );

    if (!visibleEventCards.length) {
      const modalText = await frame.locator('#activity-picker').innerText().catch(() => '');
      throw new Error(`Add Event modal did not show any event cards. Modal text: ${modalText}`);
    }

    await frame.locator('#activity-picker .btn-close').click();
    await frame.locator('#activity-picker.show').waitFor({ state: 'detached', timeout: 10000 }).catch(async () => {
      await frame.locator('#activity-picker.show').waitFor({ state: 'hidden', timeout: 10000 });
    });

    await Promise.all([
      frame.waitForURL(/\/Admin\/Workflows\/Types(\?|$)/i, { timeout: 20000 }),
      frame.evaluate(() => {
        window.location.href = '/Admin/Workflows/Types';
      }),
    ]);
    await frame.waitForLoadState('networkidle').catch(() => {});
    await frame.locator('body.crest-legacy-frame').waitFor({ timeout: 20000 });
    await assertNoNestedCrestShell(frame, 'location-assigned workflows frame');

    const reassignedUrl = new URL(frame.url());
    if (reassignedUrl.searchParams.get('legacy-frame') !== '1') {
      throw new Error(`Iframe navigation without query was not forced back into legacy frame mode: ${frame.url()}`);
    }

    console.log(JSON.stringify({
      initialFrameUrl: initialFrameUrl.pathname + initialFrameUrl.search,
      editFrameUrl: editFrameUrl.pathname + editFrameUrl.search,
      reassignedFrameUrl: reassignedUrl.pathname + reassignedUrl.search,
      visibleEventCards: visibleEventCards.slice(0, 5),
    }, null, 2));
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exit(1);
});
// Playwright probe owned by OrchardCore.Crest legacy-frame infrastructure.
