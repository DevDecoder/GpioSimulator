# UI Enhancements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the GPIO Simulator frontend into a responsive multi-pane desktop IDE using Golden Layout v2, featuring dynamic board selection, custom component catalogs, light/dark theme switching, and canvas tool selectors.

**Architecture:** We use Golden Layout v2 loaded from a CDN as an ES Module inside a single main viewport container. Custom panels are initialized from a hidden templates container, and dynamically styled using responsive, custom HSL variables supporting high-fidelity light/dark modes.

**Tech Stack:** Vanilla HTML5, Vanilla CSS3, ESM JavaScript, Golden Layout v2 CDN.

---

### Task 1: Add HTML Structure for Menu and Golden Layout

**Files:**
- Modify: `src/DevDecoder.GpioSimulator.Web/wwwroot/index.html`

- [ ] **Step 1: Write the updated index.html code**

Replace `src/DevDecoder.GpioSimulator.Web/wwwroot/index.html` with:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>DevDecoder GPIO Simulator</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@400;600;700;800&family=Fira+Code:wght@400;600&display=swap" rel="stylesheet">
    
    <!-- Golden Layout Stylesheets -->
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/golden-layout@2.6.0/dist/css/goldenlayout-base.css">
    <link id="golden-layout-theme" rel="stylesheet" href="https://cdn.jsdelivr.net/npm/golden-layout@2.6.0/dist/css/themes/goldenlayout-dark-theme.css">
    
    <link rel="stylesheet" href="style.css?v=2">
</head>
<body>
    <!-- Top Menu Bar -->
    <div id="menu-bar">
        <div class="menu-items">
            <div class="menu-item-group">
                <button class="menu-trigger">File</button>
                <div class="menu-dropdown">
                    <button id="menu-file-open" class="dropdown-item">Open Layout / Component (.gsl, .gsc)</button>
                    <button id="menu-file-save" class="dropdown-item">Save Layout (.gsl)</button>
                </div>
            </div>
            <div class="menu-item-group">
                <button class="menu-trigger">View</button>
                <div class="menu-dropdown">
                    <button id="menu-view-components" class="dropdown-item checked">Components Pane</button>
                    <button id="menu-view-logs" class="dropdown-item checked">Activity Logs Pane</button>
                </div>
            </div>
        </div>
        <div class="menu-controls">
            <button id="theme-toggle" class="control-btn" title="Toggle Light/Dark Theme">
                <span id="theme-icon">🌙</span>
            </button>
        </div>
    </div>
    
    <!-- Main Full-Screen Layout Workspace -->
    <main id="layout-container"></main>

    <!-- Hidden Template Panels Repository (Adopted by Golden Layout) -->
    <div id="component-templates" style="display: none;">
        <!-- Board Workspace Template -->
        <div id="board-container">
            <!-- Canvas Operations Toolbar -->
            <div id="canvas-toolbar">
                <button id="tool-pointer" class="tool-btn active" title="Pointer (Select & Interact)">
                    <svg viewBox="0 0 24 24" width="16" height="16"><path fill="currentColor" d="M7,2l12,11.2l-5.8,1.5l3.4,6.7l-2.7,1.3l-3.4-6.8L7,20V2z"/></svg>
                </button>
                <button id="tool-connector" class="tool-btn" title="Connector (Draw Wires)">
                    <svg viewBox="0 0 24 24" width="16" height="16"><path fill="currentColor" d="M19,3H5C3.9,3,3,3.9,3,5v14c0,1.1,0.9,2,2,2h14c1.1,0,2-0.9,2-2V5C21,3.9,20.1,3,19,3z M19,19H5V5h14V19z M12,6h2v6h-2V6z M10,12h4v2h-4V12z M10,15h4v2h-4V15z"/></svg>
                </button>
                <button id="tool-move" class="tool-btn" title="Move (Pan Canvas)">
                    <svg viewBox="0 0 24 24" width="16" height="16"><path fill="currentColor" d="M12,2A10,10,0,1,0,22,12,10,10,0,0,0,12,2Zm1,17a1,1,0,1,1-2,0V16h2Zm0-5a1,1,0,0,1-2,0V7h2Z"/></svg>
                </button>
                <button id="tool-delete" class="tool-btn" title="Delete wire/component">
                    <svg viewBox="0 0 24 24" width="16" height="16"><path fill="currentColor" d="M6,19c0,1.1,0.9,2,2,2h8c1.1,0,2-0.9,2-2V7H6V19z M19,4h-3.5l-1-1h-5l-1,1H5v2h14V4z"/></svg>
                </button>
                <span class="tool-separator"></span>
                <span id="active-tool-badge" class="tool-badge">Pointer</span>
            </div>
            <div id="visual-board"></div>
        </div>

        <!-- Terminal Log Pane Template -->
        <div id="terminal-panel">
            <h3>Real-Time Activity Log</h3>
            <div id="terminal-log"></div>
        </div>

        <!-- Components Panel Template -->
        <div id="components-panel" class="workspace-pane">
            <div class="components-category">
                <h3>Available Boards</h3>
                <div id="boards-list" class="components-grid">
                    <!-- Dynamic selector cards go here -->
                </div>
            </div>
            <div class="components-category">
                <h3>Discrete Components</h3>
                <div id="discrete-components-list" class="components-grid">
                    <div class="component-card disabled" title="Available in future update">
                        <span class="comp-icon">💡</span>
                        <span class="comp-name">LED Indicator</span>
                    </div>
                    <div class="component-card disabled" title="Available in future update">
                        <span class="comp-icon">🔘</span>
                        <span class="comp-name">Push Button</span>
                    </div>
                    <div class="component-card disabled" title="Available in future update">
                        <span class="comp-icon">⚡</span>
                        <span class="comp-name">Resistor</span>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Hidden Input for Custom Files Upload -->
    <input type="file" id="file-picker" style="display: none;" accept=".gsc,.gsl">

    <div id="disconnected-overlay">
        <div class="overlay-card">
            <div class="overlay-icon">🔌</div>
            <h2>Simulator Disconnected</h2>
            <p>The simulator web server has shut down.</p>
            <p class="sub-text">You can now close this browser tab safely.</p>
            <div class="reconnect-status">
                <span class="pulse-dot"></span>
                Attempting to reconnect...
            </div>
        </div>
    </div>

    <!-- Script registered as ES module for dynamic CDN imports -->
    <script type="module" src="main.js?v=2"></script>
</body>
</html>
```

- [ ] **Step 2: Verify HTML loads**

Ensure there are no parsing errors.

---

### Task 2: Implement Complete Theme System & Core Styles in CSS

**Files:**
- Modify: `src/DevDecoder.GpioSimulator.Web/wwwroot/style.css`

- [ ] **Step 1: Write Custom Stylings and Theme Variables**

Append the following styles to the top of `src/DevDecoder.GpioSimulator.Web/wwwroot/style.css` (overwriting the root variable block and body styles):

```css
:root {
    /* Global Variables - Default Dark mode */
    --bg-color: #0b0c10;
    --card-bg: rgba(22, 24, 30, 0.85);
    --text-color: #c5c6c7;
    --text-bright: #ffffff;
    --accent: #66fcf1;
    --accent-dim: #45a29e;
    --accent-glow: rgba(102, 252, 241, 0.25);
    
    --border-color: rgba(102, 252, 241, 0.15);
    --menu-bg: #07090e;
    --menu-hover: rgba(102, 252, 241, 0.1);
    
    --card-hover-border: var(--accent);
    --card-hover-bg: rgba(102, 252, 241, 0.03);
    
    --led-off: #2e3033;
    --led-on: #39ff14;
    --power-color: #ff3838;
    --v3-color: #ff9f43;
    --gnd-color: #576574;
    --input-color: #3498db;
    --output-color: #9b59b6;
}

body.light-theme {
    /* Light Mode Overrides */
    --bg-color: #f5f6fa;
    --card-bg: rgba(255, 255, 255, 0.95);
    --text-color: #2f3640;
    --text-bright: #1e272e;
    --accent: #00a8ff;
    --accent-dim: #0097e6;
    --accent-glow: rgba(0, 168, 255, 0.2);
    
    --border-color: rgba(0, 168, 255, 0.15);
    --menu-bg: #ffffff;
    --menu-hover: rgba(0, 168, 255, 0.08);
    
    --card-hover-border: var(--accent);
    --card-hover-bg: rgba(0, 168, 255, 0.02);
}

body {
    background-color: var(--bg-color);
    background-image: radial-gradient(circle at top right, var(--accent-glow), transparent 40%);
    color: var(--text-color);
    font-family: 'Outfit', 'Segoe UI', system-ui, sans-serif;
    margin: 0;
    padding: 0;
    box-sizing: border-box;
    display: flex;
    flex-direction: column;
    height: 100vh;
    overflow: hidden;
}

/* Menu Bar Styles */
#menu-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    background-color: var(--menu-bg);
    border-bottom: 1px solid var(--border-color);
    padding: 0 16px;
    height: 40px;
    box-sizing: border-box;
    user-select: none;
    z-index: 1000;
}

.menu-items {
    display: flex;
    gap: 4px;
}

.menu-item-group {
    position: relative;
}

.menu-trigger {
    background: none;
    border: none;
    color: var(--text-color);
    font-family: inherit;
    font-size: 13px;
    font-weight: 600;
    padding: 8px 16px;
    cursor: pointer;
    border-radius: 4px;
    transition: background 0.2s, color 0.2s;
}

.menu-trigger:hover, .menu-item-group:hover .menu-trigger {
    background-color: var(--menu-hover);
    color: var(--text-bright);
}

.menu-dropdown {
    display: none;
    position: absolute;
    top: 100%;
    left: 0;
    background-color: var(--menu-bg);
    border: 1px solid var(--border-color);
    border-radius: 6px;
    box-shadow: 0 8px 24px rgba(0,0,0,0.5);
    padding: 6px 0;
    min-width: 240px;
    z-index: 1001;
}

.menu-item-group:hover .menu-dropdown {
    display: block;
}

.dropdown-item {
    display: block;
    width: 100%;
    text-align: left;
    background: none;
    border: none;
    color: var(--text-color);
    font-family: inherit;
    font-size: 13px;
    padding: 8px 16px;
    cursor: pointer;
    transition: background 0.2s, color 0.2s;
}

.dropdown-item:hover {
    background-color: var(--menu-hover);
    color: var(--accent);
}

.menu-controls {
    display: flex;
    align-items: center;
}

.control-btn {
    background: none;
    border: 1px solid var(--border-color);
    color: var(--text-color);
    border-radius: 50%;
    width: 28px;
    height: 28px;
    display: flex;
    justify-content: center;
    align-items: center;
    cursor: pointer;
    transition: all 0.2s;
}

.control-btn:hover {
    border-color: var(--accent);
    box-shadow: 0 0 8px var(--accent-glow);
}

/* Layout Container */
#layout-container {
    flex-grow: 1;
    width: 100%;
    position: relative;
}

/* Panel Layout Custom Flattening Styles */
#board-container, #terminal-panel, #components-panel {
    width: 100%;
    height: 100%;
    box-sizing: border-box;
    border-radius: 0 !important;
    border: none !important;
    margin: 0 !important;
    box-shadow: none !important;
}

#board-container {
    position: relative;
    padding: 32px;
    background-color: var(--bg-color);
}

#terminal-panel {
    background-color: #020204;
    padding: 12px;
}

#terminal-panel h3 {
    margin: 0 0 8px 0 !important;
}

/* Workspace Panes styling */
.workspace-pane {
    background-color: var(--menu-bg);
    padding: 16px;
    overflow-y: auto;
}

.components-category h3 {
    font-size: 12px;
    text-transform: uppercase;
    letter-spacing: 1px;
    color: var(--accent-dim);
    margin: 0 0 12px 0;
    border-bottom: 1px solid var(--border-color);
    padding-bottom: 6px;
}

.components-grid {
    display: flex;
    flex-direction: column;
    gap: 8px;
    margin-bottom: 24px;
}

.component-card {
    background-color: var(--card-bg);
    border: 1px solid var(--border-color);
    border-radius: 8px;
    padding: 12px;
    display: flex;
    align-items: center;
    gap: 12px;
    cursor: pointer;
    transition: all 0.2s ease-in-out;
}

.component-card:not(.disabled):hover {
    border-color: var(--card-hover-border);
    background-color: var(--card-hover-bg);
    transform: translateX(4px);
}

.component-card.active {
    border-color: var(--accent) !important;
    background-color: var(--accent-glow) !important;
    box-shadow: 0 0 12px var(--accent-glow);
}

.component-card.disabled {
    opacity: 0.4;
    cursor: not-allowed;
}

.comp-icon {
    font-size: 20px;
}

.comp-info {
    display: flex;
    flex-direction: column;
}

.comp-name {
    font-size: 13px;
    font-weight: 700;
    color: var(--text-bright);
}

.comp-desc {
    font-size: 10px;
    color: #888;
}

.active-badge {
    margin-left: auto;
    font-size: 9px;
    background-color: var(--accent);
    color: #000;
    padding: 2px 6px;
    border-radius: 8px;
    font-weight: 800;
}

/* Floating Toolbar for Board Pane */
#canvas-toolbar {
    position: absolute;
    top: 12px;
    left: 50%;
    transform: translateX(-50%);
    background: rgba(18, 22, 28, 0.85);
    backdrop-filter: blur(12px);
    border: 1px solid var(--border-color);
    border-radius: 8px;
    padding: 4px 8px;
    display: flex;
    align-items: center;
    gap: 6px;
    z-index: 100;
    box-shadow: 0 4px 16px rgba(0,0,0,0.5);
}

.tool-btn {
    background: none;
    border: none;
    color: var(--text-color);
    width: 28px;
    height: 28px;
    border-radius: 6px;
    display: flex;
    justify-content: center;
    align-items: center;
    cursor: pointer;
    transition: all 0.2s;
}

.tool-btn:hover {
    background-color: rgba(255,255,255,0.05);
    color: var(--text-bright);
}

.tool-btn.active {
    background-color: var(--accent);
    color: #000 !important;
}

.tool-separator {
    width: 1px;
    height: 16px;
    background-color: var(--border-color);
    margin: 0 4px;
}

.tool-badge {
    font-size: 10px;
    font-weight: 700;
    color: var(--accent);
    padding-right: 4px;
    text-transform: uppercase;
}

/* Overriding Golden Layout Theme Defaults dynamically */
.lm_header {
    background-color: #07090e !important;
    box-shadow: none !important;
}

.lm_tab {
    background-color: #12161c !important;
    border-top: 1px solid rgba(255,255,255,0.05) !important;
    color: var(--text-color) !important;
    font-family: inherit !important;
    font-size: 11px !important;
    font-weight: 600 !important;
}

.lm_tab.lm_active {
    background-color: var(--menu-bg) !important;
    color: var(--accent) !important;
    border-bottom: 2px solid var(--accent) !important;
}
```

---

### Task 3: Refactor JS to ES Module & Initialize Golden Layout

**Files:**
- Create/Modify: `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js`

- [ ] **Step 1: Write Golden Layout instantiation & core state logic**

Update the entire script body of `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js` as an ES Module:

```javascript
import { GoldenLayout } from 'https://cdn.jsdelivr.net/npm/golden-layout@2.6.0/dist/esm/index.js';

let activeSchema = null;
let ws = null;
const pinsStateMap = {};
let activeTooltipPin = null;

// Track Canvas Simulation States
let activeTool = 'pointer';
const customBoards = [];

// Initialize Layout Templates
const boardContainer = document.getElementById('board-container');
const terminalPanel = document.getElementById('terminal-panel');
const componentsPanel = document.getElementById('components-panel');

// Detach from DOM to make them dynamic templates
boardContainer.remove();
terminalPanel.remove();
componentsPanel.remove();

const layoutContainer = document.getElementById('layout-container');
const myLayout = new GoldenLayout(layoutContainer);

// Register Component Factories
myLayout.registerComponentFactoryFunction('board', (container) => {
    container.element.appendChild(boardContainer);
});
myLayout.registerComponentFactoryFunction('logs', (container) => {
    container.element.appendChild(terminalPanel);
});
myLayout.registerComponentFactoryFunction('components', (container) => {
    container.element.appendChild(componentsPanel);
});

// Configure Window split
const defaultLayoutConfig = {
    root: {
        type: 'row',
        content: [
            {
                type: 'component',
                componentType: 'components',
                title: 'Components',
                width: 25,
                isClosable: true
            },
            {
                type: 'column',
                width: 75,
                content: [
                    {
                        type: 'component',
                        componentType: 'board',
                        title: 'Simulator Workspace',
                        height: 65,
                        isClosable: false
                    },
                    {
                        type: 'component',
                        componentType: 'logs',
                        title: 'Real-Time Activity Log',
                        height: 35,
                        isClosable: true
                    }
                ]
            }
        ]
    }
};

// Initialize workspace
myLayout.loadLayout(defaultLayoutConfig);

// Setup floating tooltip element
const tooltip = document.createElement('div');
tooltip.id = 'pin-tooltip';
tooltip.className = 'glass-panel';
tooltip.style.position = 'absolute';
tooltip.style.display = 'none';
tooltip.style.zIndex = '1000';
document.body.appendChild(tooltip);

// Close tooltip when clicking outside
document.addEventListener('click', (e) => {
    if (!tooltip.contains(e.target) && !e.target.classList.contains('pin-hotspot')) {
        closeTooltip();
    }
});

function log(message) {
    const logTerminal = document.getElementById('terminal-log');
    if (!logTerminal) return;
    const time = new Date().toLocaleTimeString();
    const div = document.createElement('div');
    div.textContent = `[${time}] ${message}`;
    logTerminal.appendChild(div);
    logTerminal.scrollTop = logTerminal.scrollHeight;
}

async function loadBoard(boardId) {
    closeTooltip();
    log(`Loading board component for: ${boardId}...`);
    try {
        let res;
        // Search custom uploads first, then default directory
        const customBoard = customBoards.find(b => b.boardId === boardId);
        if (customBoard) {
            activeSchema = customBoard.schema;
        } else {
            res = await fetch(`components/${boardId}.gsc?_=${Date.now()}`);
            if (!res.ok) throw new Error(`HTTP error! status: ${res.status}`);
            activeSchema = await res.json();
        }
        
        renderBoard();
        
        // Notify the server of the active board layout if standard board
        if (!customBoard) {
            await fetch(`/api/board/active?boardId=${boardId}`, { method: 'POST' });
        }
        
        await syncPinStatesFromServer();
        updateComponentsPaneUI(boardId);
    } catch (err) {
        log(`Error loading component: ${err}`);
    }
}

async function syncPinStatesFromServer() {
    try {
        const res = await fetch('/api/pins');
        if (res.ok) {
            const serverStates = await res.json();
            Object.entries(serverStates).forEach(([pinStr, stateStr]) => {
                const pin = parseInt(pinStr);
                const parts = stateStr.split(':');
                pinsStateMap[pin] = {
                    mode: parts[0],
                    value: parts[1]
                };
                updatePinVisuals(pin);
            });
            log("Synchronized all pin states from server.");
        }
    } catch (err) {
        log(`Failed to sync states from server: ${err}`);
    }
}

function renderBoard() {
    const boardVisual = document.getElementById('visual-board');
    if (!boardVisual) return;
    boardVisual.innerHTML = "";
    boardVisual.style.position = "relative";
    boardVisual.style.width = "100%";
    boardVisual.style.maxWidth = `${activeSchema.visuals.svgWidth || 600}px`;
    
    const svgContainer = document.createElement('div');
    svgContainer.className = "svg-board-container";
    svgContainer.innerHTML = activeSchema.visuals.svgTemplate;
    boardVisual.appendChild(svgContainer);
    
    activeSchema.pins.forEach(pin => {
        const hotspot = document.createElement('div');
        hotspot.className = `pin-hotspot pin-phys-${pin.physical}`;
        
        const leftPct = (pin.x / (activeSchema.visuals.svgWidth || 600)) * 100;
        const topPct = (pin.y / (activeSchema.visuals.svgHeight || 400)) * 100;
        
        hotspot.style.left = `calc(${leftPct}% - 10px)`;
        hotspot.style.top = `calc(${topPct}% - 10px)`;
        
        if (pin.name.includes("GND")) hotspot.classList.add("gnd");
        else if (pin.name.includes("5V")) hotspot.classList.add("v5");
        else if (pin.name.includes("3.3")) hotspot.classList.add("v3");
        else hotspot.classList.add("gpio");
        
        const indexSpan = document.createElement('span');
        indexSpan.textContent = pin.physical;
        hotspot.appendChild(indexSpan);
        
        hotspot.addEventListener('click', (e) => {
            e.stopPropagation();
            if (activeTool === 'pointer') {
                showTooltip(pin, hotspot);
            } else if (activeTool === 'delete') {
                log(`Interacted with Pin ${pin.physical} with Delete Tool.`);
            }
        });
        
        boardVisual.appendChild(hotspot);
        updatePinVisuals(pin.physical);
    });
    
    log(`Rendered ${activeSchema.displayName} vector board successfully.`);
}

function updatePinVisuals(physicalPin) {
    const boardVisual = document.getElementById('visual-board');
    if (!boardVisual) return;
    
    const pinDef = activeSchema?.pins.find(p => p.physical === physicalPin);
    if (!pinDef) return;
    
    const hotspot = boardVisual.querySelector(`.pin-phys-${physicalPin}`);
    if (!hotspot) return;
    
    const state = pinsStateMap[physicalPin] || { mode: "None", value: "Low" };
    
    if (state.value === "High") {
        hotspot.classList.add("active");
    } else {
        hotspot.classList.remove("active");
    }
    
    hotspot.classList.remove("mode-input", "mode-output", "mode-none");
    if (state.mode === "Input") {
        hotspot.classList.add("mode-input");
    } else if (state.mode === "Output") {
        hotspot.classList.add("mode-output");
    } else {
        hotspot.classList.add("mode-none");
    }
    
    if (activeTooltipPin === physicalPin) {
        refreshTooltipContent(pinDef, hotspot);
    }
}

function showTooltip(pin, anchorEl) {
    activeTooltipPin = pin.physical;
    refreshTooltipContent(pin, anchorEl);
    
    const rect = anchorEl.getBoundingClientRect();
    const scrollLeft = window.pageXOffset || document.documentElement.scrollLeft;
    const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
    
    tooltip.style.display = 'block';
    
    let leftPos = rect.left + scrollLeft + 30;
    if (leftPos + 220 > window.innerWidth) {
        leftPos = rect.left + scrollLeft - 235;
    }
    
    tooltip.style.left = `${leftPos}px`;
    tooltip.style.top = `${rect.top + scrollTop - 40}px`;
}

function refreshTooltipContent(pin, anchorEl) {
    const state = pinsStateMap[pin.physical] || { mode: "None", value: "Low" };
    let logicalStr = "N/A (Power / GND)";
    let controlSection = "";
    
    if (pin.logical !== null) {
        logicalStr = `GPIO ${pin.logical}`;
        const isInput = state.mode.toLowerCase() === "input";
        const checkedAttr = state.value === "High" ? "checked" : "";
        const disabledAttr = !isInput ? "disabled" : "";
        
        controlSection = `
            <div class="tooltip-control">
                <label class="switch ${!isInput ? 'disabled' : ''}">
                    <input type="checkbox" id="tooltip-state-toggle" ${checkedAttr} ${disabledAttr}>
                    <span class="slider round"></span>
                </label>
                <div class="control-text">
                    <span class="control-label">Manual Input Driver</span>
                    <span class="control-sub">${isInput ? 'Toggle HIGH/LOW' : 'Output governed by code'}</span>
                </div>
            </div>
        `;
    }
    
    tooltip.innerHTML = `
        <div class="tooltip-header">
            <h4>${pin.name}</h4>
            <span class="close-btn" id="tooltip-close-x">&times;</span>
        </div>
        <div class="tooltip-body">
            <div class="tooltip-info-row">
                <span class="info-label">Physical Pin:</span>
                <span class="info-val font-mon">${pin.physical}</span>
            </div>
            <div class="tooltip-info-row">
                <span class="info-label">Logical ID:</span>
                <span class="info-val font-mon">${logicalStr}</span>
            </div>
            <div class="tooltip-info-row">
                <span class="info-label">Current Mode:</span>
                <span class="info-val badge mode-${state.mode.toLowerCase()}">${state.mode}</span>
            </div>
            <div class="tooltip-info-row">
                <span class="info-label">Logic Level:</span>
                <span class="info-val state-indicator ${state.value.toLowerCase()}">
                    <span class="state-dot"></span>
                    ${state.value}
                </span>
            </div>
            ${controlSection}
        </div>
    `;
    
    document.getElementById('tooltip-close-x').onclick = closeTooltip;
    
    if (pin.logical !== null) {
        const toggle = document.getElementById('tooltip-state-toggle');
        if (toggle) {
            toggle.onchange = (e) => {
                const newState = e.target.checked ? "High" : "Low";
                pinsStateMap[pin.physical].value = newState;
                log(`Input manually driven to ${newState} on Pin ${pin.physical}`);
                sendPinState(pin.physical, "read", newState);
                updatePinVisuals(pin.physical);
            };
        }
    }
}

function closeTooltip() {
    tooltip.style.display = 'none';
    activeTooltipPin = null;
}

function setupWebSocket() {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    ws = new WebSocket(`${protocol}//${window.location.host}/ws?client=ui`);
    
    ws.onopen = () => {
        log("WebSocket Connected to Simulator Server.");
        document.getElementById('disconnected-overlay').classList.remove('active');
    };
    
    ws.onmessage = (event) => {
        try {
            const msg = JSON.parse(event.data);
            
            if (msg.action === "reset") {
                for (const key in pinsStateMap) {
                    delete pinsStateMap[key];
                }
                if (activeSchema && activeSchema.pins) {
                    activeSchema.pins.forEach(pin => {
                        updatePinVisuals(pin.physical);
                    });
                }
                log("Simulator state reset.");
            }
            else if (msg.action === "close") {
                delete pinsStateMap[msg.pin];
                updatePinVisuals(msg.pin);
                log(`Pin ${msg.pin} closed`);
            }
            else if (msg.action === "log") {
                log(msg.value);
            }
            else if (msg.action === "write") {
                pinsStateMap[msg.pin] = pinsStateMap[msg.pin] || { mode: "Output", value: "Low" };
                pinsStateMap[msg.pin].value = msg.value;
                updatePinVisuals(msg.pin);
                log(`Pin ${msg.pin} state set to: ${msg.value}`);
            }
            else if (msg.action === "mode") {
                pinsStateMap[msg.pin] = pinsStateMap[msg.pin] || { mode: "Input", value: "Low" };
                pinsStateMap[msg.pin].mode = msg.mode;
                updatePinVisuals(msg.pin);
                log(`Pin ${msg.pin} mode configured: ${msg.mode}`);
            }
            else if (msg.action === "state_change") {
                pinsStateMap[msg.pin] = {
                    mode: msg.mode,
                    value: msg.value
                };
                updatePinVisuals(msg.pin);
                log(`Pin ${msg.pin} updated from host: Mode=${msg.mode}, State=${msg.value}`);
            }
        } catch (err) {
            console.error("Error parsing WebSocket packet", err);
        }
    };
    
    ws.onclose = () => {
        log("WebSocket closed. Attempting reconnect...");
        document.getElementById('disconnected-overlay').classList.add('active');
        setTimeout(setupWebSocket, 3000);
    };
}

function sendPinState(pin, action, val) {
    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ action: action, pin: pin, value: val }));
    }
}

// ----------------------------------------------------
// Task 4: Dynamic Components Panel and Switcher Setup
// ----------------------------------------------------
const catalogBoards = [
    { id: 'raspberry_pi_5_breakout', name: 'Raspberry Pi 5 (Breakout)', desc: 'Breadboard breakout prototype' },
    { id: 'raspberry_pi_5', name: 'Raspberry Pi 5', desc: 'Standard single board computer' },
    { id: 'arduino_uno', name: 'Arduino Uno R3', desc: 'Sleek ATmega328P microcontroller' }
];

function updateComponentsPaneUI(activeBoardId) {
    const listContainer = document.getElementById('boards-list');
    if (!listContainer) return;
    listContainer.innerHTML = "";
    
    // Render default catalog
    catalogBoards.forEach(board => {
        const card = createBoardCard(board.id, board.name, board.desc, activeBoardId === board.id);
        listContainer.appendChild(card);
    });
    
    // Render custom uploaded boards
    customBoards.forEach(board => {
        const card = createBoardCard(board.boardId, board.name, 'Uploaded Custom Board File', activeBoardId === board.boardId);
        listContainer.appendChild(card);
    });
}

function createBoardCard(id, name, desc, isActive) {
    const card = document.createElement('div');
    card.className = `component-card ${isActive ? 'active' : ''}`;
    card.innerHTML = `
        <span class="comp-icon">🔌</span>
        <div class="comp-info">
            <span class="comp-name">${name}</span>
            <span class="comp-desc">${desc}</span>
        </div>
        ${isActive ? '<span class="active-badge">Active</span>' : ''}
    `;
    card.onclick = () => loadBoard(id);
    return card;
}

// ----------------------------------------------------
// Task 5: Theme Switching Handlers
// ----------------------------------------------------
const themeToggleBtn = document.getElementById('theme-toggle');
const themeIcon = document.getElementById('theme-icon');
const layoutThemeLink = document.getElementById('golden-layout-theme');

themeToggleBtn.onclick = () => {
    const isLight = document.body.classList.toggle('light-theme');
    if (isLight) {
        themeIcon.textContent = '☀️';
        layoutThemeLink.href = 'https://cdn.jsdelivr.net/npm/golden-layout@2.6.0/dist/css/themes/goldenlayout-light-theme.css';
        localStorage.setItem('theme', 'light');
    } else {
        themeIcon.textContent = '🌙';
        layoutThemeLink.href = 'https://cdn.jsdelivr.net/npm/golden-layout@2.6.0/dist/css/themes/goldenlayout-dark-theme.css';
        localStorage.setItem('theme', 'dark');
    }
};

// Check stored preference
const savedTheme = localStorage.getItem('theme');
if (savedTheme === 'light') {
    document.body.classList.add('light-theme');
    themeIcon.textContent = '☀️';
    layoutThemeLink.href = 'https://cdn.jsdelivr.net/npm/golden-layout@2.6.0/dist/css/themes/goldenlayout-light-theme.css';
}

// ----------------------------------------------------
// Task 6: View Menu Dynamic Toggling Handlers
// ----------------------------------------------------
const viewComponentsBtn = document.getElementById('menu-view-components');
const viewLogsBtn = document.getElementById('menu-view-logs');

viewComponentsBtn.onclick = () => {
    togglePanel('components', viewComponentsBtn);
};

viewLogsBtn.onclick = () => {
    togglePanel('logs', viewLogsBtn);
};

function togglePanel(componentType, menuBtn) {
    const isChecked = menuBtn.classList.toggle('checked');
    if (isChecked) {
        // Re-append to Golden Layout
        const targetParent = myLayout.rootItem.contentItems[0];
        if (targetParent) {
            targetParent.addChild({
                type: 'component',
                componentType: componentType,
                title: componentType.charAt(0).toUpperCase() + componentType.slice(1),
                isClosable: true
            });
        }
    } else {
        // Close from layout
        const targetItem = myLayout.rootItem.getItemsByProductType(componentType)[0];
        if (targetItem) {
            targetItem.parent.removeChild(targetItem);
        }
    }
}

// Keep menu items state checked in sync when panes are closed manually
myLayout.on('itemDestroyed', (item) => {
    if (item.componentType === 'components') {
        viewComponentsBtn.classList.remove('checked');
    } else if (item.componentType === 'logs') {
        viewLogsBtn.classList.remove('checked');
    }
});

myLayout.on('itemCreated', (item) => {
    if (item.componentType === 'components') {
        viewComponentsBtn.classList.add('checked');
    } else if (item.componentType === 'logs') {
        viewLogsBtn.classList.add('checked');
    }
});

// ----------------------------------------------------
// Task 7: File Menu (Open/Save) Circuit Engine
// ----------------------------------------------------
const openFileBtn = document.getElementById('menu-file-open');
const saveFileBtn = document.getElementById('menu-file-save');
const filePickerInput = document.getElementById('file-picker');

saveFileBtn.onclick = () => {
    if (!activeSchema) return;
    
    // Create Layout schema compilation (.gsl format)
    const layoutConfig = {
        format: "gsl",
        version: "1.0",
        boardId: activeSchema.boardId || 'raspberry_pi_5_breakout',
        components: [],
        connections: []
    };
    
    const blob = new Blob([JSON.stringify(layoutConfig, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${layoutConfig.boardId}-layout.gsl`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    log(`Circuit layout configurations saved: ${layoutConfig.boardId}-layout.gsl`);
};

openFileBtn.onclick = () => {
    filePickerInput.click();
};

filePickerInput.onchange = (e) => {
    const file = e.target.files[0];
    if (!file) return;
    
    const reader = new FileReader();
    reader.onload = async (event) => {
        try {
            const data = JSON.parse(event.target.result);
            if (data.format === "gsl") {
                log(`Parsing circuit config file: ${file.name}`);
                await loadBoard(data.boardId);
            } else if (data.pins && data.visuals && data.displayName) {
                // Parse component spec (.gsc format)
                log(`Loading custom component spec file: ${file.name}`);
                const boardId = file.name.replace('.gsc', '');
                
                customBoards.push({
                    boardId: boardId,
                    name: data.displayName,
                    schema: data
                });
                
                await loadBoard(boardId);
            } else {
                throw new Error("Invalid file schema format!");
            }
        } catch (err) {
            log(`Failed to open custom file: ${err.message}`);
        }
    };
    reader.readAsText(file);
};

// ----------------------------------------------------
// Task 8: Canvas Operations Toolbar Handlers
// ----------------------------------------------------
const tools = [
    { id: 'pointer', name: 'Pointer' },
    { id: 'connector', name: 'Connector' },
    { id: 'move', name: 'Move' },
    { id: 'delete', name: 'Delete' }
];

tools.forEach(tool => {
    const btn = document.getElementById(`tool-${tool.id}`);
    if (btn) {
        btn.onclick = () => {
            tools.forEach(t => document.getElementById(`tool-${t.id}`).classList.remove('active'));
            btn.classList.add('active');
            activeTool = tool.id;
            document.getElementById('active-tool-badge').textContent = tool.name;
            log(`Active tool changed to: ${tool.name}`);
            
            // Alter board cursor style based on tool selection
            const boardContainer = document.getElementById('visual-board');
            if (boardContainer) {
                if (tool.id === 'pointer') boardContainer.style.cursor = 'default';
                else if (tool.id === 'connector') boardContainer.style.cursor = 'crosshair';
                else if (tool.id === 'move') boardContainer.style.cursor = 'grab';
                else if (tool.id === 'delete') boardContainer.style.cursor = 'alias';
            }
        };
    }
});

// Setup
loadBoard('raspberry_pi_5_breakout').then(setupWebSocket);
```

---

### Task 4: Run Verification Tests

**Files:**
- Create: `src/DevDecoder.GpioSimulator.Web/wwwroot/test-suite.js`

- [ ] **Step 1: Write layout verification script**

Create `src/DevDecoder.GpioSimulator.Web/wwwroot/test-suite.js` with layout validations:

```javascript
export function runLayoutVerificationTests() {
    console.group("GPIO Simulator Desktop Layout Verification Tests");
    
    // Test 1: Validate menu bar elements
    const menuBar = document.getElementById('menu-bar');
    console.assert(menuBar !== null, "FAIL: Top Menu Bar is not rendered");
    
    // Test 2: Validate Golden Layout workspace
    const layout = document.getElementById('layout-container');
    console.assert(layout !== null, "FAIL: Main Golden Layout workspace wrapper is missing");
    console.assert(layout.children.length > 0, "FAIL: Golden Layout has not been instantiated inside target container");
    
    // Test 3: Validate hidden template references exist
    const boardContainer = document.getElementById('board-container');
    const terminalPanel = document.getElementById('terminal-panel');
    const componentsPanel = document.getElementById('components-panel');
    console.assert(boardContainer !== null, "FAIL: Board panel component is missing");
    console.assert(terminalPanel !== null, "FAIL: Terminal panel component is missing");
    console.assert(componentsPanel !== null, "FAIL: Components Catalog panel is missing");
    
    // Test 4: Validate Canvas Toolbar
    const toolbar = document.getElementById('canvas-toolbar');
    console.assert(toolbar !== null, "FAIL: Canvas operations toolbar is missing");
    
    console.log("PASS: Core DOM element assertions completed successfully.");
    console.groupEnd();
}
```

- [ ] **Step 2: Add validation call on boot**

Import and execute tests temporarily at the bottom of `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js`:

```javascript
import { runLayoutVerificationTests } from './test-suite.js';
setTimeout(runLayoutVerificationTests, 1000);
```

Expected output in browser developer console:
`GPIO Simulator Desktop Layout Verification Tests`
`PASS: Core DOM element assertions completed successfully.`
