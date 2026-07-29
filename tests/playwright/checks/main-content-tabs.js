// Converted from the old admin-main-content-tabs.js
// (modules/OrchardCore.Crest/tests/playwright/admin-main-content-tabs.js). Verifies the
// shared CrestMainContentTabs component across three different settings pages: the tab
// strip renders with the expected labels, and clicking the last tab actually selects it
// (aria-selected="true").
module.exports = async function run(page, ctx) {
  const results = [];

  const pages = [
    ['/Admin/Settings/SecurityHeaders', ['Content Security Policy', 'Permissions Policy', 'Referrer Policy']],
    ['/Admin/Settings/admin', ['Admin', 'Site Map']],
    ['/Admin/Settings/general', ['General', 'Resources', 'Cache']],
  ];

  for (const [route, labels] of pages) {
    await page.goto(`${ctx.baseUrl}${route}`, { waitUntil: 'networkidle' });
    const tabs = page.locator('.crest-main-content-tabs');
    const tabsOk = await tabs.waitFor({ timeout: 20000 }).then(() => true).catch(() => false);

    if (!tabsOk) {
      results.push({ name: `tabs-render:${route}`, pass: false, message: 'crest-main-content-tabs not found' });
      continue;
    }

    let allLabelsPresent = true;
    for (const label of labels) {
      const present = await tabs.locator('[role="tab"]').filter({ hasText: label }).waitFor({ timeout: 10000 }).then(() => true).catch(() => false);
      if (!present) allLabelsPresent = false;
    }
    results.push({
      name: `tabs-render:${route}`,
      pass: allLabelsPresent,
      message: allLabelsPresent ? `labels ok: ${labels.join(', ')}` : `missing one of: ${labels.join(', ')}`,
    });

    const lastLabel = labels.at(-1);
    const lastTab = tabs.locator('[role="tab"]').filter({ hasText: lastLabel });
    await lastTab.click();
    const selected = await lastTab.getAttribute('aria-selected');
    results.push({ name: `tab-click-selects:${route}`, pass: selected === 'true', message: `aria-selected=${selected} tab="${lastLabel}"` });
  }

  return results;
};
