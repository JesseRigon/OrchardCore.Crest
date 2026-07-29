// Converted from the old admin-security-headers-response.js. That script mostly logged a
// diagnostic JSON blob (status, content-type, raw html snippets, tab html/text) without
// asserting on most of it — the real, throwing assertions were: the page responds, the
// Blazor page renders (data-testid element appears), and its tab strip renders with at
// least one tab. Kept those as pass/fail; dropped the pure-diagnostic fields (hasBlazorHost,
// hasNativeSecurityHeadersMarkup, tabText/tabHtml snippets) since they were never checked
// against an expected value in the original, just printed for a human to eyeball.
module.exports = async function run(page, ctx) {
  const response = await page.goto(`${ctx.baseUrl}/Admin/Settings/SecurityHeaders`, { waitUntil: 'domcontentloaded' });

  const status = response?.status();
  const contentType = response?.headers()['content-type'] || '';

  const rendered = await page.locator('[data-testid="security-headers-page"]').waitFor({ timeout: 20000 })
    .then(() => true).catch(() => false);

  let renderedHeading = '';
  let tabCount = 0;
  if (rendered) {
    renderedHeading = await page.locator('[data-testid="security-headers-page"] h4').innerText().catch(() => '');
    const tabs = page.locator('[data-testid="security-headers-tabs"]');
    tabCount = await tabs.locator('[role="tab"]').count();
  }

  return [
    { name: 'responds-ok', pass: status === 200, message: `status=${status}` },
    { name: 'response-is-html', pass: contentType.includes('text/html'), message: `content-type=${contentType}` },
    { name: 'renders-security-headers-page', pass: rendered, message: rendered ? `heading="${renderedHeading}"` : `did not render at ${page.url()}` },
    { name: 'renders-tab-strip', pass: tabCount >= 1, message: `tabs=${tabCount}` },
  ];
};
