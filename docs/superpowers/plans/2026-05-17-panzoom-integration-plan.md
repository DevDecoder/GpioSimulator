# Implementation Plan: Pan & Zoom Integration via anvaka/panzoom

This plan outlines integrating `anvaka/panzoom` via CDN ES module to deliver a premium, fluid canvas workspace for the DevDecoder GPIO Simulator.

---

## 1. Objectives

1. **Immersive Workspace Canvas**: Allow the board to exceed physical workspace size with full pan/drag and mousewheel zoom — no native scrollbars.
2. **CDN-based / NuGet-compatible**: Load `anvaka/panzoom` dynamically via `esm.sh`, keeping the NuGet payload zero-weight.
3. **Simulation Tool Integration**: Bind panning to toolbar state — left-click only pans when the **Move** tool is active; **Pointer** tool passes clicks through to pin hotspots normally.
4. **Auto-Zoom & Floating Controls**: Auto-fit board on load; add a floating glassmorphic zoom pill (`+`, `−`, fit-to-screen) in the bottom-right corner of the board workspace.

---

## 2. Library Evaluation

**anvaka/panzoom** (`https://esm.sh/panzoom@9.4.0`):
- Lightweight (~8 KB gzipped), zero dependencies.
- Works on any DOM element via `panzoom(element, options)`.
- Exposes `beforeMouseDown(e)` filter to selectively block panning (essential for tool-gated behaviour).
- `smoothZoom(cx, cy, multiplier)` and `smoothZoomAbs(cx, cy, scale)` for programmatic smooth zoom.
- `getTransform()` returns `{ scale, x, y }` for fit-to-screen calculations.
- `moveTo(x, y)` + `zoomAbs(x, y, scale)` for instant positioning.
- Actively maintained (~3k GitHub stars), no known conflicts with Golden Layout DOM operations.

---

## 3. Technical Design

### 3.1 CSS Changes (`style.css`)

Make `#board-container` the viewport clip boundary; `#visual-board` becomes a freely positioned canvas inside it:

```css
#board-container {
    position: relative;
    overflow: hidden;  /* clip to viewport bounds */
    /* keep existing border, background, glassmorphic shadow */
}

#visual-board {
    position: absolute;
    transform-origin: 0 0;  /* matches panzoom's coordinate system */
    /* width/height set by JS from schema dimensions */
}

/* Floating Zoom Controls Pill */
#zoom-controls {
    position: absolute;
    bottom: 16px;
    right: 16px;
    background: rgba(18, 22, 28, 0.85);
    backdrop-filter: blur(12px);
    border: 1px solid var(--border-color);
    border-radius: 8px;
    padding: 4px;
    display: flex;
    gap: 4px;
    z-index: 100;
    box-shadow: 0 4px 16px rgba(0,0,0,0.5);
}

.zoom-btn {
    background: none;
    border: none;
    color: var(--text-color);
    width: 32px;
    height: 32px;
    border-radius: 6px;
    display: flex;
    justify-content: center;
    align-items: center;
    cursor: pointer;
    transition: all 0.2s;
}

.zoom-btn:hover  { background: rgba(255,255,255,0.1); color: var(--accent); }
.zoom-btn:active { transform: scale(0.95); }
```

### 3.2 HTML Changes (`index.html`)

Add `#zoom-controls` inside `#board-container`:

```html
<div id="board-container">
    <div id="canvas-toolbar">...</div>
    <div id="visual-board"></div>

    <div id="zoom-controls">
        <button id="zoom-in"  class="zoom-btn" title="Zoom In">
            <svg viewBox="0 0 24 24" width="16" height="16">
                <path fill="currentColor" d="M19,13H13V19H11V13H5V11H11V5H13V11H19V13Z"/>
            </svg>
        </button>
        <button id="zoom-out" class="zoom-btn" title="Zoom Out">
            <svg viewBox="0 0 24 24" width="16" height="16">
                <path fill="currentColor" d="M19,13H5V11H19V13Z"/>
            </svg>
        </button>
        <button id="zoom-fit" class="zoom-btn" title="Fit to Screen">
            <svg viewBox="0 0 24 24" width="16" height="16">
                <path fill="currentColor" d="M4,4H10V6H6V10H4V4M20,4V10H18V6H14V4H20M4,20V14H6V18H10V20H4M20,20H14V18H18V14H20V20Z"/>
            </svg>
        </button>
    </div>
</div>
```

### 3.3 JavaScript Changes (`main.js`)

#### Step 1 — Import panzoom
```javascript
import panzoom from 'https://esm.sh/panzoom@9.4.0';
```

#### Step 2 — Size `#visual-board` explicitly in `renderBoard()`
```javascript
function renderBoard() {
    if (!boardVisual) return;
    boardVisual.innerHTML = '';
    boardVisual.style.position  = 'absolute';
    boardVisual.style.width     = `${activeSchema.visuals.svgWidth  || 600}px`;
    boardVisual.style.height    = `${activeSchema.visuals.svgHeight || 400}px`;
    // ... rest of existing render logic
    
    autoFitBoard(); // fit after render
}
```

#### Step 3 — Initialize panzoom once (on first board component creation)
```javascript
let panzoomInstance = null;

function initPanZoom() {
    if (panzoomInstance) panzoomInstance.dispose();

    panzoomInstance = panzoom(boardVisual, {
        maxZoom: 3.0,
        minZoom: 0.1,
        zoomSpeed: 0.065,
        beforeMouseDown(e) {
            const isMoveTool    = activeTool === 'move';
            const isMiddleClick = e.button === 1;
            // Allow panning only for: middle-click (any tool), or left-click + Move tool
            return !(isMiddleClick || (e.button === 0 && isMoveTool));
        }
    });
}
```

Call `initPanZoom()` inside `myLayout.registerComponentFactoryFunction('board', ...)` after the container is appended.

#### Step 4 — Auto-fit helper
```javascript
function autoFitBoard() {
    if (!panzoomInstance || !activeSchema) return;

    const cW = boardContainer.clientWidth;
    const cH = boardContainer.clientHeight;
    const bW = activeSchema.visuals.svgWidth  || 600;
    const bH = activeSchema.visuals.svgHeight || 400;

    const scale = Math.min((cW * 0.9) / bW, (cH * 0.9) / bH, 2.0);
    const x = (cW - bW * scale) / 2;
    const y = (cH - bH * scale) / 2;

    panzoomInstance.moveTo(x, y);
    panzoomInstance.zoomAbs(0, 0, scale);
}
```

#### Step 5 — Floating zoom buttons
```javascript
document.getElementById('zoom-in').onclick  = () => {
    const { width, height } = boardContainer.getBoundingClientRect();
    panzoomInstance.smoothZoom(width / 2, height / 2, 1.25);
};
document.getElementById('zoom-out').onclick = () => {
    const { width, height } = boardContainer.getBoundingClientRect();
    panzoomInstance.smoothZoom(width / 2, height / 2, 0.8);
};
document.getElementById('zoom-fit').onclick  = autoFitBoard;
```

---

## 4. Verification Milestones

| # | Test | Expected |
|---|------|----------|
| 1 | Load page | Board auto-fits and centres in workspace |
| 2 | Mouse wheel on board | Smooth zoom towards cursor position |
| 3 | Left-click with Pointer tool | Pin tooltip appears; canvas does **not** pan |
| 4 | Switch to Move tool, left-click drag | Canvas pans smoothly |
| 5 | Middle-click drag (any tool) | Canvas pans smoothly |
| 6 | Click `+` / `−` buttons | Zoom in/out centred on workspace |
| 7 | Click fit button | Board re-centres and fits to screen |
| 8 | Resize Golden Layout pane | Board re-fits after `autoFitBoard()` on resize |

---

## 5. Design Decisions (Confirmed)

| # | Question | Decision |
|---|----------|----------|
| 1 | Auto-refit on pane resize? | **No.** Only the explicit fit button triggers refit. |
| 2 | Middle-click drag scope? | **Always active** regardless of tool selection. Mousewheel zoom and Mac trackpad pinch-to-zoom should also always work. |
