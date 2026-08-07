using ModelContextProtocol.Server;
using OracleDbMcp.Services;
using System.ComponentModel;
using System.Text.Json;

namespace OracleDbMcp.Tools;

/// <summary>
/// MCP tools exposed to the AI assistant for Oracle database interaction.
/// </summary>
[McpServerToolType]
public class OracleTools
{
    private readonly OracleService _oracleService;

    /// <summary>
    /// Initializes the tools with the Oracle service via dependency injection.
    /// </summary>
    public OracleTools(OracleService oracleService)
    {
        _oracleService = oracleService;
    }

    /// <summary>
    /// Executes a read-only SQL SELECT query against any Oracle database reachable from this machine.
    /// The target may be supplied as separate username + dbServer parameters or as the combined
    /// USERNAME@DBSERVER shorthand (e.g. PVOX@COMTEST2), in which case the dbServer is derived
    /// automatically. The server is intentionally not restricted to the connections defined in
    /// appsettings.json; that catalog is only an optional hint.
    /// </summary>
    /// <param name="username">Oracle login username; password is always identical to it. May also carry the combined USERNAME@DBSERVER form.</param>
    /// <param name="dbServer">DNS name or alias of the Oracle server (also used as the service name). Derived from username when the shorthand form is used.</param>
    /// <param name="sql">A valid SQL SELECT statement to execute.</param>
    /// <param name="exportMarkdown">If true, returns results as a Markdown table instead of JSON.</param>
    /// <param name="exportCsv">If true, also saves the result as a CSV file and returns the file path.</param>
    /// <param name="timeoutSeconds">Query timeout override in seconds. Default 60; pass a larger value for long-running queries. 0 falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token supplied by the MCP host; not exposed in the tool schema.</param>
    /// <returns>Result as a JSON string (or Markdown table when exportMarkdown is set), or a JSON error object.</returns>
    [McpServerTool(Name = "query_oracle")]
    [Description("""
    Executes a read-only SQL SELECT query against ANY Oracle database reachable from this machine.

    TARGET IDENTIFICATION:
    - A target is written as USERNAME@DBSERVER, e.g. PVOX@COMTEST2 or PVO_AIMG@COMTEST9.
    - You are NOT restricted to the connections listed in appsettings.json. The optional
      list_databases tool only shows commonly used EXAMPLES, not a whitelist. Any Oracle schema
      (username) on any reachable Oracle server (dbServer) can be targeted directly.
    - The password is always identical to the login username.

    HOW TO PASS THE TARGET:
    - Either pass username and dbServer separately (username="PVOX", dbServer="COMTEST2"), or
      pass the combined shorthand in the username parameter (username="PVOX@COMTEST2") and the
      dbServer is derived automatically.
    - When the user says something like "work with PVOX@COMTEST2", translate it directly into
      username and dbServer values - do NOT call list_databases first.

    Returns a JSON object with rows/rowCount by default. Maximum 1000 rows - a warning is included if truncated.
    Use exportMarkdown=true to get results as a Markdown table instead of JSON.
    Use exportCsv=true to also save results as a CSV file (the response includes csvPath and a file:// csvUri;
    exported files are also available as MCP resources under file://exports/{fileName}).
    """)]
    public async Task<string> QueryOracle(
        [Description("Oracle login username, e.g. PVOX. May also be passed as the combined target USERNAME@DBSERVER (e.g. PVOX@COMTEST2) and the dbServer is derived automatically. Password is always identical to the username.")]
    string username,

        [Description("DNS name or alias of the Oracle server (also used as the service name), e.g. COMTEST2. May be omitted if username carries the combined USERNAME@DBSERVER form. May also itself be passed as USERNAME@DBSERVER.")]
    string dbServer,

        [Description("A valid SQL SELECT statement to execute.")]
    string sql,

        [Description("If true, returns results as a Markdown table instead of JSON. Useful for human-readable output.")]
    bool exportMarkdown = false,

        [Description("If true, also saves the result as a CSV file and returns the file path and file:// URI. Combines with exportMarkdown by adding the export link to the Markdown output.")]
    bool exportCsv = false,

        [Description("Overrides the query timeout in seconds. Default is 60 s. Increase for long-running queries (e.g. 300). A value of 0 falls back to the configured default.")]
    int timeoutSeconds = 60,

        CancellationToken cancellationToken = default)
    {
        return await _oracleService.ExecuteQueryAsync(username, dbServer, sql, exportMarkdown, exportCsv, timeoutSeconds, cancellationToken);
    }

    /// <summary>
    /// Returns the list of commonly used Oracle database connections from configuration.
    /// </summary>
    [McpServerTool(Name = "list_databases")]
    [Description("""
        Optional - returns the catalog of commonly used Oracle connections defined in appsettings.json.

        IMPORTANT: this is NOT a whitelist. It is only a hint of frequently used targets.
        Any Oracle schema on any Oracle server reachable from this machine can be queried directly
        with query_oracle using the USERNAME@DBSERVER pattern (e.g. PVOX@COMTEST2).
        You do NOT need to call this tool before running query_oracle.
        """)]
    public string ListDatabases()
    {
        var databases = _oracleService.GetDatabases();
        return JsonSerializer.Serialize(databases, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}