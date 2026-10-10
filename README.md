# Uttu Desktop · Native Social Content Copilot

> **100% Native Windows 10 App | Zero Electron / Zero WebView | Buffer MCP via Stdio | Minimal Flat Design**

**Uttu Desktop** is an autonomous local social content copilot (for X, LinkedIn, Threads, Bluesky, Instagram), built as a 100% native Windows 10 desktop application using .NET 8, C#, and optimized WPF, directly inspired by [Otto](https://otto.doti.gg/).

---

## ⚡ Key Features

- **Ultra-Low Memory Footprint:** Consumes only **~15 to 45 MB RAM in standby** with instantaneous millisecond startup and zero Chromium/headless browser overhead.
- **Buffer MCP (Model Context Protocol) via Stdio:** Orchestrates the Buffer MCP server as a managed child process using bidirectional JSON-RPC 2.0 pipes (`StandardInput` / `StandardOutput`) with no exposed local TCP ports.
- **Process Isolation with Windows Job Objects:** Enforces atomic termination of child MCP processes (`JOB_OBJECT_LIMIT_KILL_ON_CLOSE`), guaranteeing zero orphaned background processes even if the application terminates unexpectedly.
- **Windows DPAPI Security:** Buffer Access Tokens and optional AI API keys are protected using Windows Data Protection API (`ProtectedData.Protect` with `DataProtectionScope.CurrentUser`), never stored in plaintext.
- **Interactive Windows 10 Toast Notifications:** Proactive daily morning scheduling (e.g., at 08:30) with native Action Center interactive buttons (`[Approve Now]`, `[Open Uttu]`, `[Dismiss]`). Approving from the toast immediately queues the post via Buffer MCP without opening the main window.
- **Bi-morphic Generation:** Formulates dual versions for each approved theme: compact for **X** (strictly under 280 characters) and structured for **LinkedIn** (vertical double-line spacing with hook in the opening lines).
- **1600 × 900 SkiaSharp Card Renderer:** High-performance native 2D graphics rendering (< 50ms) without headless browsers, generating flat social cards in dark (`#0C0D0E`) and light (`#F3F4F6`) themes.
- **Voice Profile Calibration:** Extracts linguistic metrics from authentic past posts (average sentence length, punctuation density, dominant hook patterns, and prohibited buzzwords).
- **Context Engine & Guidelines (SQLite):** Fast local storage in `uttu.db` with WAL mode enabled.
- **Mandatory Human-in-the-Loop:** No update is ever posted without explicit user confirmation or global shortcut (`Ctrl+Enter`).

---

## 🏛️ System Architecture

```
+-------------------------------------------------------------------------------+
|                       WINDOWS 10 APPLICATION (NATIVE EXE)                     |
|                                                                               |
|  +-------------------------------------------------------------------------+  |
|  |           NATIVE FLAT-DESIGN UI (XAML / WPF)                            |  |
|  |  - Pending Drafts Feed (VirtualizingStackPanel)                         |  |
|  |  - Memories & Guidelines Management (SQLite)                            |  |
|  |  - Inline Mini-Editor with Global Shortcuts (Ctrl+Enter, Del, Ctrl+N)   |  |
|  +------------------------------------+------------------------------------+  |
|                                       |                                       |
|  +------------------------------------v------------------------------------+  |
|  |                     NATIVE WINDOWS SUBSYSTEMS                           |  |
|  |  * System Tray Icon (H.NotifyIcon / TaskbarIcon)                        |  |
|  |  * Windows Toast Notifications (Microsoft.Toolkit.Uwp.Notifications)    |  |
|  |  * Windows DPAPI (ProtectedData.Protect CurrentUser)                    |  |
|  |  * Daily Proactive DispatcherTimer                                      |  |
|  +------------------------------------+------------------------------------+  |
|                                       |                                       |
|  +------------------------------------v------------------------------------+  |
|  |                     LOCAL AGENT ENGINE (UTTU CORE)                      |  |
|  |  * Context & Memories Orchestrator (Native SQLite uttu.db)              |  |
|  |  * Tone Analyzer & Multi-Platform Prompt Builder                        |  |
|  |  * SkiaSharp 2D Renderer (1600x900 Cards in %LocalAppData%\cache)       |  |
|  +------------------------------------+------------------------------------+  |
|                                       | Stdio (JSON-RPC 2.0)                  |
|  +------------------------------------v------------------------------------+  |
|  |                     BUFFER MCP RUNNER (CHILD PROCESS)                   |  |
|  |  * Executes Server/buffer-mcp.js or mcp-buffer-server.exe               |  |
|  |  * Governed by Windows Job Object (JOB_OBJECT_LIMIT_KILL_ON_CLOSE)      |  |
|  +-------------------------------------------------------------------------+  |
+---------------------------------------+---------------------------------------+
                                        | Secure HTTPS
+---------------------------------------v---------------------------------------+
|                       BUFFER API (api.bufferapp.com)                          |
|             [X/Twitter]      [LinkedIn]      [Threads / Bluesky]              |
+-------------------------------------------------------------------------------+
```

---

## 🚀 Quick Start

### Prerequisites
- Windows 10 (Build 19041 or higher)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Node.js (optional, for built-in `Server/buffer-mcp.js` runner)

### Running in Development Mode
```powershell
dotnet run
```

### Running the Native Automated Test Suite (7/7 Subsystems)
```powershell
dotnet run --project Tests/Uttu.Tests.csproj
```

### Publishing Standalone Single-File Binary (.exe)
```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```
The compiled single-file executable will be generated at:
`bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\Uttu.exe`

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl + Enter` | Approves focused draft and dispatches immediately to Buffer queue |
| `Delete` | Discards draft from queue |
| `Ctrl + N` | Instantly adds a new memory/guideline to the context engine |

---

## 🎨 Windows 10 Flat Design Principles

- **Strict Flat Color Palette:**
  - Window Background: `#121212`
  - Cards & Panels: `#1E1E1E`
  - Input Surfaces: `#252525`
  - Borders: Crisp 1px solid `#2B2B2B`
  - Primary Accent: `#0078D7` (Windows 10 Blue)
  - Danger / Discard: `#D83B01` (Flat Red)
- **Typography:** `Segoe UI` (UI elements), `Consolas` (metadata, metrics, character counts).
- **Corners:** Razor-sharp 90-degree corners (`CornerRadius = 0`).
- **Zero Heavy Overheads:** No GPU-draining shadows or heavy acrylic blur layers.

---

## 📄 License

MIT License. Designed and engineered for high productivity and zero bloat on Windows 10.
