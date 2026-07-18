window.blazingIconSelector = (() => {
  function remainingItems(element, columns) {
    if (!element) return 0;
    const firstItem = element.querySelector('.icon-selector__item');
    const styles = getComputedStyle(element);
    const gap = parseFloat(styles.rowGap || styles.gap || '0') || 0;
    const rowHeight = firstItem ? firstItem.getBoundingClientRect().height + gap : 104;
    const remainingPixels = element.scrollHeight - element.scrollTop - element.clientHeight;
    return Math.ceil(Math.max(0, remainingPixels) / Math.max(1, rowHeight)) * columns;
  }

  return { remainingItems };
})();
