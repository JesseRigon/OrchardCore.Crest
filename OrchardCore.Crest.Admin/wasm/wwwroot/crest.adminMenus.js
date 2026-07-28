window.crestAdminMenus = (() => {
    const instances = new WeakMap();
    const primaryNavMenuPinnedStateKey = 'crest.primary-nav-menu.pinned';

    function getPrimaryNavMenuPinnedState() {
        try {
            const value = window.sessionStorage.getItem(primaryNavMenuPinnedStateKey);
            return value === null ? null : value === 'true';
        } catch {
            return null;
        }
    }

    function setPrimaryNavMenuPinnedState(isPinned) {
        try {
            window.sessionStorage.setItem(primaryNavMenuPinnedStateKey, String(Boolean(isPinned)));
        } catch {
            // Storage can be unavailable in a privacy-restricted browser context.
        }
    }

    function clear(root) {
        root.querySelectorAll('.admin-menu-node--drop-before, .admin-menu-node--drop-after, .admin-menu-node--drop-inside')
            .forEach(element => element.classList.remove('admin-menu-node--drop-before', 'admin-menu-node--drop-after', 'admin-menu-node--drop-inside'));
    }

    function getDropIntent(event, item) {
        if (item.dataset.entryType === 'separator') {
            const rect = item.getBoundingClientRect();
            const offset = event.clientY - rect.top;
            return offset < rect.height / 2 ? 'before' : 'after';
        }

        const rect = item.getBoundingClientRect();
        const offset = event.clientY - rect.top;
        const ratio = rect.height === 0 ? 0.5 : offset / rect.height;

        if (ratio < 0.25) {
            return 'before';
        }

        if (ratio > 0.75) {
            return 'after';
        }

        return 'inside';
    }

    function siblingIndex(item) {
        const siblings = Array.from(item.parentElement?.children ?? [])
            .filter(element => element.classList.contains('admin-menu-tree__item'));

        return Math.max(0, siblings.indexOf(item));
    }

    function findParentId(item) {
        const parentItem = item.parentElement?.closest('.admin-menu-tree__item');
        return parentItem?.dataset.entryType === 'node' ? parentItem.dataset.entryId : null;
    }

    function init(rootOrSelector, dotNetRef) {
        const selector = typeof rootOrSelector === 'string' ? rootOrSelector : null;
        const listenerTarget = selector ? document : rootOrSelector;
        const getRoot = () => selector ? document.querySelector(selector) : rootOrSelector;

        if (!(listenerTarget instanceof EventTarget)) {
            return;
        }

        dispose(rootOrSelector);

        let currentTarget = null;
        let draggedEntryId = null;
        let draggedEntryType = null;
        let lastDragClientY = null;
        let autoScrollFrame = 0;
        const autoScrollBoundary = 72;
        const autoScrollMaxStep = 22;

        const clearDragging = root => root?.querySelectorAll('.admin-menu-node--dragging')
            .forEach(element => element.classList.remove('admin-menu-node--dragging'));

        const findScrollContainer = root => {
            let element = root;
            while (element instanceof Element) {
                const style = window.getComputedStyle(element);
                const overflowY = style.overflowY;
                if ((overflowY === 'auto' || overflowY === 'scroll') && element.scrollHeight > element.clientHeight) {
                    return element;
                }

                element = element.parentElement;
            }

            return document.scrollingElement || document.documentElement;
        };

        const stopAutoScroll = () => {
            if (autoScrollFrame) {
                window.cancelAnimationFrame(autoScrollFrame);
                autoScrollFrame = 0;
            }

            lastDragClientY = null;
        };

        const autoScrollStep = () => {
            const root = getRoot();
            if (!(root instanceof Element) || lastDragClientY === null || !draggedEntryId) {
                stopAutoScroll();
                return;
            }

            const container = findScrollContainer(root);
            const rect = container === document.scrollingElement || container === document.documentElement
                ? { top: 0, bottom: window.innerHeight }
                : container.getBoundingClientRect();
            let delta = 0;

            if (lastDragClientY < rect.top + autoScrollBoundary) {
                delta = -Math.ceil(autoScrollMaxStep * (1 - Math.max(0, lastDragClientY - rect.top) / autoScrollBoundary));
            } else if (lastDragClientY > rect.bottom - autoScrollBoundary) {
                delta = Math.ceil(autoScrollMaxStep * (1 - Math.max(0, rect.bottom - lastDragClientY) / autoScrollBoundary));
            }

            if (delta !== 0) {
                container.scrollTop += delta;
            }

            autoScrollFrame = window.requestAnimationFrame(autoScrollStep);
        };

        const startAutoScroll = () => {
            if (!autoScrollFrame) {
                autoScrollFrame = window.requestAnimationFrame(autoScrollStep);
            }
        };

        const onDragStart = event => {
            const root = getRoot();
            const handle = event.target.closest('.admin-menu-node__handle');
            const item = handle?.closest('.admin-menu-tree__item');
            if (!(root instanceof Element) || !item || !root.contains(item)) {
                return;
            }

            draggedEntryId = item.dataset.entryId || item.dataset.nodeId || null;
            draggedEntryType = item.dataset.entryType || 'node';
            if (event.dataTransfer && draggedEntryId) {
                event.dataTransfer.effectAllowed = 'move';
                event.dataTransfer.setData('text/plain', draggedEntryId);
                event.dataTransfer.setData('application/x-crest-admin-menu-entry-type', draggedEntryType);
            }

            item.classList.add('admin-menu-node--dragging');
            lastDragClientY = event.clientY;
            startAutoScroll();
        };

        const onDragOver = event => {
            const root = getRoot();
            const item = event.target.closest('.admin-menu-tree__item');
            if (!(root instanceof Element) || !draggedEntryId || !root.contains(event.target)) {
                return;
            }

            event.preventDefault();
            event.dataTransfer.dropEffect = 'move';
            lastDragClientY = event.clientY;
            startAutoScroll();

            if (!item || !root.contains(item)) {
                clear(root);
                currentTarget = null;
                return;
            }

            const intent = getDropIntent(event, item);
            if (currentTarget !== item) {
                clear(root);
                currentTarget = item;
            } else {
                item.classList.remove('admin-menu-node--drop-before', 'admin-menu-node--drop-after', 'admin-menu-node--drop-inside');
            }

            item.classList.add(`admin-menu-node--drop-${intent}`);
        };

        const onDragLeave = event => {
            const root = getRoot();
            if (root instanceof Element && !root.contains(event.relatedTarget)) {
                clear(root);
                currentTarget = null;
            }
        };

        const onDrop = event => {
            const root = getRoot();
            const item = event.target.closest('.admin-menu-tree__item');
            if (!(root instanceof Element) || !item || !root.contains(item)) {
                return;
            }

            event.preventDefault();
            const intent = getDropIntent(event, item);
            const targetNodeId = item.dataset.entryType === 'node' ? item.dataset.entryId : null;
            let parentNodeId = null;
            let position = 0;

            if (intent === 'inside') {
                parentNodeId = targetNodeId;
                position = 2147483647;
            } else {
                parentNodeId = findParentId(item);
                position = siblingIndex(item) + (intent === 'after' ? 1 : 0);
            }

            const entryToMoveId = draggedEntryId || event.dataTransfer?.getData('text/plain');
            const entryToMoveType = draggedEntryType || event.dataTransfer?.getData('application/x-crest-admin-menu-entry-type') || 'node';

            clear(root);
            clearDragging(root);
            currentTarget = null;
            draggedEntryId = null;
            draggedEntryType = null;
            stopAutoScroll();

            if (entryToMoveId) {
                if (entryToMoveType === 'separator') {
                    dotNetRef.invokeMethodAsync('OnMenuEntryDropped', entryToMoveId, entryToMoveType, parentNodeId, position);
                } else {
                    dotNetRef.invokeMethodAsync('OnMenuNodeDropped', entryToMoveId, parentNodeId, position);
                }
            }
        };

        const onDragEnd = () => {
            const root = getRoot();
            if (root instanceof Element) {
                clear(root);
                clearDragging(root);
            }
            currentTarget = null;
            draggedEntryId = null;
            draggedEntryType = null;
            stopAutoScroll();
        };

        listenerTarget.addEventListener('dragstart', onDragStart);
        listenerTarget.addEventListener('dragover', onDragOver);
        listenerTarget.addEventListener('dragleave', onDragLeave);
        listenerTarget.addEventListener('drop', onDrop);
        listenerTarget.addEventListener('dragend', onDragEnd);

        instances.set(listenerTarget, { onDragStart, onDragOver, onDragLeave, onDrop, onDragEnd });
    }

    function dispose(rootOrSelector) {
        const listenerTarget = typeof rootOrSelector === 'string' ? document : rootOrSelector;
        const root = typeof rootOrSelector === 'string' ? document.querySelector(rootOrSelector) : rootOrSelector;

        if (!(listenerTarget instanceof EventTarget)) {
            return;
        }

        const instance = instances.get(listenerTarget);
        if (!instance) {
            return;
        }

        listenerTarget.removeEventListener('dragstart', instance.onDragStart);
        listenerTarget.removeEventListener('dragover', instance.onDragOver);
        listenerTarget.removeEventListener('dragleave', instance.onDragLeave);
        listenerTarget.removeEventListener('drop', instance.onDrop);
        listenerTarget.removeEventListener('dragend', instance.onDragEnd);
        instances.delete(listenerTarget);
        if (root instanceof Element) {
            clear(root);
        }
    }

    function getPathAndQuery() {
        return `${window.location.pathname}${window.location.search}`;
    }

    return { init, dispose, getPathAndQuery, getPrimaryNavMenuPinnedState, setPrimaryNavMenuPinnedState };
})();
