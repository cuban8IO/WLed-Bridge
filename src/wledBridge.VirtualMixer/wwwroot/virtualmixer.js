// Virtual Mixer client helpers (ES module, imported lazily by components).
// Design rule: high-frequency pointer movement stays client-side (CSS transform / CSS custom
// property), the server only receives throttled updates plus one final commit - this keeps
// dragging smooth on Blazor Server where every event is a SignalR round trip.

let stylesInjected = false;

export function ensureStyles() {
    if (stylesInjected) return;
    stylesInjected = true;
    if (document.querySelector('link[data-virtualmixer]')) return;
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = '_content/wledBridge.VirtualMixer/virtualmixer.css';
    link.setAttribute('data-virtualmixer', '');
    document.head.appendChild(link);
}

// ---------------------------------------------------------------- resize observer

export function observeResize(element, dotnetRef) {
    const observer = new ResizeObserver(entries => {
        for (const entry of entries) {
            const rect = entry.contentRect;
            dotnetRef.invokeMethodAsync('OnContainerResized', rect.width, rect.height);
        }
    });
    observer.observe(element);
    return {
        dispose: () => observer.disconnect()
    };
}

export function getRect(element) {
    const r = element.getBoundingClientRect();
    return { left: r.left, top: r.top, width: r.width, height: r.height };
}

// ---------------------------------------------------------------- generic move/resize drag
// Used by the designer for control move and resize. The element (or a designated visual
// target) is moved via CSS transform during the drag; the server receives throttled
// OnDragMove(dx, dy) for snapline computation and one final OnDragEnd(dx, dy).

export function attachDrag(element, dotnetRef, options) {
    const opts = Object.assign({ scale: 1, throttleMs: 33, visual: 'none', visualTargetId: null }, options);
    let dragging = false;
    let moved = false;
    let startX = 0, startY = 0, lastSent = 0;

    const visualTarget = () =>
        opts.visualTargetId ? document.getElementById(opts.visualTargetId) : element;

    function onDown(e) {
        if (e.button !== 0) return;
        dragging = true;
        moved = false;
        startX = e.clientX;
        startY = e.clientY;
        element.setPointerCapture(e.pointerId);
        dotnetRef.invokeMethodAsync('OnDragStart', e.shiftKey, e.ctrlKey);
        e.preventDefault();
        e.stopPropagation();
    }

    function deltas(e) {
        return [(e.clientX - startX) / opts.scale, (e.clientY - startY) / opts.scale];
    }

    function onMove(e) {
        if (!dragging) return;
        const [dx, dy] = deltas(e);
        if (Math.abs(dx) > 2 || Math.abs(dy) > 2) moved = true;
        if (opts.visual === 'translate' && moved) {
            const t = visualTarget();
            if (t) t.style.transform = `translate(${dx * opts.scale}px, ${dy * opts.scale}px)`;
        }
        const now = performance.now();
        if (now - lastSent >= opts.throttleMs) {
            lastSent = now;
            dotnetRef.invokeMethodAsync('OnDragMove', dx, dy);
        }
        e.preventDefault();
    }

    function onUp(e) {
        if (!dragging) return;
        dragging = false;
        const [dx, dy] = deltas(e);
        if (opts.visual === 'translate') {
            const t = visualTarget();
            if (t) t.style.transform = '';
        }
        dotnetRef.invokeMethodAsync('OnDragEnd', dx, dy, moved);
        e.preventDefault();
    }

    element.addEventListener('pointerdown', onDown);
    element.addEventListener('pointermove', onMove);
    element.addEventListener('pointerup', onUp);
    element.addEventListener('pointercancel', onUp);

    return {
        dispose: () => {
            element.removeEventListener('pointerdown', onDown);
            element.removeEventListener('pointermove', onMove);
            element.removeEventListener('pointerup', onUp);
            element.removeEventListener('pointercancel', onUp);
        }
    };
}

// ---------------------------------------------------------------- fader drag
// Locally updates the '--vm-pos' custom property (0..1) for 60fps thumb tracking; the server
// receives throttled OnFaderPos(pos, false) and a final OnFaderPos(pos, true).

export function attachFaderDrag(element, dotnetRef, options) {
    const opts = Object.assign({ scale: 1, axis: 'y', throttleMs: 33 }, options);
    let dragging = false;
    let startPos = 0, startX = 0, startY = 0, trackPx = 1, lastSent = 0;

    function clamp01(v) { return Math.min(1, Math.max(0, v)); }

    function onDown(e) {
        if (e.button !== 0) return;
        dragging = true;
        startX = e.clientX;
        startY = e.clientY;
        startPos = parseFloat(getComputedStyle(element).getPropertyValue('--vm-pos')) || 0;
        trackPx = opts.axis === 'y' ? element.clientHeight : element.clientWidth;
        if (trackPx < 1) trackPx = 1;
        element.setPointerCapture(e.pointerId);
        e.preventDefault();
        e.stopPropagation();
    }

    function position(e) {
        const delta = opts.axis === 'y'
            ? (startY - e.clientY) / opts.scale / trackPx
            : (e.clientX - startX) / opts.scale / trackPx;
        return clamp01(startPos + delta);
    }

    function onMove(e) {
        if (!dragging) return;
        const pos = position(e);
        element.style.setProperty('--vm-pos', pos.toString());
        const now = performance.now();
        if (now - lastSent >= opts.throttleMs) {
            lastSent = now;
            dotnetRef.invokeMethodAsync('OnFaderPos', pos, false);
        }
        e.preventDefault();
    }

    function onUp(e) {
        if (!dragging) return;
        dragging = false;
        const pos = position(e);
        element.style.setProperty('--vm-pos', pos.toString());
        dotnetRef.invokeMethodAsync('OnFaderPos', pos, true);
        e.preventDefault();
    }

    element.addEventListener('pointerdown', onDown);
    element.addEventListener('pointermove', onMove);
    element.addEventListener('pointerup', onUp);
    element.addEventListener('pointercancel', onUp);

    return {
        dispose: () => {
            element.removeEventListener('pointerdown', onDown);
            element.removeEventListener('pointermove', onMove);
            element.removeEventListener('pointerup', onUp);
            element.removeEventListener('pointercancel', onUp);
        }
    };
}

// ---------------------------------------------------------------- knob drag
// Vertical dragging; sends cumulative logical-pixel movement: OnKnobDrag(totalDy, isFinal).

export function attachKnobDrag(element, dotnetRef, options) {
    const opts = Object.assign({ scale: 1, throttleMs: 33 }, options);
    let dragging = false;
    let startY = 0, lastSent = 0;

    function onDown(e) {
        if (e.button !== 0) return;
        dragging = true;
        startY = e.clientY;
        element.setPointerCapture(e.pointerId);
        dotnetRef.invokeMethodAsync('OnKnobDragStart');
        e.preventDefault();
        e.stopPropagation();
    }

    function onMove(e) {
        if (!dragging) return;
        const dy = (startY - e.clientY) / opts.scale;
        const now = performance.now();
        if (now - lastSent >= opts.throttleMs) {
            lastSent = now;
            dotnetRef.invokeMethodAsync('OnKnobDrag', dy, false);
        }
        e.preventDefault();
    }

    function onUp(e) {
        if (!dragging) return;
        dragging = false;
        const dy = (startY - e.clientY) / opts.scale;
        dotnetRef.invokeMethodAsync('OnKnobDrag', dy, true);
        e.preventDefault();
    }

    element.addEventListener('pointerdown', onDown);
    element.addEventListener('pointermove', onMove);
    element.addEventListener('pointerup', onUp);
    element.addEventListener('pointercancel', onUp);

    return {
        dispose: () => {
            element.removeEventListener('pointerdown', onDown);
            element.removeEventListener('pointermove', onMove);
            element.removeEventListener('pointerup', onUp);
            element.removeEventListener('pointercancel', onUp);
        }
    };
}

// ---------------------------------------------------------------- rubber band selection
// Attached to the designer surface; draws a client-side rectangle and reports the final
// logical-coordinate rect: OnRubberBand(x, y, width, height, shiftKey).

export function attachRubberBand(surface, dotnetRef, options) {
    const opts = Object.assign({ scale: 1 }, options);
    let dragging = false;
    let startX = 0, startY = 0;
    let band = null;

    function isEmptyArea(target) {
        return target === surface || target.classList?.contains('vm-group-content') ||
            target.classList?.contains('vm-group');
    }

    function onDown(e) {
        if (e.button !== 0 || !isEmptyArea(e.target)) return;
        dragging = true;
        const rect = surface.getBoundingClientRect();
        startX = e.clientX - rect.left;
        startY = e.clientY - rect.top;
        band = document.createElement('div');
        band.className = 'vm-rubberband';
        surface.appendChild(band);
        surface.setPointerCapture(e.pointerId);
        e.preventDefault();
    }

    function currentRect(e) {
        const rect = surface.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const y = e.clientY - rect.top;
        return {
            left: Math.min(startX, x),
            top: Math.min(startY, y),
            width: Math.abs(x - startX),
            height: Math.abs(y - startY)
        };
    }

    function onMove(e) {
        if (!dragging || !band) return;
        const r = currentRect(e);
        band.style.left = r.left + 'px';
        band.style.top = r.top + 'px';
        band.style.width = r.width + 'px';
        band.style.height = r.height + 'px';
        e.preventDefault();
    }

    function onUp(e) {
        if (!dragging) return;
        dragging = false;
        const r = currentRect(e);
        band?.remove();
        band = null;
        dotnetRef.invokeMethodAsync('OnRubberBand',
            r.left / opts.scale, r.top / opts.scale,
            r.width / opts.scale, r.height / opts.scale,
            e.shiftKey);
        e.preventDefault();
    }

    surface.addEventListener('pointerdown', onDown);
    surface.addEventListener('pointermove', onMove);
    surface.addEventListener('pointerup', onUp);
    surface.addEventListener('pointercancel', onUp);

    return {
        dispose: () => {
            surface.removeEventListener('pointerdown', onDown);
            surface.removeEventListener('pointermove', onMove);
            surface.removeEventListener('pointerup', onUp);
            surface.removeEventListener('pointercancel', onUp);
        }
    };
}

// ---------------------------------------------------------------- file download (export)

export function downloadText(fileName, contentType, content) {
    const blob = new Blob([content], { type: contentType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}
