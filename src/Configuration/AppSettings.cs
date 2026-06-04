namespace OracleDbMcp.Configuration;

/// <summary>
/// Root configuration object, loaded from appsettings.json.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// List of preconfigured database connections available to the MCP tools.
    /// </summary>
    public List<DatabaseEntry> Databases { get; set; } = [];

    /// <summary>
    /// Settings controlling query execution behavior such as row limits and CSV export path.
    /// </summary>
    public QuerySettings QuerySettings { get; set; } = new();
}