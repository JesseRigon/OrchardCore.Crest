// Drag-and-drop reordering for the Content Part Lists grid - the flat-list sibling of
// crest.adminMenus.js (same split: browser drag events in JS, typed callback into
// Blazor). No nesting: the only intent is before/after by row half-height.
window.crestContentPartLists = (() => {
    let dotNetRef = null;
    let rootSelector = null;
    let draggedKey = null;

    const getRoot = () => (rootSelector ? document.querySelector(rootSelector) : null);

    const rowOf = element => element?.closest?.('tr, .rz-data-row');

    const handleIn = row => row?.querySelector?.('[data-option-key]');

    const rows = root => Array.from(root.querySelectorAll('[data-option-key]'))
        .map(handle => rowOf(handle))
        .filter(Boolean);

    const clear = root => root.querySelectorAll('.option-row--drop-before, .option-row--drop-after')
        .forEach(element => element.classList.remove('option-row--drop-before', 'option-row--drop-after'));

    const isAfter = (event, row) => {
        const rect = row.getBoundingClientRect();
        return rect.height !== 0 && (event.clientY - rect.top) / rect.height > 0.5;
    };

    const onDragStart = event => {
        const handle = event.target.closest?.('[data-option-key]');
        if (!handle || handle.getAttribute('draggable') !== 'true') {
            return;
        }
        draggedKey = handle.dataset.optionKey;
        event.dataTransfer?.setData('text/plain', draggedKey);
        if (event.dataTransfer) {
            event.dataTransfer.effectAllowed = 'move';
        }
    };

    const onDragOver = event => {
        const root = getRoot();
        if (!(root instanceof Element) || !draggedKey) {
            return;
        }
        const row = rowOf(event.target);
        if (!row || !root.contains(row) || !handleIn(row)) {
            return;
        }
        event.preventDefault();
        clear(root);
        row.classList.add(isAfter(event, row) ? 'option-row--drop-after' : 'option-row--drop-before');
    };

    const onDrop = event => {
        const root = getRoot();
        if (!(root instanceof Element)) {
            return;
        }
        const row = rowOf(event.target);
        const key = draggedKey || event.dataTransfer?.getData('text/plain');
        clear(root);
        draggedKey = null;
        if (!row || !root.contains(row) || !handleIn(row) || !key) {
            return;
        }
        event.preventDefault();
        const all = rows(root);
        const index = all.indexOf(row) + (isAfter(event, row) ? 1 : 0);
        dotNetRef?.invokeMethodAsync('OnOptionDropped', key, index);
    };

    const onDragEnd = () => {
        const root = getRoot();
        if (root instanceof Element) {
            clear(root);
        }
        draggedKey = null;
    };

    return {
        init(selector, reference) {
            rootSelector = selector;
            dotNetRef = reference;
            document.addEventListener('dragstart', onDragStart);
            document.addEventListener('dragover', onDragOver);
            document.addEventListener('drop', onDrop);
            document.addEventListener('dragend', onDragEnd);
        },
        dispose() {
            document.removeEventListener('dragstart', onDragStart);
            document.removeEventListener('dragover', onDragOver);
            document.removeEventListener('drop', onDrop);
            document.removeEventListener('dragend', onDragEnd);
            dotNetRef = null;
            rootSelector = null;
            draggedKey = null;
        },
    };
})();
