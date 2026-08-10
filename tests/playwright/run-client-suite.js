const path = require('path');
const { runSuite, printSummary } = require('./harness/run-suite');
const { loginAsClient } = require('./harness/auth');

// Mirror of run-admin-suite.js for the public/front-end (Crest.Site) side. No
// client-site feature checks exist yet — this is the wired-up entry point ready for the
// first one, using the same shared-instance runSuite engine and the same base/new
// screenshot-diff convention as the admin suite. Health checks are admin-shaped
// (they hit /Admin), so they're skipped here rather than repurposed to mean something
// they don't.
function buildSharedClientChecks() {
  return [
    { name: 'localization-anonymous', fn: require('./checks/localization-anonymous') },
    { name: 'localization-smoke-site', fn: require('./checks/localization-smoke-site') },
    { name: 'blazor-counter', fn: require('./checks/blazor-counter') },
  ];
}

async function main() {
  const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
  const outputRoot = process.env.OUTPUT_ROOT || path.join(__dirname, 'output');

  const checks = buildSharedClientChecks();
  if (checks.length === 0) {
    console.log('No client-site checks registered yet — nothing to run.');
    return;
  }

  const results = await runSuite({
    baseUrl,
    login: loginAsClient,
    checks,
    outputRoot,
    skipHealthCheck: true,
  });

  const ok = printSummary(results);
  process.exit(ok ? 0 : 1);
}

module.exports = { buildSharedClientChecks };

if (require.main === module) {
  main();
}
