const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

function flatten(items) {
  return items.flatMap(item => [item, ...flatten(item.items || [])]);
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  try {
    const anonymous = await browser.newPage();
    await anonymous.goto(`${baseUrl}/login`, { waitUntil: 'domcontentloaded' });
    const anonymousStatus = await anonymous.evaluate(async () =>
      (await fetch('/api/crest/navigation/admin')).status);
    if (anonymousStatus !== 403) {
      throw new Error(`Expected anonymous navigation request to be forbidden, received ${anonymousStatus}`);
    }
    const anonymousRoute = await anonymous.goto(`${baseUrl}/Admin/Features`, { waitUntil: 'domcontentloaded' });
    if (anonymousRoute?.status() !== 200 || !/\/login$/i.test(anonymous.url())) {
      throw new Error(`Expected anonymous Crest route request to redirect to login, received ${anonymousRoute?.status()} at ${anonymous.url()}`);
    }
    await anonymous.locator('input[name="UserName"]').waitFor({ timeout: 20000 });
    await anonymous.close();

    const page = await browser.newPage();
    await page.goto(`${baseUrl}/login`, { waitUntil: 'domcontentloaded' });
    const userNameInput = page.locator('input[name="UserName"]');
    await userNameInput.waitFor({ timeout: 20000 });
    await userNameInput.fill(username);
    await page.locator('input[name="Password"]').fill(password);
    const hubNegotiation = page.waitForRequest(request =>
      request.url().includes('/api/crest/permissions/negotiate'), { timeout: 20000 });
    await page.getByRole('button', { name: 'Login', exact: true }).click();
    await page.waitForURL(/\/admin/i, { timeout: 15000 });
    await hubNegotiation;

    const menu = await page.evaluate(async () => {
      const response = await fetch('/api/crest/navigation/admin');
      return {
        status: response.status,
        contentType: response.headers.get('content-type'),
        text: await response.text(),
      };
    });
    if (menu.status !== 200 || !menu.contentType?.includes('application/json')) {
      throw new Error(`Authorized navigation was not JSON (${menu.status}, ${menu.contentType}, ${page.url()})`);
    }

    const payload = JSON.parse(menu.text);
    if (!Array.isArray(payload.items)) throw new Error('Authorized Orchard admin navigation was not returned');

    const nodes = flatten(payload.items);
    if (!nodes.length || nodes.some(node => !node.text || !node.key)) {
      throw new Error('Navigation response contains an invalid node');
    }

    const manifest = await page.evaluate(async () => {
      const response = await fetch('/api/crest/app/manifest');
      return { status: response.status, payload: await response.json() };
    });
    if (manifest.status !== 200 || !Array.isArray(manifest.payload.authorizedRoutes) || !manifest.payload.authorizedRoutes.length) {
      throw new Error('The authenticated manifest did not contain its route authorization batch');
    }

    console.log(JSON.stringify({ authorization: 'orchard', nodes: nodes.length, routes: manifest.payload.authorizedRoutes.length }));
  } finally {
    await browser.close();
  }
}

main().catch(error => { console.error(error); process.exit(1); });
