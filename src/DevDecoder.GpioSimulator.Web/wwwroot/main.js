let activeSchema = null;
let ws = null;
const pinsStateMap = {}; // Maps physicalPin -> { mode: "Input"|"Output"|"None", value: "High"|"Low" }
let activeTooltipPin = null; // Tracks physical pin currently shown in tooltip

const boardSelect = document.getElementById('board-select');
const boardVisual = document.getElementById('visual-board');
const logTerminal = document.getElementById('terminal-log');
const disconnectOverlay = document.getElementById('disconnected-overlay');

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
        const res = await fetch(`components/${boardId}.gsc?_=${Date.now()}`);
        if (!res.ok) throw new Error(`HTTP error! status: ${res.status}`);
        activeSchema = await res.json();
        renderBoard();
        
        // Notify the server of the active board layout
        await fetch(`/api/board/active?boardId=${boardId}`, { method: 'POST' });
        
        // Request fresh pin status from server API after render
        await syncPinStatesFromServer();
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
    boardVisual.innerHTML = "";
    boardVisual.style.position = "relative";
    boardVisual.style.width = "100%";
    boardVisual.style.maxWidth = `${activeSchema.visuals.svgWidth || 600}px`;
    
    // Inject the raw board vector SVG
    const svgContainer = document.createElement('div');
    svgContainer.className = "svg-board-container";
    svgContainer.innerHTML = activeSchema.visuals.svgTemplate;
    boardVisual.appendChild(svgContainer);
    
    // Dynamically draw and overlay pin hotspots based on coordinates
    activeSchema.pins.forEach(pin => {
        const hotspot = document.createElement('div');
        hotspot.className = `pin-hotspot pin-phys-${pin.physical}`;
        
        // Calculate percentages based on design viewBox (600 x 400 default)
        const leftPct = (pin.x / (activeSchema.visuals.svgWidth || 600)) * 100;
        const topPct = (pin.y / (activeSchema.visuals.svgHeight || 400)) * 100;
        
        hotspot.style.left = `calc(${leftPct}% - 10px)`;
        hotspot.style.top = `calc(${topPct}% - 10px)`;
        
        // Add visual class types
        if (pin.name.includes("GND")) hotspot.classList.add("gnd");
        else if (pin.name.includes("5V")) hotspot.classList.add("v5");
        else if (pin.name.includes("3.3")) hotspot.classList.add("v3");
        else hotspot.classList.add("gpio");
        
        // Pin physical number overlay inside circle
        const indexSpan = document.createElement('span');
        indexSpan.textContent = pin.physical;
        hotspot.appendChild(indexSpan);
        
        // Attach interactivity
        hotspot.addEventListener('click', (e) => {
            e.stopPropagation();
            showTooltip(pin, hotspot);
        });
        
        boardVisual.appendChild(hotspot);
        
        // Update its active visualization
        updatePinVisuals(pin.physical);
    });
    
    log(`Rendered ${activeSchema.displayName} vector board successfully.`);
}

function updatePinVisuals(physicalPin) {
    const pinDef = activeSchema?.pins.find(p => p.physical === physicalPin);
    if (!pinDef) return;
    
    const hotspot = boardVisual.querySelector(`.pin-phys-${physicalPin}`);
    if (!hotspot) return;
    
    const state = pinsStateMap[physicalPin] || { mode: "None", value: "Low" };
    
    // Apply styling based on active logic level
    if (state.value === "High") {
        hotspot.classList.add("active");
    } else {
        hotspot.classList.remove("active");
    }
    
    // Dynamically apply input/output mode classes for visualization
    hotspot.classList.remove("mode-input", "mode-output", "mode-none");
    if (state.mode === "Input") {
        hotspot.classList.add("mode-input");
    } else if (state.mode === "Output") {
        hotspot.classList.add("mode-output");
    } else {
        hotspot.classList.add("mode-none");
    }
    
    // Update tooltip if currently open for this pin
    if (activeTooltipPin === physicalPin) {
        refreshTooltipContent(pinDef, hotspot);
    }
}

function showTooltip(pin, anchorEl) {
    activeTooltipPin = pin.physical;
    refreshTooltipContent(pin, anchorEl);
    
    // Position tooltip beautifully relative to anchor element
    const rect = anchorEl.getBoundingClientRect();
    const scrollLeft = window.pageXOffset || document.documentElement.scrollLeft;
    const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
    
    tooltip.style.display = 'block';
    
    // Check if tooltip overflows right side of viewport
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
        
        // Only show interactive toggle if pin is set to Input mode
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
            <span class="close-btn" onclick="closeTooltip()">&times;</span>
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
    
    // Wire up driver state input toggle
    if (pin.logical !== null) {
        const toggle = document.getElementById('tooltip-state-toggle');
        if (toggle) {
            toggle.onchange = (e) => {
                const newState = e.target.checked ? "High" : "Low";
                pinsStateMap[pin.physical].value = newState;
                
                log(`Input manually driven to ${newState} on Pin ${pin.physical}`);
                sendPinState(pin.physical, "read", newState);
                
                // Immediately update board visuals locally
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
        disconnectOverlay.classList.remove('active');
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
        disconnectOverlay.classList.add('active');
        setTimeout(setupWebSocket, 3000);
    };
}

function sendPinState(pin, action, val) {
    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ action: action, pin: pin, value: val }));
    }
}

boardSelect.onchange = (e) => loadBoard(e.target.value);

// Initialize
loadBoard('raspberry_pi_5_breakout').then(setupWebSocket);
