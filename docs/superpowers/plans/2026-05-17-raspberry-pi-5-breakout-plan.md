# Raspberry Pi 5 Breakout Breadboard Component Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a stunning, premium, high-fidelity Raspberry Pi 5 Breakout Breadboard component (`raspberry_pi_5_breakout.gsc`), integrate it dynamically into the front-end simulator UI, and enable students to switch between views seamlessly.

**Architecture:** 
1. **Component Specification (`.gsc` JSON)**: Declare the metadata, layout, and complete coordinate system mapping for all 40 GPIO pins matching columns **e** and **f** on Rows 1–20.
2. **High-Fidelity SVG Embed**: Craft an SVG template containing the beige ceramic 60-row breadboard, vertical power buses, labeled gutters, and the deep navy-blue breakout board with premium gold dashed boundaries and crisp white silkscreen labels.
3. **UI Integration**: Extend `index.html` to add a drop-down selector for components, and update `main.js` to support loading custom active board coordinates, preserving all glassmorphic interactive hotspots.

**Tech Stack:** SVG, JSON/GSC, Vanilla CSS, JS/HTML5

---

### Task 1: Create the Component Specification File (`raspberry_pi_5_breakout.gsc`)

**Files:**
- Create: `src/DevDecoder.GpioSimulator.Web/wwwroot/components/raspberry_pi_5_breakout.gsc`
- Test: Dynamic fetch verification on front-end

- [ ] **Step 1: Write the `.gsc` Component Specification**
  Create the `/components/raspberry_pi_5_breakout.gsc` file. Place the full visual SVG template (with 60-row breadboard, navy blue breakout board, labeled rows and gutters, and power buses) alongside the exact 40-pin coordinate mapping matching column **e** ($x=188$) and column **f** ($x=254$) from row 1 to 20 ($y = 60 + (Row-1) \times 20$).

  ```json
  {
    "boardId": "raspberry_pi_5_breakout",
    "displayName": "Raspberry Pi 5 (Breakout Breadboard)",
    "layoutType": "dual_row_header",
    "visuals": {
      "boardColor": "#0b1329",
      "svgWidth": 440,
      "svgHeight": 1320,
      "svgTemplate": "<svg viewBox=\"0 0 440 1320\" width=\"100%\" height=\"100%\" xmlns=\"http://www.w3.org/2000/svg\">\n  <!-- Base Ceramic Breadboard -->\n  <rect x=\"5\" y=\"5\" width=\"430\" height=\"1310\" rx=\"15\" fill=\"#fafaf9\" stroke=\"#e2e8f0\" stroke-width=\"3\" />\n  \n  <!-- Center Divider Trough -->\n  <rect x=\"216\" y=\"40\" width=\"8\" height=\"1230\" fill=\"#e2e8f0\" />\n  \n  <!-- Left & Right Power Bus Vertical Lines -->\n  <line x1=\"38\" y1=\"50\" x2=\"38\" y2=\"1250\" stroke=\"#0284c7\" stroke-width=\"1.5\" />\n  <line x1=\"60\" y1=\"50\" x2=\"60\" y2=\"1250\" stroke=\"#ef4444\" stroke-width=\"1.5\" />\n  <line x1=\"382\" y1=\"50\" x2=\"382\" y2=\"1250\" stroke=\"#ef4444\" stroke-width=\"1.5\" />\n  <line x1=\"404\" y1=\"50\" x2=\"404\" y2=\"1250\" stroke=\"#0284c7\" stroke-width=\"1.5\" />\n  \n  <!-- Power Rail Symbols -->\n  <text x=\"38\" y=\"35\" fill=\"#0284c7\" font-family=\"Outfit, sans-serif\" font-size=\"14\" font-weight=\"bold\" text-anchor=\"middle\">-</text>\n  <text x=\"60\" y=\"35\" fill=\"#ef4444\" font-family=\"Outfit, sans-serif\" font-size=\"14\" font-weight=\"bold\" text-anchor=\"middle\">+</text>\n  <text x=\"382\" y=\"35\" fill=\"#ef4444\" font-family=\"Outfit, sans-serif\" font-size=\"14\" font-weight=\"bold\" text-anchor=\"middle\">+</text>\n  <text x=\"404\" y=\"35\" fill=\"#0284c7\" font-family=\"Outfit, sans-serif\" font-size=\"14\" font-weight=\"bold\" text-anchor=\"middle\">-</text>\n  \n  <text x=\"38\" y=\"1280\" fill=\"#0284c7\" font-family=\"Outfit, sans-serif\" font-size=\"14\" font-weight=\"bold\" text-anchor=\"middle\">-</text>\n  <text x=\"60\" y=\"1280\" fill=\"#ef4444\" font-family=\"Outfit, sans-serif\" font-size=\"14\" font-weight=\"bold\" text-anchor=\"middle\">+</text>\n  <text x=\"382\" y=\"1280\" fill=\"#ef4444\" font-family=\"Outfit, sans-serif\" font-size=\"14\" font-weight=\"bold\" text-anchor=\"middle\">+</text>\n  <text x=\"404\" y=\"1280\" fill=\"#0284c7\" font-family=\"Outfit, sans-serif\" font-size=\"14\" font-weight=\"bold\" text-anchor=\"middle\">-</text>\n  \n  <!-- Column Gutter Header/Footer Labels (a-e, f-j) -->\n  <g fill=\"#94a3b8\" font-family=\"Outfit, sans-serif\" font-size=\"11\" font-weight=\"bold\" text-anchor=\"middle\">\n    <text x=\"100\" y=\"32\">a</text><text x=\"122\" y=\"32\">b</text><text x=\"144\" y=\"32\">c</text><text x=\"166\" y=\"32\">d</text><text x=\"188\" y=\"32\">e</text>\n    <text x=\"254\" y=\"32\">f</text><text x=\"276\" y=\"32\">g</text><text x=\"298\" y=\"32\">h</text><text x=\"320\" y=\"32\">i</text><text x=\"342\" y=\"32\">j</text>\n    \n    <text x=\"100\" y=\"1288\">a</text><text x=\"122\" y=\"1288\">b</text><text x=\"144\" y=\"1288\">c</text><text x=\"166\" y=\"1288\">d</text><text x=\"188\" y=\"1288\">e</text>\n    <text x=\"254\" y=\"1288\">f</text><text x=\"276\" y=\"1288\">g</text><text x=\"298\" y=\"1288\">h</text><text x=\"320\" y=\"1288\">i</text><text x=\"342\" y=\"1288\">j</text>\n  </g>\n  \n  <!-- NAVY BLUE BREAKOUT BOARD OVERLAY (Rows 1-20, Columns d,e,f,g) -->\n  <rect x=\"154\" y=\"45\" width=\"132\" height=\"415\" rx=\"6\" fill=\"#0b1329\" stroke=\"#1e293b\" stroke-width=\"2\" />\n  <rect x=\"159\" y=\"50\" width=\"122\" height=\"405\" rx=\"4\" fill=\"none\" stroke=\"#c5a059\" stroke-dasharray=\"6, 4\" stroke-width=\"1.2\" opacity=\"0.8\" />\n  \n  <!-- Gold Header Title Ribbon -->\n  <text x=\"220\" y=\"1275\" fill=\"#94a3b8\" font-family=\"Outfit, sans-serif\" font-size=\"13\" font-weight=\"bold\" letter-spacing=\"1.5\" text-anchor=\"middle\">GPIO SIMULATOR BREADBOARD</text>\n  \n  <!-- Loop Holes & Layout Generation -->\n  <!-- Dynamic elements representing power connections and pins are added directly -->\n</svg>"
    },
    "pins": [
      { "physical": 1, "logical": null, "name": "3.3V Power", "supportedModes": [], "x": 188, "y": 60 },
      { "physical": 2, "logical": null, "name": "5V Power", "supportedModes": [], "x": 254, "y": 60 },
      { "physical": 3, "logical": 2, "name": "GPIO 2 (SDA)", "supportedModes": ["Input", "Output"], "x": 188, "y": 80 },
      { "physical": 4, "logical": null, "name": "5V Power", "supportedModes": [], "x": 254, "y": 80 },
      { "physical": 5, "logical": 3, "name": "GPIO 3 (SCL)", "supportedModes": ["Input", "Output"], "x": 188, "y": 100 },
      { "physical": 6, "logical": null, "name": "GND", "supportedModes": [], "x": 254, "y": 100 },
      { "physical": 7, "logical": 4, "name": "GPIO 4", "supportedModes": ["Input", "Output"], "x": 188, "y": 120 },
      { "physical": 8, "logical": 14, "name": "GPIO 14 (TX)", "supportedModes": ["Input", "Output"], "x": 254, "y": 120 },
      { "physical": 9, "logical": null, "name": "GND", "supportedModes": [], "x": 188, "y": 140 },
      { "physical": 10, "logical": 15, "name": "GPIO 15 (RX)", "supportedModes": ["Input", "Output"], "x": 254, "y": 140 },
      { "physical": 11, "logical": 17, "name": "GPIO 17", "supportedModes": ["Input", "Output"], "x": 188, "y": 160 },
      { "physical": 12, "logical": 18, "name": "GPIO 18", "supportedModes": ["Input", "Output"], "x": 254, "y": 160 },
      { "physical": 13, "logical": 27, "name": "GPIO 27", "supportedModes": ["Input", "Output"], "x": 188, "y": 180 },
      { "physical": 14, "logical": null, "name": "GND", "supportedModes": [], "x": 254, "y": 180 },
      { "physical": 15, "logical": 22, "name": "GPIO 22", "supportedModes": ["Input", "Output"], "x": 188, "y": 200 },
      { "physical": 16, "logical": 23, "name": "GPIO 23", "supportedModes": ["Input", "Output"], "x": 254, "y": 200 },
      { "physical": 17, "logical": null, "name": "3.3V Power", "supportedModes": [], "x": 188, "y": 220 },
      { "physical": 18, "logical": 24, "name": "GPIO 24", "supportedModes": ["Input", "Output"], "x": 254, "y": 220 },
      { "physical": 19, "logical": 10, "name": "GPIO 10 (MOSI)", "supportedModes": ["Input", "Output"], "x": 188, "y": 240 },
      { "physical": 20, "logical": null, "name": "GND", "supportedModes": [], "x": 254, "y": 240 },
      { "physical": 21, "logical": 9, "name": "GPIO 9 (MISO)", "supportedModes": ["Input", "Output"], "x": 188, "y": 260 },
      { "physical": 22, "logical": 25, "name": "GPIO 25", "supportedModes": ["Input", "Output"], "x": 254, "y": 260 },
      { "physical": 23, "logical": 11, "name": "GPIO 11 (SCLK)", "supportedModes": ["Input", "Output"], "x": 188, "y": 280 },
      { "physical": 24, "logical": 8, "name": "GPIO 8 (CE0)", "supportedModes": ["Input", "Output"], "x": 254, "y": 280 },
      { "physical": 25, "logical": null, "name": "GND", "supportedModes": [], "x": 188, "y": 300 },
      { "physical": 26, "logical": 7, "name": "GPIO 7 (CE1)", "supportedModes": ["Input", "Output"], "x": 254, "y": 300 },
      { "physical": 27, "logical": 0, "name": "GPIO 0 (ID_SD)", "supportedModes": ["Input", "Output"], "x": 188, "y": 320 },
      { "physical": 28, "logical": 1, "name": "GPIO 1 (ID_SC)", "supportedModes": ["Input", "Output"], "x": 254, "y": 320 },
      { "physical": 29, "logical": 5, "name": "GPIO 5", "supportedModes": ["Input", "Output"], "x": 188, "y": 340 },
      { "physical": 30, "logical": null, "name": "GND", "supportedModes": [], "x": 254, "y": 340 },
      { "physical": 31, "logical": 6, "name": "GPIO 6", "supportedModes": ["Input", "Output"], "x": 188, "y": 360 },
      { "physical": 32, "logical": 12, "name": "GPIO 12", "supportedModes": ["Input", "Output"], "x": 254, "y": 360 },
      { "physical": 33, "logical": 13, "name": "GPIO 13", "supportedModes": ["Input", "Output"], "x": 188, "y": 380 },
      { "physical": 34, "logical": null, "name": "GND", "supportedModes": [], "x": 254, "y": 380 },
      { "physical": 35, "logical": 19, "name": "GPIO 19", "supportedModes": ["Input", "Output"], "x": 188, "y": 400 },
      { "physical": 36, "logical": 16, "name": "GPIO 16", "supportedModes": ["Input", "Output"], "x": 254, "y": 400 },
      { "physical": 37, "logical": 26, "name": "GPIO 26", "supportedModes": ["Input", "Output"], "x": 188, "y": 420 },
      { "physical": 38, "logical": 20, "name": "GPIO 20", "supportedModes": ["Input", "Output"], "x": 254, "y": 420 },
      { "physical": 39, "logical": null, "name": "GND", "supportedModes": [], "x": 188, "y": 440 },
      { "physical": 40, "logical": 21, "name": "GPIO 21", "supportedModes": ["Input", "Output"], "x": 254, "y": 440 }
    ]
  }
  ```

- [ ] **Step 2: Compile the complete, high-fidelity SVG graphic template**
  Ensure the `svgTemplate` inside `raspberry_pi_5_breakout.gsc` is fully populated with SVG representations for the 60 rows of holes, labels, power tracks, and white breakout text centered and right/left aligned. (Details of the complete SVG structure are verified in the Task 2 creation step).

- [ ] **Step 3: Save and Verify File Integrity**
  Check that the JSON is fully syntactically correct (no trailing commas, double-escaped quotes).

---

### Task 2: Implement and Inject the High-Fidelity SVG Elements

**Files:**
- Modify: `src/DevDecoder.GpioSimulator.Web/wwwroot/components/raspberry_pi_5_breakout.gsc` (Expand the SVG template contents to include full 60-row loops and labeled pins)

- [ ] **Step 1: Expand GSC file to contain the complete, gorgeous SVG markup**
  To maintain a premium standard, generate all 60 rows of standard holes and the full breakout card labels inline in the GSC file.
  
  *Layout coordinates breakdown*:
  * Standard hole spacing: Columns `a-c` at `x=[100, 122, 144]`, `d-e` (rows 21-60) at `x=[166, 188]`, `f-g` (rows 21-60) at `x=[254, 276]`, `h-j` at `x=[298, 320, 342]`.
  * Rows from `y=60` to `y=1240` in 20px intervals.
  * Gutters centered at `x=155` and `x=287` with row numbers (1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60).
  * Breakout Text labels placed at row increments: Left pins (e.g. 3.3V, SDA1...) aligned right at `x=215` and Even pins (e.g. 5V, 5V, GND...) aligned left at `x=227`.

- [ ] **Step 2: Stage and save**
  Write this fully fleshed component spec to disk.

---

### Task 3: Integrate Component Selector into UI

**Files:**
- Modify: `src/DevDecoder.GpioSimulator.Web/wwwroot/index.html` (Add a visually premium drop-down / selector for switching boards)
- Modify: `src/DevDecoder.GpioSimulator.Web/wwwroot/main.js` (Support dynamically switching boards via selector and reloading socket maps)

- [ ] **Step 1: Add a premium board selector drop-down in `index.html`**
  Modify `/wwwroot/index.html` to add a board selection control inside the header next to the connection status badge. Wrap it in a premium glassmorphic button style.
  
  *Target location:* Line 12–25 (inside `#status-bar` / `.container`).
  
  ```html
  <div class="board-selector-wrapper">
    <label for="board-select">Active Board:</label>
    <select id="board-select">
      <option value="raspberry_pi_5" selected>Raspberry Pi 5</option>
      <option value="raspberry_pi_5_breakout">Raspberry Pi 5 (Breakout Breadboard)</option>
      <option value="arduino_uno">Arduino Uno R3</option>
    </select>
  </div>
  ```

- [ ] **Step 2: Style the new selector in `style.css`**
  Add high-quality modern styling for the select dropdown: glassmorphic background, subtle hover transitions, rounded corners, and clear Outfit typography.
  
  *Target file:* `src/DevDecoder.GpioSimulator.Web/wwwroot/style.css`

- [ ] **Step 3: Update `main.js` to handle board selection changes**
  Modify `/wwwroot/main.js` to register a change listener on `#board-select`. When changed, it must trigger `loadBoard(selectedBoardId)` which fetches the fresh `.gsc` configuration, updates the inline SVG, clears and rebuilds the interactive pin hotspots, and preserves the active WebSocket connection.

---

### Task 4: Compilation and Verification

**Files:**
- Test: Build project and run interactive verification.

- [ ] **Step 1: Build the Web App**
  Run the dotnet build command to confirm that the static assets and configuration are correctly compiled with zero issues.
  Command: `dotnet build`

- [ ] **Step 2: Verify dynamic JSON rendering**
  Spin up the DevDecoder.GpioSimulator.Web server and verify that navigating to `http://localhost:5050/components/raspberry_pi_5_breakout.gsc` fetches the syntactically valid component layout successfully.
