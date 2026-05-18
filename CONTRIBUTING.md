# Contributing to Gpio Simulator

Thank you for your interest in contributing to Gpio Simulator! This document guides you through building, testing, and developing features for the simulator.

---

## 🛠️ Development & Build Guide

### 1. Prerequisites
- **.NET SDK**: Ensure you have the latest .NET SDK installed (the active projects target `.NET 6.0` and test suites target `.NET 8.0` for broad compatibility).

### 2. Building the Solution
Open the root directory in your terminal or IDE and run:
```bash
dotnet restore
dotnet build
```

### 3. Running Unit and Integration Tests
Always run the test suite to verify changes:
```bash
dotnet test
```

---

## ⚡ Cache-Busting in Development (Important!)

The simulator web host dynamically injects the compiled assembly's `AssemblyInformationalVersionAttribute` as a cache-busting query parameter (`?v={{CACHE_BUST_VERSION}}`) to served assets (like `style.css` and `main.js`).

Whenever you modify any frontend style (`.css`) or script (`.js`) during development:
1. **You must bump the minor version number inside `version.json`** (e.g., from `0.5-beta` to `0.6-beta`). Keep it as a two-part version tag (do not specify a patch/revision number like `0.5.1-beta`, as specifying three parts prevents Nerdbank.GitVersioning from automatically using the git height/depth as the revision component).
2. **Rebuild the solution** (`dotnet build`).

This updates the assembly Informational Version (resetting the git height to `0` and incrementing minor), forcing browsers to instantly bust their cache and load your latest changes upon run. Subsequent commits on that version will automatically increment the git-height revision.
