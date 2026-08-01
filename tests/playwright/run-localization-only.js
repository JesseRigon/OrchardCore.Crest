const path = require('path');
const { runSuite, printSummary } = require('./harness/run-suite');
const { loginAsAdmin } = require('./harness/auth');

async function main() {
  const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
  const outputRoot = path.join(__dirname, 'output');

  const checks = [
    { name: 'localization-sequential-settings', fn: require('./checks/localization-sequential-settings') },
    { name: 'localization-tab-scoping', fn: require('./checks/localization-tab-scoping') },
    { name: 'localization-new-tab-inheritance', fn: require('./checks/localization-new-tab-inheritance') },
    { name: 'localization-multi-user-switch', fn: require('./checks/localization-multi-user-switch') },
  ];

  const results = await runSuite({ baseUrl, login: loginAsAdmin, checks, outputRoot, skipHealthCheck: true });
  const ok = printSummary(results);
  process.exit(ok ? 0 : 1);
}
main();
