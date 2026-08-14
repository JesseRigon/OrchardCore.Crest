const path = require('path');
const { runSuite, printSummary } = require('./harness/run-suite');
const { loginAsAdmin } = require('./harness/auth');

async function main() {
  const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
  const outputRoot = process.env.OUTPUT_ROOT || path.join(__dirname, 'output');

  const checks = [
    { name: 'primary-nav-default-icon-culture-switch', fn: require('./checks/primary-nav/default-icon-culture-switch') },
  ];

  const results = await runSuite({ baseUrl, login: loginAsAdmin, checks, outputRoot });
  const ok = printSummary(results);
  process.exit(ok ? 0 : 1);
}
main();
