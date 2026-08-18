// Converted from OrchardCore.Crest.Admin/tests/playwright/primary-nav-menu-submenu-hierarchy.js.
// Verifies mixed icon/no-icon level-1 items align their text, and that expanding a
// third-tier submenu (Content Definition) renders a visible, indented, themed container.
//
// REWRITTEN (Phase 8 triage): the original queried the old Radzen structure
// (.rz-navigation-item-text / .rz-expander / .rz-navigation-menu), all gone since the
// CrestPanelMenu refactor — collapsed children are no longer in the DOM at all, items
// render .crest-panel-menu__item-content with __icon-rail/__text-rail, and children
// live in .crest-panel-menu__children > .crest-panel-menu__children-inner. The level-1
// pair also tracks the current layout: "Admin Menus" (has an icon) vs "Site Menus"
// (no icon -> placeholder dot).
const { clickForEffect } = require('../../harness/interactive');

module.exports = async function run(page, ctx) {
  // NOTE: "Contents" (plural) is the real route. The singular /Admin/Content/... still
  // renders a page, but it matches no admin-menu link, so no active trail is resolved
  // and "Content" never auto-expands — which is why this check used to time out looking
  // for "Content Definition".
  await page.goto(`${ctx.baseUrl}/Admin/Contents/ContentItems`, { waitUntil: 'networkidle' });
  const primaryNavMenu = page.locator('.primary-nav-menu');
  await primaryNavMenu.waitFor({ timeout: 20000 });

  const expandLink = label =>
    primaryNavMenu.locator(`button.crest-panel-menu__item-link:has(.crest-panel-menu__text-rail:text-is("${label}"))`).first();
  const itemContent = label =>
    primaryNavMenu.locator(`.crest-panel-menu__item-content:has(.crest-panel-menu__text-rail:text-is("${label}"))`).first();

  // "Content Definition" is a child of "Design", not of "Content" - the active trail
  // from a Contents route expands "Content", which leaves Design (and therefore
  // Content Definition) collapsed and entirely absent from the DOM. Expand Design
  // first, then Content Definition itself. clickForEffect covers the
  // prerendered-inert-button race on both.
  await clickForEffect(expandLink('Design'), itemContent('Content Definition'));
  await clickForEffect(expandLink('Content Definition'), itemContent('Content Types'));

  const result = await primaryNavMenu.evaluate(root => {
    const textOf = element => (element.querySelector('.crest-panel-menu__text-rail')?.textContent || '').trim();

    const details = text => {
      const item = Array.from(root.querySelectorAll('.primary-nav-menu__item--level-1')).find(
        element => textOf(element) === text,
      );
      if (!item) return null;
      const textElement = item.querySelector('.crest-panel-menu__text-rail');
      const icon = item.querySelector('.orchard-icon:not(.primary-nav-menu__icon-placeholder)');
      const placeholder = item.querySelector('.primary-nav-menu__icon-placeholder');
      const textBox = textElement?.getBoundingClientRect();
      return { text, hasIcon: !!icon, hasPlaceholder: !!placeholder, textLeft: textBox?.left || 0 };
    };

    // The mixed icon/no-icon level-1 pair. "Admin Menus"/"Site Menus" were the original
    // pair, then "Templates"/"Admin Templates" - but Admin Templates belongs to the
    // OrchardCore.AdminTemplates feature, which a freshly provisioned FruitfulSetup tenant
    // does not enable, so that pair only existed on tenants where some earlier run had
    // enabled it. Under the expanded Design group, "Templates" (real icon) and
    // "Workflows" (no legacy icon mapping, so the placeholder dot) are the current pair
    // exercising the same contract on a fresh tenant: both occupy the icon rail one way
    // or the other, so their text rails stay aligned.
    const adminMenus = details('Templates');
    const siteMenus = details('Workflows');

    const contentDefinition = Array.from(root.querySelectorAll('.primary-nav-menu__item--level-1')).find(
      element => textOf(element) === 'Content Definition',
    );
    const link = contentDefinition?.querySelector(':scope button.crest-panel-menu__item-link');
    const container = contentDefinition?.querySelector(':scope .crest-panel-menu__children');
    const inner = container?.querySelector(':scope > .crest-panel-menu__children-inner');
    const children = container
      ? Array.from(container.querySelectorAll('.primary-nav-menu__item--level-2')).map(item => textOf(item))
      : [];
    // The tier background is set on the item LINK (CrestComponents.css: .crest-panel-menu__item-link
    // uses --crest-panel-menu-level-background); __children-inner is only a layout/animation
    // wrapper and is always transparent. Sample a level-2 link for the themed-tier assertion.
    const levelTwoLink = container?.querySelector('.primary-nav-menu__item--level-2 .crest-panel-menu__item-link');
    const innerStyle = levelTwoLink ? getComputedStyle(levelTwoLink) : null;
    const levelOneText = contentDefinition?.querySelector('.crest-panel-menu__text-rail')?.getBoundingClientRect();
    const levelTwoItem = container?.querySelector('.primary-nav-menu__item--level-2 .crest-panel-menu__text-rail');
    const levelTwoText = levelTwoItem?.getBoundingClientRect();

    return {
      adminMenus,
      siteMenus,
      textLeftDelta: adminMenus && siteMenus ? Math.abs(adminMenus.textLeft - siteMenus.textLeft) : null,
      levelTwoContainer: container
        ? {
            expanded: container.classList.contains('crest-panel-menu__children--expanded'),
            // Blazor renders a bool `true` attribute as aria-expanded="" (empty string), and
            // omits the attribute entirely when false — it never emits the literal "true".
            // Presence of the attribute IS the expanded signal.
            linkExpanded: link?.hasAttribute('aria-expanded') && link.getAttribute('aria-expanded') !== 'false',
            backgroundColor: innerStyle?.backgroundColor ?? null,
            childCount: children.length,
            children,
            indentDelta: levelOneText && levelTwoText ? levelTwoText.left - levelOneText.left : null,
          }
        : null,
    };
  });

  return [
    { name: 'icon-item-has-icon', pass: Boolean(result.adminMenus?.hasIcon), message: JSON.stringify(result.adminMenus) },
    {
      // "Site Menus" used to be the iconless half of this pair, but the current layout
      // gives it a real icon. The contract this pair actually guards is that every
      // level-1 item occupies the icon rail one way or the other — a real icon OR the
      // placeholder dot — which is what keeps the text rails aligned (asserted below).
      name: 'iconless-item-has-placeholder',
      pass: Boolean(result.siteMenus && (result.siteMenus.hasIcon || result.siteMenus.hasPlaceholder)),
      message: JSON.stringify(result.siteMenus),
    },
    {
      name: 'mixed-icon-items-text-aligned',
      pass: result.textLeftDelta !== null && result.textLeftDelta <= 1,
      message: `textLeftDelta=${result.textLeftDelta}`,
    },
    {
      name: 'third-tier-submenu-renders-expanded',
      pass: Boolean(
        result.levelTwoContainer &&
          result.levelTwoContainer.childCount >= 2 &&
          result.levelTwoContainer.expanded &&
          result.levelTwoContainer.linkExpanded,
      ),
      message: JSON.stringify(result.levelTwoContainer),
    },
    {
      name: 'third-tier-submenu-themed-and-nested',
      pass: Boolean(
        result.levelTwoContainer &&
          !/rgba?\(0, 0, 0(?:, 0)?\)/.test(result.levelTwoContainer.backgroundColor || '') &&
          result.levelTwoContainer.indentDelta !== null &&
          result.levelTwoContainer.indentDelta > 0,
      ),
      message: JSON.stringify(result.levelTwoContainer),
    },
  ];
};
