import { GoldenLayout } from 'https://esm.sh/golden-layout@2.6.0';
// panzoom loaded as UMD global via <script defer> in index.html

let activeSchema = null;
let ws = null;
const pinsStateMap = {};
let activeTooltipPin = null;
let myClientId = null;

// Track Canvas Simulation States
let activeTool = 'move';
let panzoomInstance = null;
const customBoards = [];

// Initialize Layout Templates
const boardContainer = document.getElementById('board-container');
const terminalPanel = document.getElementById('terminal-panel');
const componentsPanel = document.getElementById('components-panel');
const disconnectedOverlay = document.getElementById('disconnected-overlay');

// Pre-cache core elements relative to the templates or document
const boardVisual = boardContainer.querySelector('#visual-board');
const canvasToolbar = document.getElementById('canvas-toolbar');
const logTerminal = terminalPanel.querySelector('#terminal-log');
const listContainer = componentsPanel.querySelector('#boards-list');
const activeToolBadge = document.getElementById('active-tool-badge');

// Detach from DOM to make them dynamic templates
boardContainer.remove();
terminalPanel.remove();
componentsPanel.remove();

const layoutContainer = document.getElementById('layout-container');
const myLayout = new GoldenLayout(layoutContainer);

// Dynamic resize observer to prevent cut-off panes on window resize
const resizeObserver = new ResizeObserver(() => {
    const rect = layoutContainer.getBoundingClientRect();
    myLayout.setSize(rect.width, rect.height);
});
resizeObserver.observe(layoutContainer);

// Register Component Factories
myLayout.registerComponentFactoryFunction('board', (container) => {
    container.element.appendChild(boardContainer);
    
    // Add custom class to parent stack container to hide header / title
    setTimeout(() => {
        const stackElement = container.element.closest('.lm_stack');
        if (stackElement) {
            stackElement.classList.add('no-header');
        }
    }, 0);
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
        type: 'component',
        componentType: 'board',
        title: 'Simulator Workspace',
        isClosable: false
    }
};

// Initialize workspace
myLayout.loadLayout(defaultLayoutConfig);
setTimeout(syncViewMenuChecks, 50);

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
                    value: parts[1],
                    ownerId: parts[2] || null,
                    ownerType: parts[3] || ""
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
    if (!boardVisual) return;
    boardVisual.innerHTML = '';

    // Set explicit canvas dimensions so panzoom has a concrete bounding box
    const bW = activeSchema.visuals.svgWidth  || 600;
    const bH = activeSchema.visuals.svgHeight || 400;
    boardVisual.style.width  = `${bW}px`;
    boardVisual.style.height = `${bH}px`;

    const svgContainer = document.createElement('div');
    svgContainer.className = 'svg-board-container';
    svgContainer.innerHTML = activeSchema.visuals.svgTemplate;
    boardVisual.appendChild(svgContainer);

    activeSchema.pins.forEach(pin => {
        const hotspot = document.createElement('div');
        hotspot.className = `pin-hotspot pin-phys-${pin.physical}`;

        const leftPct = (pin.x / bW) * 100;
        const topPct  = (pin.y / bH) * 100;

        hotspot.style.left = `calc(${leftPct}% - 10px)`;
        hotspot.style.top  = `calc(${topPct}%  - 10px)`;

        if (pin.name.includes('GND'))  hotspot.classList.add('gnd');
        else if (pin.name.includes('5V'))  hotspot.classList.add('v5');
        else if (pin.name.includes('3.3')) hotspot.classList.add('v3');
        else hotspot.classList.add('gpio');

        const indexSpan = document.createElement('span');
        indexSpan.textContent = pin.physical;
        hotspot.appendChild(indexSpan);

        hotspot.addEventListener('click', (e) => {
            e.stopPropagation();
            if (activeTool === 'pointer' || activeTool === 'move') {
                showTooltip(pin, hotspot);
            } else if (activeTool === 'delete') {
                log(`Interacted with Pin ${pin.physical} with Delete Tool.`);
            }
        });

        boardVisual.appendChild(hotspot);
        updatePinVisuals(pin.physical);
    });

    log(`Rendered ${activeSchema.displayName} vector board successfully.`);

    // Initialise or re-initialise panzoom then fit the board to screen
    initPanZoom();
    // rAF ensures the GL container is fully sized before we calculate fit
    requestAnimationFrame(fitBoard);
}

// Initialise anvaka/panzoom on the canvas element
function initPanZoom() {
    if (panzoomInstance) {
        panzoomInstance.dispose();
        panzoomInstance = null;
    }
    panzoomInstance = panzoom(boardVisual, {
        maxZoom: 4.0,
        minZoom: 0.05,
        zoomSpeed: 0.065,
        // Allow panning only when: middle-click (any tool) OR left-click + Move tool
        beforeMouseDown(e) {
            if (e.button === 1) return false;            // middle-click always pans
            if (e.button === 0 && activeTool === 'move') return false; // left + move tool
            return true;                                  // block panning otherwise
        }
    });
}

// Fit the board to the available viewport with 5% padding on each side
function fitBoard() {
    if (!panzoomInstance || !activeSchema) return;
    const cW = boardContainer.clientWidth;
    const cH = boardContainer.clientHeight;
    if (!cW || !cH) return;

    const bW = activeSchema.visuals.svgWidth  || 600;
    const bH = activeSchema.visuals.svgHeight || 400;

    const scale = Math.min((cW * 0.9) / bW, (cH * 0.9) / bH, 4.0);
    const tx = (cW - bW * scale) / 2;
    const ty = (cH - bH * scale) / 2;

    // zoomAbs FIRST (anchors scale at origin, leaving tx=0,ty=0)
    // then moveTo translates to the centred position
    panzoomInstance.zoomAbs(0, 0, scale);
    panzoomInstance.moveTo(tx, ty);
}


function updatePinVisuals(physicalPin) {
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
    
    hotspot.classList.remove("mode-input", "mode-output", "mode-none", "mode-inputpullup", "mode-inputpulldown");
    const modeLower = state.mode ? state.mode.toLowerCase() : "none";
    if (modeLower.startsWith("input")) {
        hotspot.classList.add("mode-input");
        if (modeLower === "inputpullup") {
            hotspot.classList.add("mode-inputpullup");
        } else if (modeLower === "inputpulldown") {
            hotspot.classList.add("mode-inputpulldown");
        }
    } else if (modeLower === "output") {
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
    
    let ownershipStr = "Unopened";
    let ownershipClass = "badge-unopened";
    let canEdit = false;
    
    const isOpened = state.mode && state.mode !== "None" && state.ownerId;
    if (!isOpened) {
        ownershipStr = "Unopened (Available)";
        ownershipClass = "badge-available";
        canEdit = true;
    } else if (state.ownerId === myClientId) {
        ownershipStr = "Opened by You";
        ownershipClass = "badge-owned-by-me";
        canEdit = true;
    } else {
        ownershipStr = `${state.ownerType || "Controller"} (${state.ownerId ? state.ownerId.substring(0, 8) : "unknown"})`;
        ownershipClass = "badge-owned-by-other";
        canEdit = false;
    }
    
    let modeSelector = "";
    
    if (pin.logical !== null) {
        logicalStr = `GPIO ${pin.logical}`;
        const modeLower = state.mode ? state.mode.toLowerCase() : "";
        
        const modes = ["None", "Input", "Output", "InputPullUp", "InputPullDown"];
        const modeLabels = {
            "None": "None (Closed)",
            "Input": "Input",
            "Output": "Output",
            "InputPullUp": "Input Pull-Up",
            "InputPullDown": "Input Pull-Down"
        };
        const options = modes.map(m => {
            const selected = (state.mode || "None") === m ? "selected" : "";
            return `<option value="${m}" ${selected}>${modeLabels[m]}</option>`;
        }).join("");
        
        const disabledAttr = canEdit ? "" : "disabled";
        modeSelector = `
            <select id="tooltip-mode-selector" class="mode-select" ${disabledAttr}>
                ${options}
            </select>
        `;
        
        if (modeLower === "input") {
            const checkedAttr = state.value === "High" ? "checked" : "";
            const toggleDisabled = canEdit ? "" : "disabled";
            controlSection = `
                <div class="tooltip-control ${canEdit ? '' : 'disabled'}">
                    <label class="switch">
                        <input type="checkbox" id="tooltip-state-toggle" ${checkedAttr} ${toggleDisabled}>
                        <span class="slider round"></span>
                    </label>
                    <div class="control-text">
                        <span class="control-label">Manual Input Driver</span>
                        <span class="control-sub">Toggle HIGH/LOW</span>
                    </div>
                </div>
            `;
        } else if (modeLower === "inputpulldown") {
            const btnDisabled = canEdit ? "" : "disabled";
            controlSection = `
                <div class="tooltip-control ${canEdit ? '' : 'disabled'}">
                    <button class="push-btn" id="tooltip-state-push" ${btnDisabled}>Drive HIGH</button>
                    <div class="control-text">
                        <span class="control-label">Pull-Down Push Button</span>
                        <span class="control-sub">Hold to drive HIGH</span>
                    </div>
                </div>
            `;
        } else if (modeLower === "inputpullup") {
            const btnDisabled = canEdit ? "" : "disabled";
            controlSection = `
                <div class="tooltip-control ${canEdit ? '' : 'disabled'}">
                    <button class="push-btn" id="tooltip-state-push" ${btnDisabled}>Drive LOW</button>
                    <div class="control-text">
                        <span class="control-label">Pull-Up Push Button</span>
                        <span class="control-sub">Hold to drive LOW</span>
                    </div>
                </div>
            `;
        } else {
            controlSection = `
                <div class="tooltip-control disabled">
                    <div class="control-text">
                        <span class="control-label">Governed by Controller</span>
                        <span class="control-sub">${modeLower === 'output' ? 'Output governed by code' : 'Pin mode not configured'}</span>
                    </div>
                </div>
            `;
        }
    } else {
        modeSelector = `<span class="info-val badge mode-none">Power / GND</span>`;
        controlSection = `
            <div class="tooltip-control disabled">
                <div class="control-text">
                    <span class="control-label">Non-configurable Pin</span>
                    <span class="control-sub">This is a hardwired physical connection.</span>
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
                <span class="info-label">Ownership:</span>
                <span class="info-val badge ${ownershipClass}" title="${state.ownerId || ''}">${ownershipStr}</span>
            </div>
            <div class="tooltip-info-row">
                <span class="info-label">Current Mode:</span>
                ${modeSelector}
            </div>
            <div class="tooltip-info-row">
                <span class="info-label">Logic Level:</span>
                <span class="info-val state-indicator ${state.value ? state.value.toLowerCase() : 'low'}">
                    <span class="state-dot"></span>
                    ${state.value || 'Low'}
                </span>
            </div>
            ${controlSection}
        </div>
    `;
    
    const closeX = tooltip.querySelector('#tooltip-close-x');
    if (closeX) closeX.onclick = closeTooltip;
    
    if (pin.logical !== null) {
        const modeSelect = tooltip.querySelector('#tooltip-mode-selector');
        if (modeSelect) {
            modeSelect.onchange = (e) => {
                const newMode = e.target.value;
                log(`UI requested mode change for Pin ${pin.physical} to: ${newMode}`);
                
                if (newMode === "None") {
                    if (ws && ws.readyState === WebSocket.OPEN) {
                        ws.send(JSON.stringify({ action: "close", pin: pin.physical }));
                    }
                } else {
                    if (state.ownerId === myClientId) {
                        if (ws && ws.readyState === WebSocket.OPEN) {
                            ws.send(JSON.stringify({ action: "mode", pin: pin.physical, mode: newMode }));
                        }
                    } else {
                        if (ws && ws.readyState === WebSocket.OPEN) {
                            ws.send(JSON.stringify({ action: "open", pin: pin.physical, mode: newMode }));
                        }
                    }
                }
            };
        }
        
        const toggle = tooltip.querySelector('#tooltip-state-toggle');
        if (toggle && canEdit) {
            toggle.onchange = (e) => {
                const newState = e.target.checked ? "High" : "Low";
                pinsStateMap[pin.physical].value = newState;
                log(`Input manually driven to ${newState} on Pin ${pin.physical}`);
                sendPinState(pin.physical, "read", newState);
                updatePinVisuals(pin.physical);
            };
        }
        
        const pushBtn = tooltip.querySelector('#tooltip-state-push');
        if (pushBtn && canEdit) {
            const modeLower = state.mode ? state.mode.toLowerCase() : "";
            const defaultState = modeLower === "inputpullup" ? "High" : "Low";
            const pressedState = modeLower === "inputpullup" ? "Low" : "High";
            
            const handlePress = (e) => {
                e.preventDefault();
                pinsStateMap[pin.physical].value = pressedState;
                log(`Pull-button pressed: Pin ${pin.physical} driven to ${pressedState}`);
                sendPinState(pin.physical, "read", pressedState);
                updatePinVisuals(pin.physical);
            };
            
            const handleRelease = (e) => {
                e.preventDefault();
                pinsStateMap[pin.physical].value = defaultState;
                log(`Pull-button released: Pin ${pin.physical} reverted to default ${defaultState}`);
                sendPinState(pin.physical, "read", defaultState);
                updatePinVisuals(pin.physical);
            };
            
            pushBtn.addEventListener('mousedown', handlePress);
            pushBtn.addEventListener('touchstart', handlePress);
            
            pushBtn.addEventListener('mouseup', handleRelease);
            pushBtn.addEventListener('mouseleave', handleRelease);
            pushBtn.addEventListener('touchend', handleRelease);
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
        if (disconnectedOverlay) disconnectedOverlay.classList.remove('active');
    };
    
    ws.onmessage = (event) => {
        try {
            const msg = JSON.parse(event.data);
            
            if (msg.action === "connected") {
                myClientId = msg.clientId;
                log(`My simulator UI client ID registered: ${myClientId}`);
            }
            else if (msg.action === "reset") {
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
                    value: msg.value,
                    ownerId: msg.ownerId || null,
                    ownerType: msg.ownerType || ""
                };
                updatePinVisuals(msg.pin);
                log(`Pin ${msg.pin} updated: Mode=${msg.mode}, State=${msg.value}, OwnerType=${msg.ownerType}`);
            }
        } catch (err) {
            console.error("Error parsing WebSocket packet", err);
        }
    };
    
    ws.onclose = () => {
        log("WebSocket closed. Attempting reconnect...");
        if (disconnectedOverlay) disconnectedOverlay.classList.add('active');
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
        <span class="comp-icon">
            <svg viewBox="0 0 24 24" width="20" height="20" class="comp-icon-svg">
                <rect x="5" y="5" width="14" height="14" rx="2" fill="none" stroke="currentColor" stroke-width="2"/>
                <rect x="9" y="9" width="6" height="6" rx="1" fill="currentColor"/>
                <path d="M9 1v4M15 1v4M9 19v4M15 19v4M1 9h4M1 15h4M19 9h4M19 15h4" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
            </svg>
        </span>
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

// Helper to recursively find component items in Golden Layout v2
function findComponentItems(node, componentType) {
    if (!node) return [];
    const results = [];
    if (node.type === 'component' && node.componentType === componentType) {
        results.push(node);
    }
    if (node.contentItems && Array.isArray(node.contentItems)) {
        for (const child of node.contentItems) {
            results.push(...findComponentItems(child, componentType));
        }
    }
    return results;
}

viewComponentsBtn.onclick = () => {
    togglePanel('components');
};

viewLogsBtn.onclick = () => {
    togglePanel('logs');
};

// Panel visibility state — source of truth for layout reconstruction
const panelVisible = { components: false, logs: false };

// Build a clean layout config from current panel visibility state
function buildLayoutConfig() {
    const boardConfig = {
        type: 'component',
        componentType: 'board',
        title: 'Simulator Workspace',
        isClosable: false
    };
    const logsConfig = {
        type: 'component',
        componentType: 'logs',
        title: 'Logs',
        isClosable: true
    };
    const componentsConfig = {
        type: 'component',
        componentType: 'components',
        title: 'Components',
        width: 25,
        isClosable: true
    };

    // Right side: board alone or board+logs stacked in a column
    let rightSide;
    if (panelVisible.logs) {
        rightSide = {
            type: 'column',
            width: panelVisible.components ? 75 : 100,
            content: [
                { ...boardConfig, height: 65 },
                { ...logsConfig,  height: 35 }
            ]
        };
    } else {
        rightSide = { ...boardConfig, width: panelVisible.components ? 75 : 100 };
    }

    // Wrap in a row if components pane is also visible
    let rootNode;
    if (panelVisible.components) {
        rootNode = {
            type: 'row',
            content: [ componentsConfig, rightSide ]
        };
    } else {
        rootNode = rightSide;
    }

    return {
        settings: {
            showPopoutIcon: false
        },
        dimensions: {
            minItemWidth: 250,
            minItemHeight: 150
        },
        root: rootNode
    };
}

function togglePanel(componentType) {
    panelVisible[componentType] = !panelVisible[componentType];
    myLayout.loadLayout(buildLayoutConfig());
}

// Keep menu items state checked in sync when panes are closed manually or layout changes
function syncViewMenuChecks() {
    if (!myLayout.rootItem) return;

    const componentsVisible = findComponentItems(myLayout.rootItem, 'components').length > 0;
    const logsVisible       = findComponentItems(myLayout.rootItem, 'logs').length > 0;

    // Keep our state object in sync with manual closes (X button)
    panelVisible.components = componentsVisible;
    panelVisible.logs       = logsVisible;

    viewComponentsBtn.classList.toggle('checked', componentsVisible);
    viewLogsBtn.classList.toggle('checked', logsVisible);
}

// Bind synchronization to layout updates
myLayout.on('stateChanged', syncViewMenuChecks);
myLayout.on('itemDestroyed', syncViewMenuChecks);
myLayout.on('itemCreated', syncViewMenuChecks);

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
            tools.forEach(t => {
                const otherBtn = document.getElementById(`tool-${t.id}`);
                if (otherBtn) otherBtn.classList.remove('active');
            });
            btn.classList.add('active');
            activeTool = tool.id;
            if (activeToolBadge) activeToolBadge.textContent = tool.name;
            log(`Active tool changed to: ${tool.name}`);
            
            // Alter board cursor style based on tool selection
            if (boardVisual) {
                if (tool.id === 'pointer') boardVisual.style.cursor = 'default';
                else if (tool.id === 'connector') boardVisual.style.cursor = 'crosshair';
                else if (tool.id === 'move') boardVisual.style.cursor = 'grab';
                else if (tool.id === 'delete') boardVisual.style.cursor = 'alias';
            }
        };
    }
});

// ----------------------------------------------------
// Zoom Controls
// ----------------------------------------------------
const zoomInBtn  = boardContainer.querySelector('#zoom-in');
const zoomOutBtn = boardContainer.querySelector('#zoom-out');
const zoomFitBtn = boardContainer.querySelector('#zoom-fit');

if (zoomInBtn) zoomInBtn.onclick = () => {
    if (!panzoomInstance) return;
    const cx = boardContainer.clientWidth  / 2;
    const cy = boardContainer.clientHeight / 2;
    panzoomInstance.smoothZoom(cx, cy, 1.3);
};

if (zoomOutBtn) zoomOutBtn.onclick = () => {
    if (!panzoomInstance) return;
    const cx = boardContainer.clientWidth  / 2;
    const cy = boardContainer.clientHeight / 2;
    panzoomInstance.smoothZoom(cx, cy, 1 / 1.3);
};

if (zoomFitBtn) zoomFitBtn.onclick = fitBoard;

// Setup
loadBoard('raspberry_pi_5_breakout').then(setupWebSocket);

import { runLayoutVerificationTests } from './test-suite.js';
setTimeout(runLayoutVerificationTests, 1000);
