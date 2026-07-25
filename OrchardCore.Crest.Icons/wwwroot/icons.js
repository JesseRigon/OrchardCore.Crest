window.crestIconSelector = (() => {
  function getColumnCount(element) {
    if (!element) return 10;
    const styles = getComputedStyle(element);
    const template = styles.gridTemplateColumns || '';
    const columns = template
      .split(' ')
      .map(value => value.trim())
      .filter(Boolean)
      .length;
    return Math.max(1, columns || 10);
  }

  function getMetrics(element) {
    if (!element) return { columns: 10, remainingItems: 0 };
    const columns = getColumnCount(element);
    const firstItem = element.querySelector('.icon-selector__item');
    const styles = getComputedStyle(element);
    const gap = parseFloat(styles.rowGap || styles.gap || '0') || 0;
    const rowHeight = firstItem ? firstItem.getBoundingClientRect().height + gap : 104;
    const remainingPixels = element.scrollHeight - element.scrollTop - element.clientHeight;
    const remainingItems = Math.ceil(Math.max(0, remainingPixels) / Math.max(1, rowHeight)) * columns;
    return { columns, remainingItems };
  }

  function remainingItems(element, columns) {
    const metrics = getMetrics(element);
    return Math.ceil(metrics.remainingItems / Math.max(1, metrics.columns)) * Math.max(1, columns || metrics.columns);
  }

  function enableHorizontalWheel(element) {
    if (!element || element.dataset.horizontalWheel === 'true') return;
    element.dataset.horizontalWheel = 'true';
    element.addEventListener('wheel', event => {
      if (Math.abs(event.deltaY) <= Math.abs(event.deltaX)) return;
      if (element.scrollWidth <= element.clientWidth) return;
      element.scrollLeft += event.deltaY;
      event.preventDefault();
    }, { passive: false });
  }

  function registerDialogKeyboard(element, dotNetRef) {
    if (!element) return;
    unregisterDialogKeyboard(element);

    const handler = event => {
      if (event.key === 'Escape') {
        event.preventDefault();
        event.stopPropagation();
        dotNetRef.invokeMethodAsync('CloseFromKeyboardAsync');
        return;
      }

      if (event.key !== 'Enter') return;

      const saveButton = event.target?.closest?.('[data-icon-selector-save="true"]');
      if (saveButton && !saveButton.disabled && !saveButton.getAttribute('aria-disabled')) {
        return;
      }

      event.preventDefault();
      event.stopPropagation();
    };

    element.__crestIconSelectorKeyboard = handler;
    element.addEventListener('keydown', handler, true);
  }

  function unregisterDialogKeyboard(element) {
    const handler = element?.__crestIconSelectorKeyboard;
    if (!element || !handler) return;
    element.removeEventListener('keydown', handler, true);
    delete element.__crestIconSelectorKeyboard;
  }

  return { getMetrics, remainingItems, enableHorizontalWheel, registerDialogKeyboard, unregisterDialogKeyboard };
})();
