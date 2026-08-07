namespace OracleDbMcp.Configuration;

/// <summary>
/// Controls runtime behavior of SQL query execution.
/// </summary>
public class QuerySettings
{
    /// <summary>
    /// Default maximum number of rows returned by a single query when MaxRows is not configured.
    /// </summary>
    public const int DefaultMaxRows = 1000;

    /// <summary>
    /// Maximum number of rows returned by a single query. Defaults to 1000.
    /// If the result exceeds this limit, a truncation warning is included in the response.
    /// </summary>
    public int MaxRows { get; set; } = DefaultMaxRows;

    /// <summary>
    /// Maximum time in seconds a single query may run before it is cancelled. Defaults to 60.
    /// A value of 0 disables the timeout.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Upper clamp for the effective query timeout in seconds; 0 = no clamp.
    /// Applies to both the configured CommandTimeoutSeconds and the per-query timeoutSeconds override.
    /// </summary>
    public int MaxTimeoutSeconds { get; set; } = 3600;

    /// <summary>
    /// Directory where optional CSV exports are saved. Resolved to an absolute path once at startup.
    /// Precedence: ORACLE_DB_MCP_EXPORTS_PATH environment variable, then this config value, then the
    /// OS application-data default (%LOCALAPPDATA%\OracleDbMcp\exports on Windows).
    /// An empty value selects the OS default.
    /// </summary>
    public string CsvExportPath { get; set; } = string.Empty;
}