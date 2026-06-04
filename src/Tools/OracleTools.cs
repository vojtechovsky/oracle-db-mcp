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
    /// Executes a SQL SELECT query against an Oracle database and returns results as JSON.
    /// </summary>
    [McpServerTool(Name = "query_oracle")]
    [Description("""
        Executes a read-only SQL SELECT query on an Oracle database.
        Returns a JSON array of result rows. Maximum 1000 rows — a warning is included if truncated.
        Use list_databases first to get available username and dbserver values.
        """)]
    public async Task<string> QueryOracle(
        [Description("Oracle username. The password is always identical to the username.")]
        string username,

        [Description("DNS name of the Oracle server (also used as the service name).")]
        string dbServer,

        [Description("A valid SQL SELECT statement to execute.")]
        string sql,

        [Description("If true, also saves the result as a CSV file and returns the file path.")]
        bool exportCsv = false)
    {
        return await _oracleService.ExecuteQueryAsync(username, dbServer, sql, exportCsv);
    }

    /// <summary>
    /// Returns the list of preconfigured Oracle database connections.
    /// </summary>
    [McpServerTool(Name = "list_databases")]
    [Description("""
        Returns all preconfigured Oracle database connections available in this environment.
        Always call this tool first to discover valid username and dbserver values before running query_oracle.
        """)]
    public string ListDatabases()
    {
        var databases = _oracleService.GetDatabases();
        return JsonSerializer.Serialize(databases, new JsonSerializerOptions { WriteIndented = false });
    }
}