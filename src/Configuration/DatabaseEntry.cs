namespace OracleDbMcp.Configuration;

/// <summary>
/// Represents a single preconfigured Oracle database connection entry.
/// </summary>
public class DatabaseEntry
{
    /// <summary>
    /// Oracle username. The password is always identical to the username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// DNS name of the Oracle database server. Also used as the service name (port alias).
    /// </summary>
    public string DbServer { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of this database entry, shown to the user when selecting a target DB.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Compact target identifier in USERNAME@DBSERVER form (e.g. PVOX@COMTEST2).
    /// This is the preferred way to reference a database in prompts and tool calls.
    /// </summary>
    public string Target => $"{Username}@{DbServer}";
}