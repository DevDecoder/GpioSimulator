# Design Specification: Client Permissions & Auth Validation

This design document specifies the architecture, components, and behavior for securing pin operations across clients, implementing subprotocol header authentication, and fixing visual ownership reversion in the GPIO simulator.

---

## 1. Requirements

### 🔐 Secure Write & Administration Permission System (WebSocket Subprotocol Header)
* **Auth GUID Generation:** The simulator web application (`Program.cs`) generates a unique, secure, randomly initialized `authGuid` (UUID) upon startup.
* **Auth GUID Injection:** The server injects this `authGuid` dynamically into served HTML (`index.html`) using a high-performance compiled regular expression replacing the `{{AUTH_GUID}}` placeholder.
* **Subprotocol-Based Handshake (Browser):** The browser Web UI (`main.js`) retrieves this dynamic GUID from `window.SIMULATOR_AUTH_GUID` and passes it via the WebSocket subprotocol array parameter:
  `new WebSocket(url, ["simulator-admin-token-" + window.SIMULATOR_AUTH_GUID])`.
* **Sec-WebSocket-Protocol Transmission:** The browser automatically transmits this token in the **`Sec-WebSocket-Protocol` HTTP request header** during the upgrade handshake. It satisfies the strict requirement of header-based transmission and completely avoids query parameters.
* **Immunity to School Lockdowns:** Because subprotocols are standard connection negotiations and do not write to cookies or local storage, they are **100% immune to aggressive school IT group policies (GPOs), private/incognito modes, and cookie blocking**.
* **Standard Client Headers:** For external programmatic admin connections (e.g. CLI tools or C# drivers), the server also accepts the token via the `Sec-WebSocket-Protocol` header using the same subprotocol string format.
* **Connection Authorization:** The server extracts the requested subprotocols from the WebSocket connection context. If a match is found, the connection is flagged as **Authorized Admin** (`IsAuthorizedAdmin = true`) and the handshake is completed accepting that specific subprotocol. Standard controllers and third-party tools do not possess this subprotocol and are flagged as unauthorized.

### 🚫 standard GpioController Operation & Ownership Validation
In `System.Device.Gpio`, a controller throws `InvalidOperationException` if an operation is performed on an unowned or unopened pin. To match this codebase semantics, we enforce ownership validation at the WebSocket level for the following messages:
1. **`"write"` / `"read"`:** Modifying/reading the pin value.
2. **`"mode"`:** Changing the configuration mode of the pin.
3. **`"close"`:** Releasing the pin.

* **Admin Connections:** Authorized admin connections are permitted to execute any of these actions on any pin on the simulator, enabling full manual visual override and monitoring.
* **Standard Connections:** Standard connections (e.g. `client=controller`) are restricted. They can only execute these actions on a pin if:
  1. The pin is currently open.
  2. The connection's `clientId` matches the stored owner ID of the pin (i.e. they are the client that originally opened the pin).
* **Unauthorized Rejection:** Any attempt by an unauthorized client to modify or interact with a pin they do not own is rejected. The server logs the event and sends an error response message over the WebSocket matching the exact structure of standard GpioController exception details.

### 🛡️ Non-Admin Session Spoofing Prevention
* **Stateful Connection Mapping:** Standard clients connect via a stateful TCP connection (WebSocket). The server generates a cryptographically secure random `Guid` client ID (`Guid.NewGuid()`) *exclusively* on the server side upon connection.
* **Payload Independence:** Standard clients do not transmit `clientId` values in their WebSocket JSON command payloads. Instead, the server automatically maps incoming commands to the server-side `ClientId` bound directly to that connection session.
* **Impenetrable Boundaries:** Because standard clients cannot dictate their `ClientId` or inject custom IDs in command payloads, session spoofing is structurally impossible.

### 🐛 Visual Pin Ownership Fix
* **Event Integrity:** In `GpioSimulatorEngine.WritePin`, rather than invoking the `PinStateChanged` event with the writer's credentials, lookup the **stored, actual owner** (`_pinOwnerIds[physicalPin]` and `_pinOwnerTypes[physicalPin]`) and propagate those instead.
* **UI Stabilization:** The server broadcasts this corrected actual owner state, ensuring that the visual ownership indicator in all UI instances remains stable and representing the controller, rather than flipping to whichever UI just updated it.

---

## 2. Detailed Technical Design & Changes

### 2.1 UI Layer (`wwwroot/index.html` and `wwwroot/main.js`)

#### `wwwroot/index.html`
* Embed a global script element to make the server-injected `authGuid` accessible:
  ```html
  <!-- Simulator Auth Token Configuration -->
  <script>
      window.SIMULATOR_AUTH_GUID = "{{AUTH_GUID}}";
  </script>
  ```

#### `wwwroot/main.js`
* Pass the authentication token using the subprotocols parameter:
  ```javascript
  const protocols = [];
  if (window.SIMULATOR_AUTH_GUID) {
      protocols.push("simulator-admin-token-" + window.SIMULATOR_AUTH_GUID);
  }
  ws = new WebSocket(`${protocol}//${window.location.host}/ws?client=ui`, protocols);
  ```

---

### 2.2 Server Layer (`Program.cs`)

#### Auth GUID Storage and Injection
* Add `AuthGuidRegex` and `AuthGuid` fields to the `Program` partial class:
  ```csharp
  public partial class Program
  {
      private static readonly Regex CacheBustRegex = new Regex(@"\{\{CACHE_BUST_VERSION\}\}", RegexOptions.Compiled);
      private static readonly Regex AuthGuidRegex = new Regex(@"\{\{AUTH_GUID\}\}", RegexOptions.Compiled);
      private static readonly string AuthGuid = Guid.NewGuid().ToString("D");
      
      private static string ApplyCacheBusting(string html, string version)
      {
          return CacheBustRegex.Replace(html, version);
      }

      private static string ApplyAuthGuid(string html)
      {
          return AuthGuidRegex.Replace(html, AuthGuid);
      }
  }
  ```
* Apply `ApplyAuthGuid` when serving `/` and `/index.html`:
  ```csharp
  var html = await File.ReadAllTextAsync(htmlPath);
  html = ApplyCacheBusting(html, bustVersion);
  html = ApplyAuthGuid(html);
  ```

#### WebSocket Handshake and Connection Authorization
* Add `IsAuthorizedAdmin` to the `ClientConnection` class:
  ```csharp
  public class ClientConnection
  {
      public Guid ClientId { get; set; }
      public WebSocket Socket { get; set; } = null!;
      public string Type { get; set; } = "ui";
      public string Scheme { get; set; } = "Board";
      public bool IsAuthorizedAdmin { get; set; }
  }
  ```
* Parse requested subprotocols and flag connection as admin:
  ```csharp
  string? matchedSubprotocol = null;
  bool isAuthorizedAdmin = false;

  foreach (var proto in context.WebSockets.WebSocketRequestedProtocols)
  {
      if (proto.StartsWith("simulator-admin-token-"))
      {
          var token = proto.Substring("simulator-admin-token-".Length);
          if (token == AuthGuid)
          {
              isAuthorizedAdmin = true;
              matchedSubprotocol = proto;
              break;
          }
      }
  }

  var webSocket = await context.WebSockets.AcceptWebSocketAsync(matchedSubprotocol);
  ```

#### Ownership Validation and Rejection Loop
* Update the incoming WebSocket message handlers:
  ```csharp
  else if (action == "write" || action == "read" || action == "mode" || action == "close")
  {
      bool canExecute = isAuthorizedAdmin;
      if (!canExecute)
      {
          var actualOwner = engine.GetPinOwnerId(physPin);
          canExecute = (actualOwner == clientId.ToString());
      }

      if (canExecute)
      {
          if (action == "write" || action == "read")
          {
              var val = root.GetProperty("value").GetString() ?? "Low";
              engine.WritePin(physPin, val, clientId.ToString(), clientType);
          }
          else if (action == "mode")
          {
              var mode = root.GetProperty("mode").GetString() ?? "Input";
              engine.SetPinMode(physPin, mode);
          }
          else if (action == "close")
          {
              engine.ClosePin(physPin);
          }
      }
      else
      {
          Log($"Blocked unauthorized '{action}' command to physical Pin {physPin} by client {clientId} ({clientType})");
          
          // Send back standard GpioController exception detail
          var resp = JsonSerializer.Serialize(new {
              action = "error",
              pin = physPin,
              errorType = "InvalidOperationException",
              errorMessage = $"Physical Pin {physPin} is not open or is owned by another controller."
          });
          await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(resp)), WebSocketMessageType.Text, true, CancellationToken.None);
      }
  }
  ```

---

### 2.3 Common Layer (`GpioSimulatorEngine.cs`)

#### Pin Ownership Event Broadcast Fix
* Expose `GetPinOwnerId(int physicalPin)`:
  ```csharp
  public string? GetPinOwnerId(int physicalPin)
  {
      return _pinOwnerIds.TryGetValue(physicalPin, out var ownerId) ? ownerId : null;
  }
  ```
* Update `WritePin` to retrieve and propagate the actual pin owner:
  ```csharp
  public void WritePin(int physicalPin, string value, string ownerId, string ownerType)
  {
      if (!_pinModes.ContainsKey(physicalPin)) return;

      _pinValues[physicalPin] = value;
      
      _pinOwnerIds.TryGetValue(physicalPin, out var actualOwnerId);
      _pinOwnerTypes.TryGetValue(physicalPin, out var actualOwnerType);

      PinStateChanged?.Invoke(
          physicalPin, 
          _pinModes[physicalPin], 
          value, 
          actualOwnerId ?? ownerId, 
          actualOwnerType ?? ownerType);
  }
  ```

