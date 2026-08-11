// Blazor Web App prerender race helper. Under InteractiveAuto the SSR document
// contains real, visible buttons BEFORE any interactive runtime (circuit or WASM)
// attaches handlers — a single early click lands on an inert element and nothing
// happens. There is no public "handlers attached" signal, so the robust pattern is
// effect-based: click, wait briefly for the expected effect, retry until it appears.
async function clickForEffect(clickTarget, effectTarget, { attempts = 5, effectTimeout = 4000, state = 'visible' } = {}) {
  let lastError;
  for (let attempt = 0; attempt < attempts; attempt++) {
    try {
      await clickTarget.click({ timeout: effectTimeout });
    } catch (error) {
      lastError = error;
    }
    const ok = await effectTarget.waitFor({ state, timeout: effectTimeout })
      .then(() => true)
      .catch(error => {
        lastError = error;
        return false;
      });
    if (ok) return;
  }
  throw lastError || new Error('clickForEffect: effect never appeared');
}

module.exports = { clickForEffect };
