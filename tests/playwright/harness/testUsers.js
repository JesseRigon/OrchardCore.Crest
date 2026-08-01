const { fetchAntiforgeryToken } = require('./antiforgery');

// Provisions the two non-admin identities the localization multi-user/anonymous checks
// need (see plans/user-localization-testing.md): a "testuser" (rung 2 - stored default es)
// and a "testuser2" (rung 2 - stored default fr, used for switch-user coverage). Created
// idempotently via api/crest/users (CrestUsersController) against an already-logged-in
// admin page/context - no recipe/setup changes, so this works against any already-
// provisioned tenant, not just a fresh one.
//
// Per tests/README.md's "credentials never live here" rule: usernames/passwords are read
// from the environment, falling back to generic (non-secret) defaults that only work
// against a throwaway dev tenant - never hardcode a real credential here.
function testUserCredentials(suffix) {
  const upper = suffix.toUpperCase();
  return {
    username: process.env[`CLIENT_USER${upper}`] || `testuser${suffix}`,
    password: process.env[`CLIENT_PASSWORD${upper}`] || 'FruitfulRules1!',
  };
}

// suffix: '' for the first test user, '2' for the second (switch-user) identity.
// Looks up by username first (GET api/crest/users?search=) and only creates if absent -
// id is always resolved via that same id-based API (CrestUsersController.ListAsync),
// never guessed or looked up by name after creation.
async function ensureTestUser(page, baseUrl, suffix = '') {
  const { username, password } = testUserCredentials(suffix);

  const existing = await page.evaluate(async ({ baseUrl, username }) => {
    const response = await fetch(`${baseUrl}/api/crest/users?search=${encodeURIComponent(username)}`, {
      credentials: 'include',
    });
    if (!response.ok) throw new Error(`GET api/crest/users?search= failed: ${response.status} ${await response.text()}`);
    const body = await response.json();
    return body.items.find(user => user.userName?.toLowerCase() === username.toLowerCase()) || null;
  }, { baseUrl, username });

  if (existing) {
    return { username, password, id: existing.id, created: false };
  }

  const antiforgery = await fetchAntiforgeryToken(page, baseUrl);

  const created = await page.evaluate(async ({ baseUrl, username, password, antiforgery }) => {
    const headers = { 'Content-Type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken };
    const response = await fetch(`${baseUrl}/api/crest/users`, {
      method: 'POST',
      credentials: 'include',
      headers,
      body: JSON.stringify({
        userName: username,
        email: `${username}@fruitful.example.com`,
        emailConfirmed: true,
        isEnabled: true,
        // Culture resolution only happens inside the Blazor .Admin shell
        // (DisplayManager/AppController.GetManifest requires AdminPermissions.
        // AccessAdminPanel) - a role-less user can log in but gets "Access denied" on
        // every /Admin route, which is useless for these checks. "Administrator" is the
        // only stock role with isAdmin:true (confirmed via GET api/crest/roles); a
        // throwaway dev-tenant test account having full admin rights is an acceptable
        // trade-off here, matching every other Playwright check's use of the fixed
        // `admin` account.
        roles: ['Administrator'],
        password,
      }),
    });
    if (!response.ok) {
      throw new Error(`Failed to create user '${username}': ${response.status} ${await response.text()}`);
    }
    return response.json();
  }, { baseUrl, username, password, antiforgery });

  return { username, password, id: created.id, created: true };
}

module.exports = { ensureTestUser, testUserCredentials };
