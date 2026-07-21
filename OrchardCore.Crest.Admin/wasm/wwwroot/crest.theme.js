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
      themeLink.href = `/_content/Radzen.Blazor/css/${safeTheme}.css`;
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

  return { apply, toggleMode, isDarkMode, rememberSignedInUser, getKnownUsers };
})();
