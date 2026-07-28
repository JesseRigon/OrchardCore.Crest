const { chromium } = require('playwright');

const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

async function login(page) {
  await page.goto(`${baseUrl}/Login`, { waitUntil: 'networkidle' });
  const userInput = page.locator('#UserName, #LoginForm_UserName').first();
  const passwordInput = page.locator('#Password, #LoginForm_Password').first();
  if (await userInput.count()) {
    await userInput.fill(username);
    await passwordInput.fill(password);
    await page.locator('button:has-text("Log in"), button:has-text("Login")').first().click();
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  page.on('console', msg => console.log(`[browser:${msg.type()}] ${msg.text()}`));
  await login(page);
  await page.goto(`${baseUrl}/Admin/CRM/Customers`, { waitUntil: 'networkidle' });

  const trace = await page.evaluate(() => {
    const selectors = [
      '#ta-left-primaryNavMenu a',
      '#ta-left-primaryNavMenu li',
      '#ta-left-primaryNavMenu .item-label',
      '.ta-left-primaryNavMenu a',
      '.admin-menu a',
      '.primary-nav-menu__item-content',
      '.rz-panel-menu a',
      'nav a',
      'a'
    ];
    const seen = new Set();
    const out = { url: location.href, title: document.title, bodyClasses: document.body.className, matches: [] };
    for (const selector of selectors) {
      for (const el of document.querySelectorAll(selector)) {
        if (seen.has(el)) continue;
        const text = (el.textContent || '').replace(/\s+/g, ' ').trim();
        if (!/^(Content|Design|Platform|Settings|Media|New|CRM|Customers|Content Items|Content Definition)\b/i.test(text)) continue;
        seen.add(el);
        const icon = el.querySelector('.icon, i[class*=fa], .orchard-icon, svg') || el.previousElementSibling?.matches?.('.icon, i[class*=fa], .orchard-icon, svg') && el.previousElementSibling;
        const parentChain = [];
        let p = el;
        for (let i = 0; p && i < 5; i++, p = p.parentElement) {
          parentChain.push({ tag: p.tagName.toLowerCase(), id: p.id || null, cls: p.className?.toString?.() || null });
        }
        out.matches.push({
          selector,
          text,
          href: el.getAttribute('href'),
          class: el.className?.toString?.() || null,
          iconHtml: icon?.outerHTML?.replace(/\s+/g, ' ').slice(0, 500) || null,
          html: el.outerHTML.replace(/\s+/g, ' ').slice(0, 800),
          parentChain
        });
      }
    }
    return out;
  });

  console.log(JSON.stringify(trace, null, 2));
  await browser.close();
}

main().catch(error => { console.error(error); process.exit(1); });
// Playwright probe owned by OrchardCore.Crest.Admin.
