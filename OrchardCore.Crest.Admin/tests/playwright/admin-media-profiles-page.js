const { chromium } = require('playwright');
const baseUrl = process.env.BASE_URL || 'http://fruitful.localhost:5010';
const username = process.env.ADMIN_USER || 'admin';
const password = process.env.ADMIN_PASSWORD || 'FruitfulRules1!';
async function main() {
 const browser=await chromium.launch({headless:true}); const page=await browser.newPage();
 try {
  await page.goto(`${baseUrl}/login`,{waitUntil:'networkidle'});
  if(await page.locator('#UserName').count()){await page.fill('#UserName',username);await page.fill('#Password',password);await page.press('#Password','Enter');await page.waitForURL(/\/admin/i,{timeout:15000}).catch(()=>{});}
  await page.goto(`${baseUrl}/Admin/MediaProfiles`,{waitUntil:'networkidle'});
  await page.locator('[data-testid="media-profiles-page"]').waitFor({timeout:20000});
  if(await page.locator('iframe').count()) throw new Error('Legacy frame rendered.');
  const name=`crest-probe-${Date.now()}`;
  const saved=await page.request.put(`${baseUrl}/api/crest/media/profiles/${name}`,{data:{hint:'probe',width:120,height:80,mode:0,format:0,quality:80,backgroundColor:null,autoOrient:true}});
  if(!saved.ok()) throw new Error(`Profile save failed: ${saved.status()}`);
  const removed=await page.request.delete(`${baseUrl}/api/crest/media/profiles/${name}`);
  if(!removed.ok()) throw new Error(`Profile cleanup failed: ${removed.status()}`);
  console.log(JSON.stringify({name,createdAndDeleted:true}));
 } finally { await browser.close(); }
}
main().catch(e=>{console.error(e);process.exit(1)});
// Playwright probe owned by OrchardCore.Crest.Admin.
