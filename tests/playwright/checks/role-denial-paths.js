const { createInstance } = require('../harness/instance');
const { loginAsUser } = require('../harness/auth');
const { ensureTestUser } = require('../harness/testUsers');
const { fetchAntiforgeryToken } = require('../harness/antiforgery');

// The role-switching fixture's first consumer: end-to-end denial paths. The suite
// otherwise runs as ONE administrator, so scope failures (silent data exposure)
// were only unit-tested. A limited-role identity must (a) still reach the admin
// shell, (b) get the client route gate's Access denied on pages its role cannot
// view, and (c) get real 403s from the APIs behind them - the client gate is a
// convenience, the server is the authority, and this asserts BOTH layers agree.
//
// The limited role is resolved from the tenant's own roles (any non-admin,
// non-system role; Editor preferred) rather than hardcoded, so the check works on
// any provisioned tenant. The identity is provisioned/re-pointed through
// ensureTestUser's roles option - re-runs re-align the roles, nothing accumulates.
module.exports = async function run(page, ctx) {
  const results = [];

  const roles = await page.evaluate(async () => {
    const response = await fetch('/api/crest/roles', { credentials: 'include' });
    if (!response.ok) throw new Error(`GET api/crest/roles failed: ${response.status}`);
    return response.json();
  });
  const limitedRole = roles.find(role => role.name === 'Editor' && !role.isAdmin)
    ?? roles.find(role => !role.isAdmin && !role.isSystem);
  if (!limitedRole) {
    return [{ name: 'limited-role-available', pass: false, message: `no non-admin role on this tenant: ${JSON.stringify(roles.map(role => role.name))}` }];
  }

  const user = await ensureTestUser(page, ctx.baseUrl, '3', { roles: [limitedRole.name] });

  // A second browser: isolated cookies, and the suite's shared admin page stays
  // untouched for provisioning (same pattern as localization-multi-user-switch).
  const session = await createInstance();
  try {
    await loginAsUser(session.page, ctx.baseUrl, user);
    await session.page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });

    // (a) The shell itself loads for a role with admin access.
    const navShown = await session.page.locator('.primary-nav-menu').waitFor({ timeout: 20000 }).then(() => true).catch(() => false);
    results.push({ name: 'limited-role-reaches-admin-shell', pass: navShown, message: `role=${limitedRole.name}` });

    // (b) Route gating on HARD navigation: BlazorAdminThemeMiddleware answers a
    // denied admin route with a bare 403 before Blazor ever renders (the client
    // gate's Access-denied panel only appears on CLIENT-side navigation). The
    // probe route must sit behind a permission NO non-admin role can plausibly
    // hold: /Admin/Tenants (ManageTenants). What a stock role holds elsewhere
    // (Editor legitimately Views content part lists, and this tenant's Editor
    // even manages admin menus) is tenant policy, not this check's contract.
    {
      const response = await session.page.goto(`${ctx.baseUrl}/Admin/Tenants`, { waitUntil: 'domcontentloaded' }).catch(() => null);
      const status = response?.status() ?? 0;
      const deniedPanel = status === 403
        ? false
        : await session.page.locator('[data-testid="crest-route-forbidden"]').waitFor({ timeout: 15000 }).then(() => true).catch(() => false);
      results.push({
        name: 'route-gate-denies-tenants',
        pass: status === 403 || deniedPanel,
        message: `/Admin/Tenants status=${status} deniedPanel=${deniedPanel} role=${limitedRole.name}`,
      });
    }

    // (c) The server is the authority: a security-critical API 403s outright.
    // LockContentPartLists is Administrator-only by design (deliberately NOT
    // implied by ManageContentPartLists), and the probe key does not exist -
    // authorization runs before resolution, so even a misconfigured tenant
    // where the role somehow held the permission could only 4xx, never mutate.
    await session.page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });
    const antiforgery = await fetchAntiforgeryToken(session.page, ctx.baseUrl);
    const statuses = await session.page.evaluate(async ({ antiforgery }) => {
      const results = {};
      results.lockWrite = (await fetch('/api/crest/content-part-lists/zz.denial.probe/locks', {
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
        body: JSON.stringify({ dataLock: true }),
      })).status;
      // Positive control: their own identity read works, so the 403 above is
      // authorization, not a broken session.
      results.me = (await fetch('/api/crest/auth/me', { credentials: 'include' })).status;
      return results;
    }, { antiforgery });

    results.push({
      name: 'security-critical-api-403s',
      pass: statuses.lockWrite === 403,
      message: JSON.stringify({ ...statuses, role: limitedRole.name }),
    });
    results.push({
      name: 'limited-session-still-authenticated',
      pass: statuses.me === 200,
      message: `auth/me=${statuses.me}`,
    });
  } finally {
    await session.browser.close();
  }

  return results;
};
