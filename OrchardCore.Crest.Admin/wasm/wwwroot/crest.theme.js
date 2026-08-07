window.crestTheme = (() => {
  const themes = new Set([
    'material-base',
    'material',
    'material-dark-base',
    'material-dark',
    'standard-base',
    'standard',
    'standard-dark-base',
    'standard-dark',
    'default-base',
    'default',
    'dark-base',
    'dark',
    'software-base',
    'software',
    'software-dark-base',
    'software-dark',
    'humanistic-base',
    'humanistic',
    'humanistic-dark-base',
    'humanistic-dark'
  ]);

  function currentThemeName() {
    const themeLink = document.getElementById('crest-radzen-theme');
    const match = themeLink?.href?.match(/\/([^/]+)\.css(?:\?|$)/);
    return match?.[1] || 'material-base';
  }

  function isDarkTheme(theme) {
    return /(^|-)dark($|-)/i.test(theme || currentThemeName());
  }

  function darkVariant(theme) {
    if (isDarkTheme(theme)) return theme;
    const candidate = theme.endsWith('-base') ? theme.replace('-base', '-dark-base') : `${theme}-dark`;
    return themes.has(candidate) ? candidate : 'dark-base';
  }

  function lightVariant(theme) {
    const value = theme || currentThemeName();
    if (value === 'dark-base' || value === 'dark') return 'material-base';
    const candidate = value
      .replace('-dark-base', '-base')
      .replace('-dark', '');
    return themes.has(candidate) ? candidate : 'material-base';
  }

  function setTheme(theme) {
    const safeTheme = themes.has(theme) ? theme : 'material-base';
    const themeLink = document.getElementById('crest-radzen-theme');

    if (themeLink) {
      themeLink.href = `/_content/OrchardCore.Crest.Components/css/${safeTheme}.css`;
    }

    try {
      localStorage.setItem('crest-theme-mode', isDarkTheme(safeTheme) ? 'dark' : 'light');
    } catch {}
  }

  function apply(radzenTheme, variables) {
    let theme = themes.has(radzenTheme) ? radzenTheme : 'material-base';
    try {
      const mode = localStorage.getItem('crest-theme-mode');
      if (mode === 'dark') theme = darkVariant(theme);
      if (mode === 'light') theme = lightVariant(theme);
    } catch {}

    setTheme(theme);

    const root = document.documentElement;
    for (const [name, value] of Object.entries(variables || {})) {
      root.style.setProperty(name, value);
    }
  }

  function toggleMode() {
    const nextTheme = isDarkTheme() ? lightVariant(currentThemeName()) : darkVariant(currentThemeName());
    setTheme(nextTheme);
    return isDarkTheme(nextTheme);
  }

  const localUsersKey = 'crest-admin-local-users';

  function readLocalUsers() {
    try {
      const value = JSON.parse(localStorage.getItem(localUsersKey) || '[]');
      return Array.isArray(value) ? value : [];
    } catch {
      return [];
    }
  }

  function writeLocalUsers(users) {
    try {
      localStorage.setItem(localUsersKey, JSON.stringify(users));
    } catch {}
  }

  function rememberSignedInUser(userName, tenantName, tenantPrefix) {
    if (!userName) return readLocalUsers();

    const normalizedName = String(userName);
    const now = new Date().toISOString();
    const users = readLocalUsers().filter(user => String(user.userName || '').toLowerCase() !== normalizedName.toLowerCase());
    const existing = readLocalUsers().find(user => String(user.userName || '').toLowerCase() === normalizedName.toLowerCase()) || {};
    const tenant = {
      tenantName: tenantName || '',
      tenantPrefix: tenantPrefix || '',
      lastSeenUtc: now
    };
    const tenants = [tenant, ...((existing.tenants || [])
      .filter(item => String(item.tenantName || '').toLowerCase() !== String(tenant.tenantName || '').toLowerCase()))]
      .slice(0, 12);

    users.unshift({
      userName: normalizedName,
      lastTenantName: tenant.tenantName,
      lastTenantPrefix: tenant.tenantPrefix,
      lastSeenUtc: now,
      tenants
    });

    const trimmed = users.slice(0, 12);
    writeLocalUsers(trimmed);
    return trimmed;
  }

  function getKnownUsers() {
    return readLocalUsers();
  }

  function isDarkMode() {
    return isDarkTheme();
  }

  const sessionCultureKey = 'crest-admin-session-culture';

  // Culture resolution happens entirely client-side (DisplayManager.RefreshManifestAsync -
  // see plans/user-localization.md's "Resolution architecture" section). setAdminCulture is
  // called with the fully-resolved culture on every resolution (not only when the user
  // explicitly picks one), and writes a tenant-wide cookie (CrestCultureCookie server-side,
  // NOT AdminCookieCultureProvider's admin-path-scoped one) so both the WASM app's own API
  // calls and any legacy same-origin iframed Orchard page see the same answer. Mirrored
  // into localStorage, same pattern as crest-theme-mode/crest-admin-local-users, so a fresh
  // tab can rehydrate the resolved culture before the first server round-trip completes.
  // NOTE: because the cookie is per-origin (not per-tab), it only reflects whichever tab
  // wrote it most recently - see getSessionCultureOverride below for the actual per-tab
  // source of truth. CrestAntiforgeryHandler.RewriteCultureCookie calls this same function
  // again (via getSessionCultureOverride/getBrowserLocale) immediately before every
  // outgoing Crest API request, not only on manifest refresh, so each tab's own requests
  // always carry its own resolved culture - see plans/user-localization.md phase 15.
  function setAdminCulture(cookieName, cookiePath, culture) {
    if (!cookieName || !culture) return;
    const path = cookiePath || '/';
    const maxAgeSeconds = 60 * 60 * 24 * 365;
    document.cookie = `${encodeURIComponent(cookieName)}=c=${encodeURIComponent(culture)}|uic=${encodeURIComponent(culture)}; path=${path}; max-age=${maxAgeSeconds}; SameSite=Lax`;
    try {
      localStorage.setItem(sessionCultureKey, culture);
    } catch {}
  }

  function getSessionCulture() {
    try {
      return localStorage.getItem(sessionCultureKey);
    } catch {
      return null;
    }
  }

  // The session OVERRIDE is rung 1 of the resolution chain: only set when the user
  // explicitly picks a culture from the titlebar dropdown, and must never be conflated
  // with a value that only ended up in the cookie because it won some other rung (stored
  // default, browser locale, tenant default).
  //
  // Storage: sessionStorage, not localStorage - sessionStorage is genuinely per-tab (a
  // fresh, empty, non-inherited store per tab/window), which localStorage is not (shared
  // across every tab of the same origin). This is required so a user can have the admin
  // portal open in Spanish in one tab and the front-end site in English in another, using
  // the same override mechanism, simultaneously.
  //
  // Keying: per user name, not a flat key - the portal supports switching between multiple
  // signed-in identities in the same browser (known-users list / "Sign in as another
  // user"). Without per-user keying, switching from user A to user B in the same tab would
  // incorrectly hand B user A's override. Each identity's override is independent; there is
  // deliberately no "current user" tracked here - the caller (DisplayManager, which knows
  // who is actually authenticated for this request) always passes the user name in.
  function overrideKey(userName) {
    return `crest-culture-override:${userName || ''}`;
  }

  function setSessionCultureOverride(userName, culture) {
    if (!culture) return;
    try {
      sessionStorage.setItem(overrideKey(userName), culture);
    } catch {}
  }

  function getSessionCultureOverride(userName) {
    try {
      return sessionStorage.getItem(overrideKey(userName));
    } catch {
      return null;
    }
  }

  function clearSessionCultureOverride(userName) {
    try {
      sessionStorage.removeItem(overrideKey(userName));
    } catch {}
  }

  function getBrowserLocale() {
    return navigator.language || null;
  }

  return {
    apply, toggleMode, isDarkMode, rememberSignedInUser, getKnownUsers,
    setAdminCulture, getSessionCulture,
    setSessionCultureOverride, getSessionCultureOverride, clearSessionCultureOverride,
    getBrowserLocale,
  };
})();
