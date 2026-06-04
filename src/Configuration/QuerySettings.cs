namespace OracleDbMcp.Configuration;

/// <summary>
/// Controls runtime behavior of SQL query execution.
/// </summary>
public class QuerySettings
{
    /// <summary>
    /// Maximum number of rows returned by a single query. Defaults to 1000.
    /// If the result exceeds this limit, a truncation warning is included in the response.
    /// </summary>
    public int MaxRows { get; set; } = 1000;

    /// <summary>
    /// Directory path where optional CSV exports are saved.
    /// Defaults to an "exports" folder next to the executable.
    /// </summary>
    public string CsvExportPath { get; set; } = "./exports";
}