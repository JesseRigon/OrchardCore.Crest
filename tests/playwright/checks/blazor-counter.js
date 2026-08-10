const BLAZOR_COUNTER_CONTENT_ITEM_ID = '4c1b6c1f8f5b4a2f9c2d7e8a1b3f5c6d';

// Verifies the first Blazor interactive island (Components/Pages/BlazorCounter.razor,
// mapped through Startup.cs's MapRazorComponents<App>() endpoint, not the static-SSR
// shape pipeline) is both reachable and genuinely interactive - a real SignalR
// circuit/WASM click handler, not just server-rendered markup. The seeded content item
// (see Recipes/orchardcore.crest.dev.recipe.json) carries CrestBlazorComponentPart with
// ComponentName "CrestCounter" and Parameters.StartValue "5".
module.exports = async function run(page, ctx) {
  const url = `${ctx.baseUrl}/blazor-counter/${BLAZOR_COUNTER_CONTENT_ITEM_ID}`;
  await page.goto(url, { waitUntil: 'networkidle' });

  const counter = page.locator('.rz-counter');
  const found = await counter.waitFor({ state: 'attached', timeout: 5000 }).then(() => true).catch(() => false);
  if (!found) {
    return [{
      name: 'blazor-counter-renders',
      pass: false,
      status: 'not-found',
      message: `.rz-counter not found at ${url} - is the seeded BlazorComponent content item present?`,
    }];
  }

  const results = [{ name: 'blazor-counter-renders', pass: true, message: 'ok' }];

  const countText = page.locator('.rz-counter p');
  const initial = await countText.textContent();
  results.push({
    name: 'blazor-counter-initial-value',
    pass: initial.trim() === 'Count: 5',
    message: `text=${initial.trim()}`,
  });

  const button = page.locator('.rz-counter button');
  await button.click();

  // Interactivity needs an attached circuit (Server) or downloaded WASM runtime
  // (WebAssembly) - a static-SSR click would be inert, so a real increment here is
  // proof the render mode wiring (AddInteractiveServerRenderMode /
  // AddInteractiveWebAssemblyRenderMode) actually works end-to-end, not just that the
  // markup happens to look right.
  await page.waitForFunction(
    () => document.querySelector('.rz-counter p')?.textContent?.trim() === 'Count: 6',
    { timeout: 5000 },
  ).then(() => true).catch(() => false);

  const afterClick = await countText.textContent();
  results.push({
    name: 'blazor-counter-increments-on-click',
    pass: afterClick.trim() === 'Count: 6',
    message: `text=${afterClick.trim()}`,
  });

  return results;
};
