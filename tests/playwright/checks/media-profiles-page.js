const { severeConsoleErrors, drainConsoleErrors } = require('../harness/instance');

// Converted from the old admin-media-profiles-page.js. Same assertions (page renders
// natively, no legacy iframe, create+delete a probe media profile via the API), minus
// the per-script browser launch/login boilerplate.
//
// Adaptation: the original threw immediately if the create PUT failed, which also
// skipped the DELETE cleanup call. Here we only attempt the DELETE if the create
// reported success, so we don't fire a cleanup request for a profile that was never
// created; the delete result is reported as a failure (not skipped) if create failed,
// since the overall probe still didn't achieve create+delete.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/MediaProfiles`, { waitUntil: 'networkidle' });
  await page.locator('[data-testid="media-profiles-page"]').waitFor({ timeout: 20000 });

  const iframeCount = await page.locator('iframe').count();

  const name = `crest-probe-${Date.now()}`;
  const saved = await page.request.put(`${ctx.baseUrl}/api/crest/media/profiles/${name}`, {
    data: {
      hint: 'probe',
      width: 120,
      height: 80,
      mode: 0,
      format: 0,
      quality: 80,
      backgroundColor: null,
      autoOrient: true,
    },
  });
  const savedOk = saved.ok();

  let removedOk = false;
  let removedStatus = 'skipped (create failed)';
  if (savedOk) {
    const removed = await page.request.delete(`${ctx.baseUrl}/api/crest/media/profiles/${name}`);
    removedOk = removed.ok();
    removedStatus = removed.status();
  }

  const errors = severeConsoleErrors(drainConsoleErrors(ctx.consoleErrors));

  return [
    { name: 'no-legacy-iframe', pass: iframeCount === 0, message: `iframe count=${iframeCount}` },
    { name: 'profile-create', pass: savedOk, message: `PUT status=${saved.status()}` },
    { name: 'profile-delete', pass: removedOk, message: `DELETE status=${removedStatus}` },
    { name: 'no-console-errors', pass: errors.length === 0, message: errors.join(' | ') || 'clean' },
  ];
};
