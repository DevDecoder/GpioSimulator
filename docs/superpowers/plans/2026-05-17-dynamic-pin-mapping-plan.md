# Implementation Plan - Dynamic Pin Mapping & Switching

This document maps out the concrete tasks required to implement dynamic pin numbering schemes mapping and switching.

---

## Plan & Wave Schedule

```mermaid
gantt
    title Dynamic Pin Mapping & Switching
    dateFormat  YYYY-MM-DD
    section Phase 1: Web Server Mapping
    Implement GSC Mapping Parser       :active, p1, 2026-05-17, 1d
    Add Active Board State & Endpoints :active, p2, after p1, 1d
    Add WebSocket Translations         :active, p3, after p2, 1d
    section Phase 2: Client & Frontend
    Update UI Selection Sync           :p4, after p3, 1d
    Update GpioController WS Uri       :p5, after p4, 1d
    section Phase 3: Sample & CLI
    Implement Schema Switching CLI     :p6, after p5, 1d
    Add Status Layout Printout         :p7, after p6, 1d
```

### Wave 1: Web Server Mapping Engine (`DevDecoder.GpioSimulator.Web`)
1. **GSC Parse Helper**: Add logic to load the `.gsc` file and extract standard `{ physical: logical }` pairs.
2. **State & Endpoints**:
   - Store `activeBoardId` and mapping dictionaries.
   - Default to `"raspberry_pi_5"` and parse its GSC on startup.
   - Implement `POST /api/board/active?boardId=...` to set layout.
3. **WS Connection Client Scheme Metadata**:
   - Parse `scheme` parameter on WebSocket connection.
   - Update `clients` map to track `(WebSocket Socket, string Type, string Scheme)`.
4. **On-the-fly Translation Loop**:
   - Translate pin indices dynamically on message receive and broadcast.

### Wave 2: Front-End UI Sync (`wwwroot/main.js`)
1. **Selection Sync**:
   - In `loadBoard(boardId)`, invoke `fetch(`/api/board/active?boardId=${boardId}`, { method: 'POST' })` to sync server layout state.

### Wave 3: GpioController Websocket Connection Scheme (`System.Device.Gpio`)
1. **URI Query Parameter**:
   - Update `_wsClient.ConnectAsync` to append `&scheme={NumberingScheme}` query parameter.

### Wave 4: Sample & CLI (`DevDecoder.GpioSimulator.Sample`)
1. **Interactive Prompt**: Allow user to choose standard scheme on boot.
2. **Dynamic Switch Command**: Add `scheme` / `schema` command to close old controller and launch new one dynamically.
3. **Layout Status Display**: Add numbering scheme printout to the active console layout.
