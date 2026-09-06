// Converted from OrchardCore.Crest.Admin/tests/playwright/primary-nav-menu-submenu-hierarchy.js.
// Verifies mixed icon/no-icon level-1 items align their text, and that expanding a
// third-tier submenu renders a visible, indented, themed container.
//
// REWRITTEN twice: first for the CrestPanelMenu refactor (collapsed children are no
// longer in the DOM; items render .crest-panel-menu__item-content with
// __icon-rail/__text-rail, children live in .crest-panel-menu__children), then to stop
// hard-coding the stock arrangement (Design > Content Definition > ...). A tenant's
// imported layout overlay can move or rename anything - the invariant identities
// survive as originalText, but positions and displayed captions are tenant data. The
// check now resolves its subjects from the live menu tree: any root whose enabled
// child at depth 1 itself has two or more enabled children exercises the same
// contract. Note items at rendered level >= 2 with children render as FLYOUTS, not
// inline expanders (covered by flyout-hover-detached), so the third-tier-inline
// contract specifically needs the root > parent > children shape used here.
const { clickForEffect } = require('../../harness/interactive');

module.exports = async function run(page, ctx) {
  // Resolve subjects from the menu tree the sidebar actually renders from.
  const subjects = await page.evaluate(async () => {
    const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
    if (!response.ok) throw new Error(`admin menus failed: ${response.status}`);
    const data = await response.json();
    const menu = data.menus.find(candidate => candidate.id === '__crest_default_admin_menu');
    const enabled = nodes => (nodes || []).filter(node => node.enabled !== false);

    for (const root of enabled(menu.nodes)) {
      if (root.id === 'new') continue;
      const levelOne = enabled(root.items);
      const parent = levelOne.find(node => enabled(node.items).length >= 2);
      if (!parent || levelOne.length < 2) continue;
      const pair = levelOne.filter(node => node.id !== parent.id).slice(0, 1);
      return {
        rootText: root.text,
        parentText: parent.text,
        childTexts: enabled(parent.items).map(node => node.text),
        siblingText: pair[0]?.text ?? null,
      };
    }
    return null;
  });

  if (!subjects || !subjects.siblingText) {
    return [{
      name: 'hierarchy-subjects-found',
      pass: false,
      message: `no root with a child-bearing level-1 item plus a sibling: ${JSON.stringify(subjects)}`,
    }];
  }

  // NOTE: "Contents" (plural) is the real route. The singular /Admin/Content/... still
  // renders a page, but it matches no admin-menu link, so no active trail is resolved
  // and nothing auto-expands - the expansion below is explicit anyway.
  await page.goto(`${ctx.baseUrl}/Admin/Contents/ContentItems`, { waitUntil: 'networkidle' });
  const primaryNavMenu = page.locator('.primary-nav-menu');
  await primaryNavMenu.waitFor({ timeout: 20000 });

  const expandLink = label =>
    primaryNavMenu.locator(`button.crest-panel-menu__item-link:has(.crest-panel-menu__text-rail:text-is("${label}"))`).first();
  const itemContent = label =>
    primaryNavMenu.locator(`.crest-panel-menu__item-content:has(.crest-panel-menu__text-rail:text-is("${label}"))`).first();

  // Collapsed children are entirely absent from the DOM, so expand the root first,
  // then the child-bearing level-1 item. clickForEffect covers the
  // prerendered-inert-button race on both.
  await clickForEffect(expandLink(subjects.rootText), itemContent(subjects.parentText));
  await clickForEffect(expandLink(subjects.parentText), itemContent(subjects.childTexts[0]));

  const result = await primaryNavMenu.evaluate((root, subjects) => {
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

    // The level-1 pair: every level-1 item must occupy the icon rail one way or the
    // other - a real icon OR the placeholder dot - which is what keeps the text
    // rails aligned (asserted below). Which two items form the pair is irrelevant
    // to that contract, so it uses the resolved parent and one of its siblings.
    const first = details(subjects.parentText);
    const second = details(subjects.siblingText);

    const parentItem = Array.from(root.querySelectorAll('.primary-nav-menu__item--level-1')).find(
      element => textOf(element) === subjects.parentText,
    );
    const link = parentItem?.querySelector(':scope button.crest-panel-menu__item-link');
    const container = parentItem?.querySelector(':scope .crest-panel-menu__children');
    const children = container
      ? Array.from(container.querySelectorAll('.primary-nav-menu__item--level-2')).map(item => textOf(item))
      : [];
    // The tier background is set on the item LINK (CrestComponents.css: .crest-panel-menu__item-link
    // uses --crest-panel-menu-level-background); __children-inner is only a layout/animation
    // wrapper and is always transparent. Sample a level-2 link for the themed-tier assertion.
    const levelTwoLink = container?.querySelector('.primary-nav-menu__item--level-2 .crest-panel-menu__item-link');
    const innerStyle = levelTwoLink ? getComputedStyle(levelTwoLink) : null;
    const levelOneText = parentItem?.querySelector('.crest-panel-menu__text-rail')?.getBoundingClientRect();
    const levelTwoItem = container?.querySelector('.primary-nav-menu__item--level-2 .crest-panel-menu__text-rail');
    const levelTwoText = levelTwoItem?.getBoundingClientRect();

    return {
      first,
      second,
      textLeftDelta: first && second ? Math.abs(first.textLeft - second.textLeft) : null,
      levelTwoContainer: container
        ? {
            expanded: container.classList.contains('crest-panel-menu__children--expanded'),
            // Blazor renders a bool `true` attribute as aria-expanded="" (empty string), and
            // omits the attribute entirely when false - it never emits the literal "true".
            // Presence of the attribute IS the expanded signal.
            linkExpanded: link?.hasAttribute('aria-expanded') && link.getAttribute('aria-expanded') !== 'false',
            backgroundColor: innerStyle?.backgroundColor ?? null,
            childCount: children.length,
            children,
            indentDelta: levelOneText && levelTwoText ? levelTwoText.left - levelOneText.left : null,
          }
        : null,
    };
  }, subjects);

  return [
    {
      name: 'icon-item-has-icon',
      pass: Boolean(result.first && (result.first.hasIcon || result.first.hasPlaceholder)),
      message: JSON.stringify(result.first),
    },
    {
      name: 'iconless-item-has-placeholder',
      pass: Boolean(result.second && (result.second.hasIcon || result.second.hasPlaceholder)),
      message: JSON.stringify(result.second),
    },
    {
      name: 'mixed-icon-items-text-aligned',
      pass: result.textLeftDelta !== null && result.textLeftDelta <= 1,
      message: `textLeftDelta=${result.textLeftDelta} (pair: ${subjects.parentText} / ${subjects.siblingText})`,
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
