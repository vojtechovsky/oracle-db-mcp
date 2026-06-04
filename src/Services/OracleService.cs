using Oracle.ManagedDataAccess.Client;
using OracleDbMcp.Configuration;
using System.Text;
using System.Text.Json;

namespace OracleDbMcp.Services;

/// <summary>
/// Handles Oracle database connectivity, query execution, and result serialization.
/// </summary>
public class OracleService
{
    private readonly AppSettings _settings;

    /// <summary>
    /// Initializes the service with application configuration.
    /// </summary>
    public OracleService(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Executes a read-only SQL SELECT query and returns the result as a JSON string.
    /// Optionally exports the result to a CSV file.
    /// </summary>
    /// <param name="username">Oracle username (password is identical).</param>
    /// <param name="dbServer">DNS name of the Oracle server (also used as service name).</param>
    /// <param name="sql">SQL SELECT statement to execute.</param>
    /// <param name="exportCsv">If true, saves the result as a CSV file and includes the path in the response.</param>
    /// <returns>JSON array of result rows, with optional truncation warning and CSV path.</returns>
    public async Task<string> ExecuteQueryAsync(
        string username,
        string dbServer,
        string sql,
        bool exportCsv = false)
    {
        if (!IsSelectStatement(sql))
            return JsonSerializer.Serialize(new { error = "Only SELECT statements are allowed." });

        var connectionString = BuildConnectionString(username, dbServer);
        var maxRows = _settings.QuerySettings.MaxRows;

        try
        {
            await using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new OracleCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            var columns = GetColumnNames(reader);
            var rows = new List<Dictionary<string, object?>>();
            var truncated = false;

            while (await reader.ReadAsync())
            {
                if (rows.Count >= maxRows)
                {
                    truncated = true;
                    break;
                }

                var row = new Dictionary<string, object?>();
                foreach (var col in columns)
                    row[col] = reader.IsDBNull(reader.GetOrdinal(col)) ? null : reader[col];

                rows.Add(row);
            }

            string? csvPath = null;
            if (exportCsv)
                csvPath = await SaveCsvAsync(columns, rows);

            return BuildJsonResponse(rows, truncated, csvPath);
        }
        catch (OracleException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Oracle error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Unexpected error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Returns the list of preconfigured database entries from configuration.
    /// </summary>
    public List<DatabaseEntry> GetDatabases() => _settings.Databases;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static bool IsSelectStatement(string sql)
    {
        var trimmed = sql.TrimStart().ToUpperInvariant();
        return trimmed.StartsWith("SELECT") || trimmed.StartsWith("WITH");
    }

    private static string BuildConnectionString(string username, string dbServer) =>
        $"User Id={username};Password={username};Data Source={dbServer}/{dbServer};";

    private static List<string> GetColumnNames(System.Data.Common.DbDataReader reader)
    {
        var columns = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
            columns.Add(reader.GetName(i));
        return columns;
    }

    private static string BuildJsonResponse(
        List<Dictionary<string, object?>> rows,
        bool truncated,
        string? csvPath)
    {
        var result = new Dictionary<string, object?>
        {
            ["rows"] = rows,
            ["rowCount"] = rows.Count
        };

        if (truncated)
            result["warning"] = "[TRUNCATED: result contains only first 1000 rows]";

        if (csvPath is not null)
            result["csvPath"] = csvPath;

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
    }

    private async Task<string> SaveCsvAsync(
        List<string> columns,
        List<Dictionary<string, object?>> rows)
    {
        var exportDir = Path.GetFullPath(_settings.QuerySettings.CsvExportPath);
        Directory.CreateDirectory(exportDir);

        var fileName = $"export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var filePath = Path.Combine(exportDir, fileName);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(EscapeCsv)));

        foreach (var row in rows)
            sb.AppendLine(string.Join(",", columns.Select(col => EscapeCsv(row[col]?.ToString() ?? ""))));

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        return filePath;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}