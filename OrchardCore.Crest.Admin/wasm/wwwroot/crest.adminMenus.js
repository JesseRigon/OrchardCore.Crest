window.crestAdminMenus = (() => {
    const instances = new WeakMap();

    function clear(root) {
        root.querySelectorAll('.admin-menu-node--drop-before, .admin-menu-node--drop-after, .admin-menu-node--drop-inside')
            .forEach(element => element.classList.remove('admin-menu-node--drop-before', 'admin-menu-node--drop-after', 'admin-menu-node--drop-inside'));
    }

    function getDropIntent(event, item) {
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
        return item.parentElement?.closest('.admin-menu-tree__item')?.dataset.nodeId || null;
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
        let draggedNodeId = null;

        const clearDragging = root => root?.querySelectorAll('.admin-menu-node--dragging')
            .forEach(element => element.classList.remove('admin-menu-node--dragging'));

        const onDragStart = event => {
            const root = getRoot();
            const handle = event.target.closest('.admin-menu-node__handle');
            const item = handle?.closest('.admin-menu-tree__item');
            if (!(root instanceof Element) || !item || !root.contains(item)) {
                return;
            }

            draggedNodeId = item.dataset.nodeId || null;
            if (event.dataTransfer && draggedNodeId) {
                event.dataTransfer.effectAllowed = 'move';
                event.dataTransfer.setData('text/plain', draggedNodeId);
            }

            item.classList.add('admin-menu-node--dragging');
        };

        const onDragOver = event => {
            const root = getRoot();
            const item = event.target.closest('.admin-menu-tree__item');
            if (!(root instanceof Element) || !item || !root.contains(item)) {
                return;
            }

            event.preventDefault();
            event.dataTransfer.dropEffect = 'move';

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
            const targetNodeId = item.dataset.nodeId;
            let parentNodeId = null;
            let position = 0;

            if (intent === 'inside') {
                parentNodeId = targetNodeId;
                position = 2147483647;
            } else {
                parentNodeId = findParentId(item);
                position = siblingIndex(item) + (intent === 'after' ? 1 : 0);
            }

            const nodeToMoveId = draggedNodeId || event.dataTransfer?.getData('text/plain');

            clear(root);
            clearDragging(root);
            currentTarget = null;
            draggedNodeId = null;

            if (nodeToMoveId) {
                dotNetRef.invokeMethodAsync('OnMenuNodeDropped', nodeToMoveId, parentNodeId, position);
            }
        };

        const onDragEnd = () => {
            const root = getRoot();
            if (root instanceof Element) {
                clear(root);
                clearDragging(root);
            }
            currentTarget = null;
            draggedNodeId = null;
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

    function syncSidebarActiveState(rootSelector = '.admin-menu-sidebar') {
        const root = document.querySelector(rootSelector);
        if (!(root instanceof Element)) {
            return;
        }

        root.querySelectorAll('.admin-menu-sidebar__item-content--not-active').forEach(content => {
            const wrapper = content.closest('.rz-navigation-item-wrapper');
            const link = content.closest('.rz-navigation-item-link');
            wrapper?.classList.remove('rz-navigation-item-wrapper-active');
            link?.classList.remove('rz-navigation-item-link-active', 'active');
            link?.removeAttribute('aria-current');
        });

        root.querySelectorAll('.admin-menu-sidebar__item-content--active').forEach(content => {
            const wrapper = content.closest('.rz-navigation-item-wrapper');
            const link = content.closest('.rz-navigation-item-link');
            wrapper?.classList.add('rz-navigation-item-wrapper-active');
            link?.classList.add('rz-navigation-item-link-active', 'active');
            link?.setAttribute('aria-current', 'page');
        });
    }

    return { init, dispose, getPathAndQuery, syncSidebarActiveState };
})();
