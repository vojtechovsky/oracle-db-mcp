# oracle-db-mcp

A local MCP (Model Context Protocol) server written in C# (.NET 10) that gives AI assistants (Cursor, Claude Desktop, GitHub Copilot) direct read-only access to Oracle databases inside a LAN environment. Built as a companion tool for developers working with DevExpress XPO + Oracle stacks.

---

## Features

- 🔍 **Execute SQL SELECT queries** against Oracle databases via natural language
- 📋 **List preconfigured databases** — AI can present the user with a selection of available environments
- 🛡️ **Read-only enforcement** — only `SELECT` and `WITH` statements are accepted
- ⚠️ **Row limit with truncation warning** — results capped at 1000 rows; truncation is explicitly flagged
- 📁 **Optional CSV export** — save results to disk on demand
- ⚙️ **Central configuration** — all database connections defined in `appsettings.json`
- 🤖 **LLM-optimized output** — results returned as structured JSON for maximum AI reasoning quality

---

## Architecture

```
oracle-db-mcp/
├── Directory.Packages.props        # Central NuGet package version management
├── oracle-db-mcp.slnx              # Solution file (SLNX format)
└── src/
    ├── OracleDbMcp.csproj
    ├── appsettings.json             # Database connections & settings
    ├── Program.cs                   # Host builder & DI setup
    ├── Configuration/
    │   ├── AppSettings.cs           # Root configuration model
    │   ├── DatabaseEntry.cs         # Single DB connection model
    │   └── QuerySettings.cs         # Row limit & export path settings
    ├── Services/
    │   └── OracleService.cs         # Oracle connectivity & query execution
    └── Tools/
        └── OracleTools.cs           # MCP tool definitions
```

---

## MCP Tools

### `list_databases`

Returns all preconfigured Oracle database connections from `appsettings.json`. Always call this tool first to discover valid `username` and `dbserver` values before running queries.

**Parameters:** none

**Example response:**
```json
[
  { "username": "PVO_AIMG", "dbserver": "COMTEST99", "description": "PVO_AIMG, version:trunk" },
  { "username": "PVO132_AIMG", "dbserver": "COMTEST99", "description": "PVO132_AIMG, version:13.2 - bootstrap 2026-02-24" }
]
```

---

### `query_oracle`

Executes a read-only SQL SELECT query on an Oracle database and returns the result as a JSON array of objects.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `username` | string | ✅ | Oracle username. Password is always identical to username. |
| `dbServer` | string | ✅ | DNS name of the Oracle server (also used as the service name). |
| `sql` | string | ✅ | A valid SQL SELECT statement. |
| `exportCsv` | bool | ❌ | If `true`, saves the result as a CSV file and returns the file path. Default: `false`. |

**Example response (normal):**
```json
{
  "rowCount": 3,
  "rows": [
    { "ID": 1, "NAME": "Novak", "STATUS": "ACTIVE" },
    { "ID": 2, "NAME": "Dvorak", "STATUS": "INACTIVE" },
    { "ID": 3, "NAME": "Horak", "STATUS": "ACTIVE" }
  ]
}
```

**Example response (truncated):**
```json
{
  "rowCount": 1000,
  "warning": "[TRUNCATED: result contains only first 1000 rows]",
  "rows": [ ... ]
}
```

**Example response (with CSV export):**
```json
{
  "rowCount": 42,
  "csvPath": "C:\\path\\to\\exports\\export_20260604_092800.csv",
  "rows": [ ... ]
}
```

**Security notes:**
- Only `SELECT` and `WITH` statements are permitted. Any other SQL command returns an error response.
- No network exposure — server runs locally via STDIO transport only.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (required for `dotnet run` mode)
- Access to Oracle databases within your LAN
- An MCP-compatible AI client: [Cursor](https://cursor.sh), Claude Desktop, or GitHub Copilot

> **Note:** No Oracle Client installation is required. The project uses `Oracle.ManagedDataAccess.Core` — a fully managed NuGet package with no native dependencies.

---

## Configuration

Edit `appsettings.json` next to the executable (or in the project root during development):

```json
{
  "Databases": [
    {
      "username": "PVO_AIMG",
      "dbserver": "COMTEST99",
      "description": "PVO_AIMG, version:trunk"
    }
  ],
  "QuerySettings": {
    "MaxRows": 1000,
    "CsvExportPath": "./exports"
  }
}
```

### Configuration reference

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Databases[].username` | string | — | Oracle username. Password is always identical. |
| `Databases[].dbserver` | string | — | DNS name of the Oracle server (EZConnect format). |
| `Databases[].description` | string | — | Human-readable label shown to the AI and user. |
| `QuerySettings.MaxRows` | int | `1000` | Maximum number of rows returned per query. |
| `QuerySettings.CsvExportPath` | string | `./exports` | Directory where CSV exports are saved. |

### Connection string format

The server uses Oracle EZConnect syntax:

```
User Id={username};Password={username};Data Source={dbserver}/{dbserver};
```

Password is always identical to the username — this is by design for the target test environment.

---

## Installation & Usage

### Option A — Development mode (`dotnet run`)

Recommended during active development. Recompiles on each restart so code changes take effect immediately.

**Cursor `mcp.json` configuration:**
```json
{
  "mcpServers": {
    "oracle-db-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\<you>\\source\\repos\\oracle-db-mcp\\src"
      ]
    }
  }
}
```

### Option B — Self-contained executable

Recommended for stable daily use. No .NET SDK required on the machine — the runtime is bundled inside the `.exe`.

**Publish:**
```
dotnet publish src/OracleDbMcp.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output: `src/bin/Release/net10.0/win-x64/publish/OracleDbMcp.exe`

Make sure `appsettings.json` is in the same directory as the `.exe`.

**Cursor `mcp.json` configuration:**
```json
{
  "mcpServers": {
    "oracle-db-mcp": {
      "command": "C:\\Users\\<you>\\source\\repos\\oracle-db-mcp\\src\\bin\\Release\\net10.0\\win-x64\\publish\\OracleDbMcp.exe"
    }
  }
}
```

### Cursor MCP settings location

Open Cursor → **File → Preferences → Cursor Settings → MCP**, or edit directly:
```
%APPDATA%\Cursor\User\globalStorage\cursor.mcp.json
```

---

## Example AI prompts

Once configured, you can use natural language in Cursor chat:

```
List all available databases.
```
```
Show me the last 10 records from the PVOX2.DOCUMENTS table on COMTEST9.
```
```
Query the PVO_AIMG schema on COMTEST9 and find all objects with STATUS = 'ERROR'. Export to CSV.
```
```
What columns does the PVOX2.WORKFLOW_LOG table have?
```

---

## Tech Stack

| Component | Technology |
|-----------|------------|
| Language | C# 13 |
| Framework | .NET 10 |
| MCP SDK | `ModelContextProtocol` 1.3.0 |
| DB driver | `Oracle.ManagedDataAccess.Core` 23.26.100 |
| Hosting | `Microsoft.Extensions.Hosting` 10.0.8 |
| Transport | STDIO |
| Package management | Central Package Management (`Directory.Packages.props`) |
| Solution format | SLNX |
| Target | win-x64, self-contained |

---

## Project Background

This project was inspired by [excel-reader-mcp](https://github.com/vojtechovsky/excel-reader-mcp) — a Python-based MCP server for reading Excel files. `oracle-db-mcp` brings the same concept to Oracle databases, rewritten in C# for seamless integration into a .NET development workflow.

**Primary use case:** Give Cursor AI direct visibility into test Oracle databases used in DevExpress XPO projects, enabling root-cause analysis, schema exploration, and data diagnostics without leaving the IDE.

---

## Limitations (v1)

- Windows only (`win-x64`)
- Oracle databases only
- `SELECT` queries only — no DML or DDL
- STDIO transport only — no HTTP/SSE endpoint
- No authentication — designed for trusted LAN environments
- No connection pooling — each query opens and closes a connection

---

## License

MIT
