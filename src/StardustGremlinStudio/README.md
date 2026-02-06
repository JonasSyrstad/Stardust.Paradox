# ?? Stardust Gremlin Studio

<div align="center">

**A Modern, Feature-Rich Query Tool for Gremlin Graph Databases**

[![Build Status](https://github.com/JonasSyrstad/Stardust.Paradox/actions/workflows/gremlin-studio-build.yml/badge.svg)](https://github.com/JonasSyrstad/Stardust.Paradox/actions/workflows/gremlin-studio-build.yml)
[![GitHub Release](https://img.shields.io/github/v/release/JonasSyrstad/Stardust.Paradox?include_prereleases&label=latest)](https://github.com/JonasSyrstad/Stardust.Paradox/releases/latest)
[![License](https://img.shields.io/github/license/JonasSyrstad/Stardust.Paradox)](../../LICENSE)

</div>

---

## ? Features

- **?? Multi-Database Support** - Connect to Azure Cosmos DB Gremlin API, Apache TinkerPop servers, JanusGraph, and more
- **?? Intelligent Query Editor** - Syntax highlighting, auto-completion (Ctrl+Space), and real-time validation for Gremlin queries
- **?? Multiple Result Views** - View results as JSON, Table, Tree, or interactive Graph visualization
- **?? Graph Visualization** - Azure Portal-style graph explorer with drag-and-drop nodes, zoom/pan, and edge highlighting
- **?? Local Playground** - Built-in in-memory graph database with sample scenarios for learning and testing
- **?? Scenario Export** - Export query results to JSON or C# code for testing scenarios
- **?? Dark/Light Themes** - Modern UI with customizable themes
- **?? Query History** - Pin and manage frequently used queries
- **?? Secure Connections** - Encrypted credential storage with Windows DPAPI

---

## ?? Download & Install

| Platform | Download | Description |
|----------|----------|-------------|
| **Windows Installer** | [?? GremlinStudio-1.0.0-Setup.exe](https://github.com/JonasSyrstad/Stardust.Paradox/releases/latest/download/GremlinStudio-1.0.0-Setup.exe) | Recommended - Full installer with Start Menu integration |
| **Windows Portable** | [?? GremlinStudio.exe](https://github.com/JonasSyrstad/Stardust.Paradox/releases/latest/download/GremlinStudio.exe) | Single executable, no installation required |

> **System Requirements**: Windows 10 or later (x64)

---

## ??? Build from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup 6](https://jrsoftware.org/isdl.php) (optional, for creating installer)

### Build & Run

```bash
# Clone the repository
git clone https://github.com/JonasSyrstad/Stardust.Paradox.git
cd Stardust.Paradox

# Navigate to the project
cd src/StardustGremlinStudio/Stardust.Paradox.GremlinStudio

# Build and run
dotnet run
```

### Create Installer

```bash
cd src/StardustGremlinStudio/Installer
./build-installer.cmd
```

The installer will be created at `dist/GremlinStudio-1.0.0-Setup.exe`

---

## ?? Project Structure

```
StardustGremlinStudio/
??? Stardust.Paradox.GremlinStudio/       # WPF Application
?   ??? Controls/                          # Custom WPF controls
?   ??? Dialogs/                           # Dialog windows
?   ??? Editor/                            # Gremlin editor components
?   ??? Services/                          # Application services
?   ??? Themes/                            # Dark/Light theme resources
?   ??? ViewModels/                        # MVVM ViewModels
??? Stardust.Paradox.GremlinStudio.Core/  # Core library
?   ??? Connections/                       # Connection management
?   ??? Execution/                         # Query execution
?   ??? Export/                            # Scenario export
?   ??? History/                           # Query history
?   ??? Playground/                        # In-memory graph playground
??? Stardust.Paradox.GremlinStudio.Tests/ # Unit tests
??? Installer/                             # Inno Setup installer
```

---

## ?? Quick Start Guide

### 1. Start with the Local Playground

The built-in playground lets you experiment without connecting to a real database:

1. Launch Gremlin Studio
2. Click **Start** under "Local Playground"
3. Select a scenario (e.g., "Modern" - the classic TinkerPop example graph)
4. Click **Load**

### 2. Run Your First Query

In the query editor, type:
```gremlin
g.V().limit(10)
```

Press **Ctrl+Enter** or click **Run** to execute.

### 3. Explore the Results

- **JSON Tab**: Raw JSON response
- **Table Tab**: Flattened tabular view
- **Tree Tab**: Hierarchical JSON explorer
- **Graph Tab**: Interactive visualization

---

## ?? Connect to Azure Cosmos DB

1. Click **New** in the Connections panel
2. Select **Azure Cosmos DB (Gremlin API)**
3. Paste your connection string from Azure Portal:
   - Go to your Cosmos DB account ? Keys ? Primary Connection String
4. Click **Discover Graphs** - databases and graphs are auto-discovered
5. Select your database and graph
6. Click **Next** to save the connection

---

## ?? Connect to Gremlin Server / TinkerPop

1. Click **New** in the Connections panel
2. Select **Generic Gremlin Server**
3. Enter connection details:
   - **Host**: Your Gremlin server hostname
   - **Port**: Typically 8182
   - **Username/Password**: If authentication is enabled
   - **Use SSL**: Check if using HTTPS
4. Click **Test** to verify connectivity
5. Click **Next** to save the connection

---

## ?? Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+Enter` | Execute query |
| `F5` | Execute query |
| `Escape` | Cancel running query |
| `Ctrl+Space` | Show auto-completion |

---

## ?? Themes

Switch between Dark and Light themes:

1. Scroll to the bottom of the left panel
2. Find "Theme" dropdown
3. Select your preferred theme

---

## ?? Export Scenarios

Export query results for use in testing:

1. Run a query that returns vertices/edges
2. Go to the **Export** tab
3. Enter a scenario name
4. Choose format:
   - **JSON**: For use with ScenarioConnector
   - **C#**: Generates code to recreate the scenario
5. Click **Generate Preview**
6. Click **Save to File**

---

## ?? Configuration

Gremlin Studio stores configuration in:
- **Windows**: `%APPDATA%\Stardust.Paradox.GremlinStudio\`

Files stored:
- `connections.json` - Connection metadata (no secrets)
- `secrets.dat` - Encrypted secrets (Windows DPAPI)
- `query-history.json` - Recent queries
- `settings.json` - Application preferences

---

## ?? Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

### Development Setup

1. Fork the repository
2. Clone your fork
3. Open `Stardust.Paradox.sln` in Visual Studio 2022
4. Set `Stardust.Paradox.GremlinStudio` as the startup project
5. Build and run (F5)

---

## ?? License

This project is part of [Stardust.Paradox](https://github.com/JonasSyrstad/Stardust.Paradox) and is available under the same license.

---

## ?? Acknowledgments

- Built with [Stardust.Paradox](https://github.com/JonasSyrstad/Stardust.Paradox) - Entity Framework-style ORM for Gremlin
- Uses [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) for the code editor
- Inspired by Azure Portal's Cosmos DB Data Explorer
