# Handoff — oracle-db-mcp

C# MCP server (ModelContextProtocol 2.1.0, .NET 10) pro read-only SQL dotazy na Oracle. Aktuální stav: opraveno 15 nalezu z code review, build cisty (0 warningu), vse otestovano end-to-end pres MCP klienta.

## Co jsme opravili

- **H1 — Bloated JSON z ODP.NET typu** — `NormalizeCellValue` rozbali OracleDecimal/OracleString/... na primitiva; ordinals sloupcu se pocitaji jednou. Vystup je `{"ID":42}` misto vnorenych objektu.
- **H3 — Chybejici timeout/cancellation** — `CancellationToken` propojen pres `OpenAsync/ExecuteReaderAsync/ReadAsync/WriteAllTextAsync`; `CommandTimeout` nastaven z configu; tool ma novy parametr `timeoutSeconds` (default 60, 0 = fallback na config).
- **H4 — Duplicitni sloupce z JOINu** — `GetColumnNames` deduplikuje jmena case-insensitive (`NAME`, `NAME_2`, `NAME_3`, ...).
- **H5 — Chyby vracene jako uspech** — error cesty hody `McpException` → SDK vrati `isError=true` se zachovanou zpravou (overeno).
- **M5 — Shorthand v `dbServer` zahazoval username** — `dbServer="PVOX@COMTEST2"` doplni i username (explicitni username ma prednost).
- **M3 — CsvExportPath se resila proti CWD** — resolve jednou pri startu; precedence `ORACLE_DB_MCP_EXPORTS_PATH` env var > `CsvExportPath` config > OS default `%LOCALAPPDATA%\OracleDbMcp\exports`.
- **M9 — Komentare/BOM zamitaly validni SELECT** — `IsSelectStatement` pouziva `SqlParser.Net` (dialekt Oracle, ~93 µs) s fallbackem na prefixovou kontrolu; DML (i s leading komentari) se odmita.
- **M6 — Truncation warning hardcodoval "1000 rows"** — interpoluje skutecne `MaxRows`.
- **M4 — Osiřely CSV soubor** — `exportCsv` + `exportMarkdown` → markdown footer s `csvPath` + `file:// csvUri`; popis toolu opraven.
- **M7 — `list_databases` PascalCase klice** — camelCase (`username`, `dbServer`, `description`, `target`).
- **M10 — Chybove cesty se nelogovaly** — `LogError` se stack trace v catch blocich (`Oracle query failed for '{User}' on '{Server}'`).
- **L2 — `sql=null` → NRE** — vyreseno v ramci SqlParser.Net zmeny (`IsNullOrWhiteSpace` guard).
- **L1 — `MaxRows<=0` → prazdny vystup** — clamp na 1000 pri startu.
- **L4 — Omitted sloupce zustavaly v row objektech** — rows se stavi z `visibleColumns`, souhlasí s `nullColumnsOmitted`.
- **L5 — Mrtvy null-check v `Program.cs`** — odstranen `?? throw` (nevincesacne nedosazitelne).

## Bonus / nove funkce

- **MCP resources pro exporty** — nova trida `src/Resources/ExportResources.cs`: `file://exports/` (seznam CSV) a `file://exports/{fileName}` (obsah, text/csv) s ochranou proti path traversal.
- **`csvUri` v odpovedi** — JSON response pri `exportCsv` obsahuje `csvPath` + `file://` URI.
- **Cisty konzolovy log** — poradi `UserName` → `DbServer` → `SQL` (stderr, stdout vyhrazen pro MCP).

## Nove zavislosti

- `SqlParser.Net` 1.1.21 (MIT, ciste C#, Oracle dialekt) — `Directory.Packages.props` + `src/OracleDbMcp.csproj`.
- Aktualizovano: `ModelContextProtocol` 2.1.0, `Microsoft.Extensions.Hosting` 10.0.10, `Oracle.ManagedDataAccess.Core` 23.26.300.

## Odlozeno / by design (bezpecnost — lokalni bootstrap DB, nizka priorita)

- **H2 — Read-only guard nechraní proti DML v WITH klauzuli a `FOR UPDATE`** — zustava; fallback na prefixovou kontrolu ho necha projit (dokumentovano v kodu).
- **M2 — Connection string injection** — nevalidovane `username`/`dbServer`; lokali DB, nizka hrozba.
- **M8 — CSV formula injection** — neescapes `=`, `+`, `-`, `@` na zacatku bunek.
- **L3 — Oracle error vraceny verbatim** — ponechano zamerne (agent se opravi sam).

## Otestovano (end-to-end pres MCP klienta)

- tools schema: `timeoutSeconds` default 60, `CancellationToken` neexponovan
- `list_databases` camelCase
- read-only guard: SELECT s komentari/BOM/WITH projde; UPDATE/INSERT/TRUNCATE/DELETE odmitnuto
- resources: listing + cteni `file://exports/{fileName}`, traversal → error
- `isError=true` u chyb, `isError=false` u uspechu
- konzolovy log poradi UserName → DbServer → SQL; error log se stack trace
