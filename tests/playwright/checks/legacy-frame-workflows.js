// Converted from OrchardCore.Crest/tests/playwright/legacy-frame-workflows.js.
// Legacy (pre-Crest) Orchard admin pages render inside an iframe wrapper, never nesting
// a second Crest shell. Verifies the initial frame, an in-frame link navigation, and a
// location.href reassignment all preserve legacy-frame=1 and stay free of nested chrome.
module.exports = async function run(page, ctx) {
  async function assertNoNestedCrestShell(frame) {
    const nestedFrames = await frame.locator('iframe.legacy-admin-frame').count();
    const nestedShellNotice = await frame.getByText('This Orchard admin page is running inside the Crest Admin shell.').count();
    const nestedPrimaryNavMenu = await frame.locator('.primary-nav-menu').count();
    return nestedFrames === 0 && nestedShellNotice === 0 && nestedPrimaryNavMenu === 0;
  }

  const response = await page.goto(`${ctx.baseUrl}/Admin/Workflows/Types`, { waitUntil: 'networkidle' });
  if (!response || response.status() >= 400) {
    return [{ name: 'workflows-types-page-loads', pass: false, message: `status=${response?.status() ?? 'no response'}` }];
  }

  const frameElement = page.locator('iframe.legacy-admin-frame').first();
  await frameElement.waitFor({ timeout: 20000 });
  const frame = await (await frameElement.elementHandle()).contentFrame();
  if (!frame) {
    return [{ name: 'legacy-frame-available', pass: false, message: 'Legacy frame was not available.' }];
  }

  await frame.locator('body.crest-legacy-frame').waitFor({ timeout: 20000 });
  const initialNoNesting = await assertNoNestedCrestShell(frame);
  const initialFrameUrl = new URL(frame.url());
  const initialKeepsLegacyParam = initialFrameUrl.searchParams.get('legacy-frame') === '1';

  const editLink = frame.locator('a[href*="/Admin/Workflows/Types/Edit/"]:not([href*="EditProperties"])').first();
  await editLink.waitFor({ timeout: 20000 });
  const editHref = await editLink.getAttribute('href');
  if (!editHref) {
    return [{ name: 'workflow-edit-link-has-href', pass: false, message: 'Workflow edit link did not include an href.' }];
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
  const editNoNesting = await assertNoNestedCrestShell(frame);

  let addEventShowsCards = false;
  await frame.getByRole('button', { name: /add event/i }).click();
  await frame.locator('#activity-picker.show').waitFor({ timeout: 10000 });
  const visibleEventCards = await frame.locator('#activity-picker .activity[data-activity-type="Event"]').evaluateAll(elements =>
    elements
      .filter(element => {
        const style = window.getComputedStyle(element);
        const rect = element.getBoundingClientRect();
        return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0;
      })
      .map(element => element.textContent?.trim())
      .filter(Boolean),
  );
  addEventShowsCards = visibleEventCards.length > 0;

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
  const reassignedNoNesting = await assertNoNestedCrestShell(frame);
  const reassignedUrl = new URL(frame.url());
  const reassignedKeepsLegacyParam = reassignedUrl.searchParams.get('legacy-frame') === '1';

  return [
    { name: 'initial-frame-no-nested-shell', pass: initialNoNesting, message: `noNesting=${initialNoNesting}` },
    { name: 'initial-frame-keeps-legacy-param', pass: initialKeepsLegacyParam, message: frame.url() },
    { name: 'edit-frame-no-nested-shell', pass: editNoNesting, message: `noNesting=${editNoNesting}` },
    { name: 'add-event-modal-shows-cards', pass: addEventShowsCards, message: visibleEventCards.slice(0, 5).join(', ') || 'no cards' },
    { name: 'location-reassign-no-nested-shell', pass: reassignedNoNesting, message: `noNesting=${reassignedNoNesting}` },
    {
      name: 'location-reassign-forced-back-to-legacy-frame',
      pass: reassignedKeepsLegacyParam,
      message: `Iframe navigation without query should be forced back into legacy frame mode: ${reassignedUrl.toString()}`,
    },
  ];
};
