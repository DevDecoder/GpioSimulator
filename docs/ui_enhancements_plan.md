# Implementation Plan: Simulator UI & Sizing Enhancements

This plan outlines five premium visual and layout enhancements to the GPIO Simulator workspace, ensuring a professional, desktop-like, and highly responsive user experience.

---

## 1. Planned Enhancements & Technical Architecture

### 1.1 Brand Typography (Task 1)
*   **Goal**: Make the "Simulator" part of the brand name non-bold and butt it up against "GPIO".
*   **Implementation**:
    *   Change the markup in [index.html](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/index.html) to `<span class="brand-name"><strong>GPIO</strong>Simulator</span>`.
    *   In [style.css](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/style.css), update the `.brand-name` selector to set `font-weight: 400` and style `.brand-name strong` to set `font-weight: 800`.
    *   Since `text-transform: uppercase` is applied, this yields a seamless `GPIOSIMULATOR` where the first four letters are bold and the rest are regular weight, without spaces.

### 1.2 Theme-Responsive Activity Log Pane (Task 2)
*   **Goal**: Support theme switches on the terminal log pane, so it changes to light colors in Light Mode instead of staying dark.
*   **Implementation**:
    *   Define four new theme variables under `:root` (Dark Mode) and `body.light-theme` (Light Mode) in [style.css](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/style.css):
        *   `--terminal-bg` (Pane background)
        *   `--terminal-log-bg` (Code area background)
        *   `--terminal-text` (Log line text color)
        *   `--terminal-border` (Internal code block border)
    *   Update selectors `#terminal-panel` and `#terminal-log` to consume these variables instead of hardcoded dark colors.

### 1.3 Custom Microchip SVG Icon (Task 3)
*   **Goal**: Replace the hardcoded emoji plug `🔌` on board component catalog cards with a sleek, custom SVG microchip.
*   **Implementation**:
    *   Replace `🔌` in the template string inside the `createBoardCard` factory function in [main.js](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/main.js) with a professional, handcrafted inline SVG depicting a classic silicon IC/microchip with 8 pins.
    *   Add styling rules in [style.css](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/style.css) for `.comp-icon svg` to ensure smooth color transitions during hover and active state selection.

### 1.4 Golden Layout Window Resizing (Task 4)
*   **Goal**: Fix the sizing issue where the Golden Layout workspace does not adjust when the browser window is resized, causing rightmost controls to be cut off.
*   **Implementation**:
    *   Initialize a browser-native `ResizeObserver` targetting the `#layout-container` element in [main.js](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/main.js).
    *   Inside the observer callback, fetch the exact client rect size and invoke the Golden Layout v2 standard `setSize(width, height)` API to fluidly fit the container.

### 1.5 Frameless Board Workspace Integration (Task 5)
*   **Goal**: Seamlessly embed the main board simulator canvas so it looks integrated with the workspace layout rather than being styled in a standard window pane with titles/borders.
*   **Implementation**:
    *   In the `board` factory registration function inside [main.js](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/main.js), locate the parent stack element `.lm_stack` via a microtask, and add a custom class `no-header`.
    *   In [style.css](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/style.css), write styles under `.lm_stack.no-header` to hide `.lm_header` (`display: none !important`) and strip all pane borders/backgrounds on the descendant `.lm_content` container.

---

## 2. Implementation Checklist

- [ ] **Step 1**: Modify typography inside [index.html](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/index.html) and [style.css](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/style.css).
- [ ] **Step 2**: Add terminal layout CSS custom properties and apply them to `#terminal-panel` and `#terminal-log` in [style.css](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/style.css).
- [ ] **Step 3**: Replace `🔌` with custom inline SVG in [main.js](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/main.js) and configure hover styling in [style.css](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/style.css).
- [ ] **Step 4**: Introduce `ResizeObserver` resize logic in [main.js](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/main.js).
- [ ] **Step 5**: Set up `no-header` class assignment on workspace parent stack inside [main.js](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/main.js), and write clean frameless layout rules in [style.css](file:///Users/craigdean/Repos/GpioSimulator/src/DevDecoder.GpioSimulator.Web/wwwroot/style.css).

---

## 3. Verification Plan

Since the user requested to skip subagent testing to conserve resources, we will implement these improvements precisely and invite the user to verify the changes directly.
