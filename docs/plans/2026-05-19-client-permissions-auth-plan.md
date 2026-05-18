# Implementation Plan: Client Permissions & Auth Validation

This step-by-step implementation plan outlines the specific phases required to deliver the secure write and operations permission system, subprotocol WebSocket authorization, standard GpioController compatibility, and visual pin ownership stabilization.

---

## Phase 0: Common Layer Modification (`GpioSimulatorEngine.cs`)

### 📋 Checklist
- [ ] Add `GetPinOwnerId(int physicalPin)` helper method to return the owner ID for a physical pin.
- [ ] Update `WritePin()` event broadcast payload to fetch and propagate the actual, stored owner details (`_pinOwnerIds[physicalPin]` and `_pinOwnerTypes[physicalPin]`) rather than using the writer's parameters.

### 🔍 Verification
- Compile `DevDecoder.GpioSimulator.Common.csproj` to confirm no syntax errors.

---

## Phase 1: Web Server Auth GUID & Dynamic Injection (`Program.cs`)

### 📋 Checklist
- [ ] Initialize `AuthGuid` once at startup as a Guid string (`Guid.NewGuid().ToString("D")`).
- [ ] Add compiled `AuthGuidRegex` using `RegexOptions.Compiled` for peak performance.
- [ ] Define the helper method `ApplyAuthGuid(string html)` replacing the `{{AUTH_GUID}}` placeholder.
- [ ] In the HTTP GET mappings for `/` and `/index.html`, invoke `ApplyAuthGuid(html)` before returning the response.

### 🔍 Verification
- Compile `DevDecoder.GpioSimulator.Web.csproj` successfully.

---

## Phase 2: WebSocket Connection Authorization & Subprotocol Parsing (`Program.cs`)

### 📋 Checklist
- [ ] Update `ClientConnection` class definition to include the `IsAuthorizedAdmin` boolean property.
- [ ] In the `/ws` request pipeline, iterate through `context.WebSockets.WebSocketRequestedProtocols`.
- [ ] Identify a subprotocol matching the pattern `simulator-admin-token-` + `AuthGuid`.
- [ ] Set `IsAuthorizedAdmin = true` and call `AcceptWebSocketAsync(matchedSubprotocol)` to complete the handshake.

### 🔍 Verification
- Compile the Web project and ensure no broken references.

---

## Phase 3: standard GpioController Operations & Ownership Enforcement (`Program.cs`)

### 📋 Checklist
- [ ] Update `"write"`, `"read"`, `"mode"`, and `"close"` action message handlers in the WebSocket message loop:
  - If the connection `IsAuthorizedAdmin` is `true`, allow the action.
  - Otherwise, lookup the owner of the physical pin. If the pin is open and the owner's ID matches the connection's `ClientId.ToString()`, allow the action.
  - If neither check passes, reject the action, log a warning, and send an `action = "error"` WebSocket message with `errorType = "InvalidOperationException"` and standard error message.

### 🔍 Verification
- Ensure the project builds successfully.

---

## Phase 4: UI Subprotocol Integration (`index.html` & `main.js`)

### 📋 Checklist
- [ ] Add the simulator auth token injection `<script>` block in `wwwroot/index.html` defining `window.SIMULATOR_AUTH_GUID = "{{AUTH_GUID}}";`.
- [ ] Update the `setupWebSocket()` connection in `wwwroot/main.js` to pass `["simulator-admin-token-" + window.SIMULATOR_AUTH_GUID]` as the subprotocol parameter.

### 🔍 Verification
- Perform a manual file review of `index.html` and `main.js` to confirm correct syntax and formatting.

---

## Phase 5: Solution-Wide Validation & Testing

### 📋 Checklist
- [ ] Execute `dotnet build` to ensure the entire solution builds successfully.
- [ ] Execute `dotnet test` to run all unit tests, confirming zero regressions in both the custom and official mock behaviors.

