# oracle-db-mcp

A local MCP (Model Context Protocol) server written in C# (.NET 10) that gives AI assistants (Cursor, Claude Desktop, GitHub Copilot) direct read-only access to Oracle databases inside a LAN environment. Built as a companion tool for developers working with DevExpress XPO + Oracle stacks.

---

## Features

- 🔍 **Execute SQL SELECT queries** against Oracle databases via natural language
- 🎯 **Open targeting** — any Oracle schema/server reachable from the machine can be queried using the `USERNAME@DBSERVER` pattern (e.g. `PVOX@COMTEST2`); not limited to configured connections
- 📋 **List commonly used databases** — optional catalog of frequently used connections (hint, not a whitelist)
- 🛡️ **Read-only enforcement** — only `SELECT` and `WITH` statements are accepted
- ⚠️ **Row limit with truncation warning** — results capped at 1000 rows; truncation is explicitly flagged
- 📁 **Optional CSV export** — save results to disk on demand
- 📺 **Console logging** — each query logs `UserName` → `DbServer` → `SQL` to the console
- ⚙️ **Central configuration** — commonly used database connections defined in `appsettings.json`
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

Returns the catalog of *commonly used* Oracle connections defined in `appsettings.json`.

> **Important:** this is **NOT a whitelist.** It is only a hint of frequently used targets.
> Any Oracle schema on any Oracle server reachable from this machine can be queried directly
> via `query_oracle` using the `USERNAME@DBSERVER` pattern (e.g. `PVOX@COMTEST2`).
> You do **not** need to call this tool before running a query.

Each entry includes a `Target` field in `USERNAME@DBSERVER` form — the preferred way to reference a database.

**Parameters:** none

**Example response:**
```json
[
  { "username": "PVO_AIMG", "dbServer": "COMTEST9", "description": "PVO_AIMG, version:trunk", "target": "PVO_AIMG@COMTEST9" },
  { "username": "PVO132_AIMG", "dbServer": "COMTEST9", "description": "PVO132_AIMG, version:13.2 - bootstrap 2026-02-24", "target": "PVO132_AIMG@COMTEST9" }
]
```

---

### `query_oracle`

Executes a read-only SQL SELECT query on **any** Oracle database reachable from this machine and returns the result as a JSON object.

**Targeting:** databases are identified as `USERNAME@DBSERVER`, e.g. `PVOX@COMTEST2`. You are **not** restricted to the connections listed in `appsettings.json`. The target can be passed either as separate `username` + `dbServer` parameters, or as a combined `username="PVOX@COMTEST2"` shorthand (the server is then derived automatically). Password is always identical to the login username.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `username` | string | ✅ | Oracle login username (password is identical). May also carry the combined `USERNAME@DBSERVER` form. |
| `dbServer` | string | ✅ | DNS name / alias of the Oracle server (also used as the service name). Derived automatically when `username` carries the shorthand. |
| `sql` | string | ✅ | A valid SQL SELECT statement. |
| `exportCsv` | bool | ❌ | If `true`, saves the result as a CSV file. The response includes `csvPath` and a `file://` `csvUri`. Default: `false`. |
| `exportMarkdown` | bool | ❌ | If `true`, returns results as a Markdown table instead of JSON. Useful for human-readable output. Default: `false`. |
| `timeoutSeconds` | int | ❌ | Query timeout in seconds. Default `60`. Increase for long-running queries (e.g. `300`). `0` falls back to the configured default. |

**CSV exports & resources:** exported files are written to a deterministic export directory
(`%LOCALAPPDATA%\OracleDbMcp\exports` by default) and are exposed as MCP resources — clients can
read them via `resources/read` using `file://exports/{fileName}` URIs. The export directory can be
overridden via the `ORACLE_DB_MCP_EXPORTS_PATH` environment variable or the `CsvExportPath` config key.

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
  "csvPath": "C:\\Users\\you\\AppData\\Local\\OracleDbMcp\\exports\\export_20260604_092800.csv",
  "csvUri": "file:///C:/Users/you/AppData/Local/OracleDbMcp/exports/export_20260604_092800.csv",
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
    "CommandTimeoutSeconds": 60
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
| `QuerySettings.CommandTimeoutSeconds` | int | `60` | Query timeout in seconds; `0` disables it. Overridable per query via the `timeoutSeconds` tool parameter. |
| `QuerySettings.CsvExportPath` | string | *(OS default)* | Directory where CSV exports are saved. Resolved to an absolute path once at startup. Empty → `%LOCALAPPDATA%\OracleDbMcp\exports` (Windows). |

> **Note:** `QuerySettings.CsvExportPath` is resolved at startup in this precedence order:
> `ORACLE_DB_MCP_EXPORTS_PATH` environment variable → `CsvExportPath` config value → OS application-data default.
> Relative config paths are anchored to the executable directory (never to the process working directory).

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
Work with PVOX@COMTEST2. Show me the last 10 records from the DOCUMENTS table.
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
| MCP SDK | `ModelContextProtocol` 2.1.0 |
| DB driver | `Oracle.ManagedDataAccess.Core` 23.26.300 |
| SQL validation | `SqlParser.Net` (Oracle dialect) |
| Hosting | `Microsoft.Extensions.Hosting` 10.0.10 |
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
