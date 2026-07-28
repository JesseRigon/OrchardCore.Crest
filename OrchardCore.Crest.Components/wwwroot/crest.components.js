window.crestComponents = window.crestComponents || {};

const popupWindowBindings = new WeakMap();

window.crestComponents.positionPopup = (element, anchorElement, options) => {
    if (!(element instanceof HTMLElement)) {
        return;
    }

    const anchors = getPopupAnchors(anchorElement, options);
    const anchor = anchors.anchor;
    const flipAboveAnchor = anchors.flipAboveAnchor ?? anchor;
    const offsetX = Number(options?.offsetX ?? 12);
    const offsetY = Number(options?.offsetY ?? 8);
    const margin = Number(options?.margin ?? 8);
    const windowAware = options?.windowAware !== false;
    const viewportWidth = window.innerWidth || document.documentElement.clientWidth || 0;
    const viewportHeight = window.innerHeight || document.documentElement.clientHeight || 0;

    element.style.position = 'fixed';
    element.style.maxHeight = '';
    element.style.overflowY = '';

    const rect = element.getBoundingClientRect();
    const belowTop = anchor.y + offsetY;
    const aboveTop = flipAboveAnchor.y - rect.height - offsetY;
    const shouldOpenAbove = windowAware && belowTop + rect.height > viewportHeight - margin && aboveTop >= margin;
    const unclampedTop = shouldOpenAbove ? aboveTop : belowTop;
    const top = windowAware
        ? Math.min(Math.max(margin, unclampedTop), Math.max(margin, viewportHeight - margin - rect.height))
        : unclampedTop;

    const maxHeight = shouldOpenAbove
        ? Math.max(80, flipAboveAnchor.y - offsetY - margin)
        : Math.max(80, viewportHeight - top - margin);

    const unclampedLeft = anchor.x + offsetX;
    const left = windowAware
        ? Math.min(Math.max(margin, unclampedLeft), Math.max(margin, viewportWidth - margin - rect.width))
        : unclampedLeft;

    element.style.left = `${left}px`;
    element.style.top = `${top}px`;
    element.style.maxHeight = windowAware ? `${maxHeight}px` : '';
    element.style.overflowY = windowAware ? 'auto' : '';
    element.dataset.crestPopupPlacement = shouldOpenAbove ? 'right-above' : 'right-below';

    updatePopupWindowBinding(element, anchorElement, options, windowAware);
};

window.crestComponents.scrollIntoViewIfNeeded = (id) => {
    const el = document.getElementById(id);
    if (!el) {
        return;
    }

    if (typeof el.scrollIntoViewIfNeeded === 'function') {
        el.scrollIntoViewIfNeeded();
    } else if (typeof el.scrollIntoView === 'function') {
        el.scrollIntoView({ block: 'nearest' });
    }
};

window.crestComponents.disposePopup = (element) => {
    const binding = popupWindowBindings.get(element);
    if (!binding) {
        return;
    }

    window.removeEventListener('resize', binding.onResize);
    popupWindowBindings.delete(element);
};

function updatePopupWindowBinding(element, anchorElement, options, windowAware) {
    const existing = popupWindowBindings.get(element);
    if (!windowAware) {
        if (existing) {
            window.removeEventListener('resize', existing.onResize);
            popupWindowBindings.delete(element);
        }
        return;
    }

    if (existing) {
        existing.anchorElement = anchorElement;
        existing.options = options;
        return;
    }

    const binding = { anchorElement, options, frame: 0, onResize: null };
    binding.onResize = () => {
        if (binding.frame) {
            cancelAnimationFrame(binding.frame);
        }

        binding.frame = requestAnimationFrame(() => {
            binding.frame = 0;
            window.crestComponents.positionPopup(element, binding.anchorElement, binding.options);
        });
    };

    popupWindowBindings.set(element, binding);
    window.addEventListener('resize', binding.onResize, { passive: true });
}

function getPopupAnchors(anchorElement, options) {
    const mode = String(options?.anchorMode || 'Point').toLowerCase();
    if (mode === 'element') {
        const element = anchorElement instanceof HTMLElement
            ? anchorElement
            : getElementFromOptions(options);

        if (element instanceof HTMLElement) {
            const rect = element.getBoundingClientRect();
            return {
                anchor: getAnchorPoint(rect, String(options?.anchorPoint || 'BottomRight')),
                flipAboveAnchor: options?.flipAboveAnchorPoint
                    ? getAnchorPoint(rect, String(options.flipAboveAnchorPoint))
                    : null,
            };
        }
    }

    const point = {
        x: Number(options?.anchorX ?? 0),
        y: Number(options?.anchorY ?? 0),
    };

    return {
        anchor: point,
        flipAboveAnchor: point,
    };
}

function getElementFromOptions(options) {
    if (options?.anchorSelector) {
        return getElementFromSelector(options.anchorSelector);
    }

    if (options?.anchorId) {
        return document.getElementById(String(options.anchorId));
    }

    if (options?.anchorClass) {
        return document.getElementsByClassName(String(options.anchorClass))[0] ?? null;
    }

    return null;
}

function getElementFromSelector(selector) {
    if (!selector || typeof selector !== 'string') {
        return null;
    }

    try {
        return document.querySelector(selector);
    } catch {
        return null;
    }
}

function getAnchorPoint(rect, anchorPoint) {
    switch (anchorPoint.toLowerCase()) {
        case 'topleft':
            return { x: rect.left, y: rect.top };
        case 'topcenter':
            return { x: rect.left + rect.width / 2, y: rect.top };
        case 'topright':
            return { x: rect.right, y: rect.top };
        case 'middleleft':
            return { x: rect.left, y: rect.top + rect.height / 2 };
        case 'center':
            return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
        case 'middleright':
            return { x: rect.right, y: rect.top + rect.height / 2 };
        case 'bottomleft':
            return { x: rect.left, y: rect.bottom };
        case 'bottomcenter':
            return { x: rect.left + rect.width / 2, y: rect.bottom };
        case 'bottomright':
        default:
            return { x: rect.right, y: rect.bottom };
    }
}
