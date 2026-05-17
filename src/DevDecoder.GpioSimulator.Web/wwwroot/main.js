let activeSchema = null;
let ws = null;
const pinsStateMap = {};

const boardSelect = document.getElementById('board-select');
const boardVisual = document.getElementById('visual-board');
const logTerminal = document.getElementById('terminal-log');

function log(message) {
    const time = new Date().toLocaleTimeString();
    const div = document.createElement('div');
    div.textContent = `[${time}] ${message}`;
    logTerminal.appendChild(div);
    logTerminal.scrollTop = logTerminal.scrollHeight;
}

async function loadBoard(boardId) {
    log(`Loading board schema for: ${boardId}...`);
    try {
        const res = await fetch(`board_schemas/${boardId}.json`);
        activeSchema = await res.json();
        renderBoard();
    } catch (err) {
        log(`Error loading schema: ${err}`);
    }
}

function renderBoard() {
    boardVisual.innerHTML = "";
    
    const headerDiv = document.createElement('div');
    headerDiv.className = "pin-header";
    headerDiv.style.backgroundColor = activeSchema.visuals.boardColor || "#1b3a24";
    
    if (activeSchema.layoutType === "dual_row_header") {
        headerDiv.style.gridTemplateColumns = `repeat(${activeSchema.visuals.pinColumns}, 1fr)`;
        
        for (let i = 0; i < activeSchema.pins.length; i += 2) {
            const rowDiv = document.createElement('div');
            rowDiv.className = "pin-row";
            
            const pinLeft = activeSchema.pins[i];
            const pinRight = activeSchema.pins[i+1];
            
            if (pinLeft) rowDiv.appendChild(createPinNode(pinLeft));
            if (pinRight) rowDiv.appendChild(createPinNode(pinRight));
            
            headerDiv.appendChild(rowDiv);
        }
    } else {
        // Fallback single line or split layout
        activeSchema.pins.forEach(pin => {
            const row = document.createElement('div');
            row.className = "pin-row";
            row.appendChild(createPinNode(pin));
            headerDiv.appendChild(row);
        });
    }
    
    boardVisual.appendChild(headerDiv);
    log(`Rendered ${activeSchema.displayName} header visual successfully.`);
}

function createPinNode(pin) {
    const div = document.createElement('div');
    div.className = "pin-node-wrapper";
    div.style.display = "flex";
    div.style.alignItems = "center";
    div.style.gap = "10px";
    
    const node = document.createElement('div');
    node.className = "pin-node";
    node.textContent = pin.physical;
    
    if (pin.name.includes("GND")) node.classList.add("gnd");
    else if (pin.name.includes("5V")) node.classList.add("v5");
    else if (pin.name.includes("3.3V")) node.classList.add("v3");
    
    const label = document.createElement('span');
    label.textContent = pin.name;
    label.style.fontSize = "12px";
    
    const led = document.createElement('div');
    led.className = "led-indicator";
    led.id = `led-pin-${pin.physical}`;
    
    div.appendChild(node);
    div.appendChild(led);
    div.appendChild(label);
    
    if (pin.logical !== null) {
        // If input, make it interactive/clickable
        node.style.cursor = "pointer";
        node.onclick = () => {
            const currentState = pinsStateMap[pin.logical] === "High" ? "Low" : "High";
            pinsStateMap[pin.logical] = currentState;
            led.classList.toggle('active', currentState === "High");
            
            log(`Input Triggered on Pin ${pin.logical}: ${currentState}`);
            sendPinState(pin.logical, "read", currentState);
        };
    }
    
    return div;
}

function setupWebSocket() {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    ws = new WebSocket(`${protocol}//${window.location.host}/ws`);
    
    ws.onopen = () => log("WebSocket Connected to Simulator Server.");
    
    ws.onmessage = (event) => {
        const msg = JSON.parse(event.data);
        if (msg.action === "write") {
            pinsStateMap[msg.pin] = msg.value;
            const physicalPin = activeSchema?.pins.find(p => p.logical === msg.pin)?.physical;
            if (physicalPin) {
                const led = document.getElementById(`led-pin-${physicalPin}`);
                if (led) {
                    led.classList.toggle('active', msg.value === "High");
                }
            }
            log(`Pin ${msg.pin} state set to: ${msg.value}`);
        }
    };
    
    ws.onclose = () => {
        log("WebSocket closed. Attempting reconnect...");
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
loadBoard('raspberry_pi_5').then(setupWebSocket);
