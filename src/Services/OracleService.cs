using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using OracleDbMcp.Configuration;
using SqlParser.Net;
using SqlParser.Net.Ast.Expression;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OracleDbMcp.Services;

/// <summary>
/// Handles Oracle database connectivity, query execution, and result serialization.
/// </summary>
public class OracleService
{
    private readonly AppSettings _settings;
    private readonly ILogger<OracleService> _logger;

    /// <summary>
    /// Initializes the service with application configuration.
    /// </summary>
    public OracleService(AppSettings settings, ILogger<OracleService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Executes a read-only SQL SELECT query and returns the result as a JSON string.
    /// Optionally exports the result to a CSV file.
    /// </summary>
    /// <param name="username">Oracle username (password is identical).</param>
    /// <param name="dbServer">DNS name of the Oracle server (also used as service name).</param>
    /// <param name="sql">SQL SELECT statement to execute.</param>
    /// <param name="exportCsv">If true, saves the result as a CSV file and includes the path in the response.</param>
    /// <param name="timeoutSeconds">Query timeout override in seconds. Default 60; pass a larger value for long-running queries. 0 falls back to the configured default.</param>
    /// <returns>JSON array of result rows, with optional truncation warning and CSV path.</returns>
    public async Task<string> ExecuteQueryAsync(
        string username,
        string dbServer,
        string sql,
        bool exportMarkdown = false,
        bool exportCsv = false,
        int timeoutSeconds = 60,
        CancellationToken cancellationToken = default)
    {
        if (!IsSelectStatement(sql))
            throw new McpException("Only SELECT statements are allowed.");

        var (resolvedUser, resolvedServer) = ResolveTarget(username, dbServer);

        if (string.IsNullOrWhiteSpace(resolvedUser) || string.IsNullOrWhiteSpace(resolvedServer))
            throw new McpException("Invalid target. Provide a username and dbServer, or a combined USERNAME@DBSERVER value such as PVOX@COMTEST2.");

        // Console output order: UserName -> DbServer -> SQL (stderr keeps stdout free for MCP)
        _logger.LogInformation(
            "UserName: {UserName}\nDbServer: {DbServer}\nSQL: {Sql}",
            resolvedUser, resolvedServer, sql);

        var connectionString = BuildConnectionString(resolvedUser, resolvedServer);
        var maxRows = _settings.QuerySettings.MaxRows;

        try
        {
            await using var connection = new OracleConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var commandTimeout = timeoutSeconds > 0 ? timeoutSeconds : _settings.QuerySettings.CommandTimeoutSeconds;
            if (_settings.QuerySettings.MaxTimeoutSeconds > 0)
                commandTimeout = Math.Min(commandTimeout, _settings.QuerySettings.MaxTimeoutSeconds);
            await using var command = new OracleCommand(sql, connection)
            {
                CommandTimeout = commandTimeout
            };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var columns = GetColumnNames(reader);
            var ordinals = Enumerable.Range(0, columns.Count).ToArray();
            var rows = new List<Dictionary<string, object?>>();
            var truncated = false;

            while (await reader.ReadAsync(cancellationToken))
            {
                if (rows.Count >= maxRows)
                {
                    truncated = true;
                    break;
                }

                var row = new Dictionary<string, object?>(columns.Count);
                for (int i = 0; i < columns.Count; i++)
                {
                    var ordinal = ordinals[i];
                    var value = reader.GetValue(ordinal);
                    row[columns[i]] = value is DBNull ? null : NormalizeCellValue(value);
                }

                rows.Add(row);
            }

            // Detect fully-null columns — shared for all output formats
            var nullColumns = columns
                .Where(col => rows.All(row => row[col] is null))
                .ToHashSet();

            var visibleColumns = columns
                .Where(col => !nullColumns.Contains(col))
                .ToList();

            string? csvPath = null;
            if (exportCsv)
            {
                csvPath = await SaveCsvAsync(visibleColumns, rows, cancellationToken);
            }

            if (exportMarkdown)
            {
                return BuildMarkdownResponse(visibleColumns, rows, truncated, nullColumns, maxRows, csvPath);
            }

            return BuildJsonResponse(visibleColumns, rows, truncated, csvPath, nullColumns, maxRows);
        }
        catch (OracleException ex)
        {
            _logger.LogError(ex, "Oracle query failed for '{UserName}' on '{DbServer}'", resolvedUser, resolvedServer);
            throw new McpException($"Oracle error: {ex.Message}", ex);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error for query on '{DbServer}'", resolvedServer);
            throw new McpException($"Unexpected error: {ex.Message}", ex);
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
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        try
        {
            return DbUtils.Parse(sql.TrimStart('\uFEFF'), DbType.Oracle) is SqlSelectExpression;
        }
        catch
        {
            // SqlParser.Net does not know every Oracle construct. Fall back to a prefix check
            // so that valid-but-exotic SELECT/WITH statements are not rejected. DML inside a
            // WITH clause remains an accepted limitation (see code-review finding H2).
            var trimmed = sql.TrimStart('\uFEFF', ' ', '\t', '\r', '\n').ToUpperInvariant();
            return trimmed.StartsWith("SELECT") || trimmed.StartsWith("WITH");
        }
    }

    /// <summary>
    /// Normalizes the target into a login username and dbServer pair.
    /// Accepts the combined USERNAME@DBSERVER shorthand in either parameter,
    /// e.g. username="PVOX@COMTEST2" or dbServer="PVOX@COMTEST2".
    /// </summary>
    private static (string Username, string DbServer) ResolveTarget(string username, string dbServer)
    {
        var user = username?.Trim() ?? string.Empty;
        var server = dbServer?.Trim() ?? string.Empty;

        if (user.Contains('@'))
        {
            var parts = user.Split('@', 2);
            user = parts[0].Trim();
            server = parts[1].Trim();
        }
        else if (server.Contains('@'))
        {
            var parts = server.Split('@', 2);
            server = parts[1].Trim();
            if (string.IsNullOrWhiteSpace(user))
                user = parts[0].Trim();
        }

        return (user, server);
    }

    private static string BuildConnectionString(string username, string dbServer) =>
        $"User Id={username};Password={username};Data Source={dbServer}/{dbServer};";

    private static List<string> GetColumnNames(System.Data.Common.DbDataReader reader)
    {
        var columns = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var baseName = reader.GetName(i);
            var name = baseName;
            var suffix = 2;
            while (!used.Add(name))
                name = $"{baseName}_{suffix++}";
            columns.Add(name);
        }
        return columns;
    }

    /// <summary>
    /// Converts a raw ODP.NET cell value into a compact JSON-friendly CLR value.
    /// Oracle wrapper types (OracleDecimal, OracleString, ...) are unwrapped to their
    /// underlying primitives; any unknown type falls back to an invariant-culture string so
    /// that System.Text.Json never reflects over provider-specific nested objects.
    /// </summary>
    private static object? NormalizeCellValue(object value) => value switch
    {
        OracleDecimal d => d.IsNull ? null : d.Value,
        OracleString s => s.IsNull ? null : s.Value,
        OracleDate dt => dt.IsNull ? null : dt.Value,
        OracleTimeStamp ts => ts.IsNull ? null : ts.Value,
        OracleTimeStampLTZ ltz => ltz.IsNull ? null : ltz.Value,
        OracleTimeStampTZ tz => tz.IsNull ? null : tz.Value,
        OracleIntervalDS ids => ids.IsNull ? null : ids.Value,
        OracleIntervalYM iym => iym.IsNull ? null : iym.Value,
        OracleBinary bin => bin.IsNull ? null : bin.Value,
        OracleBlob blob => blob.IsNull ? null : blob.Value,
        OracleClob clob => clob.IsNull ? null : clob.Value,
        OracleXmlType xml => xml.IsNull ? null : xml.Value,
        OracleRefCursor => "<REF CURSOR>",
        _ => value is string or bool or byte[] or DateTime or DateTimeOffset or Guid
                or sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
            ? value
            : Convert.ToString(value, CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// Serializes the query result as a compact JSON object containing the rows, the row count,
    /// and optional metadata (truncation warning, CSV export path, omitted all-NULL columns).
    /// Column keys are preserved as returned by the database; fully-null columns are stripped
    /// from the output to reduce noise.
    /// </summary>
    private static string BuildJsonResponse(
    List<string> columns,
    List<Dictionary<string, object?>> rows,
    bool truncated,
    string? csvPath,
    HashSet<string> nullColumns,
    int maxRows)
    {
        // columns already contain only visible (non-fully-null) columns — see ExecuteQueryAsync.
        var cleanedRows = rows.Select(row => columns.ToDictionary(col => col, col => row[col])).ToList();

        var result = new Dictionary<string, object?>
        {
            ["rows"] = cleanedRows,
            ["rowCount"] = cleanedRows.Count
        };

        if (truncated)
            result["warning"] = $"[TRUNCATED: result contains only first {maxRows} rows]";

        if (csvPath is not null)
        {
            result["csvPath"] = csvPath;
            result["csvUri"] = new Uri(csvPath).AbsoluteUri;
        }

        if (nullColumns.Count > 0)
            result["nullColumnsOmitted"] = nullColumns.OrderBy(c => c).ToList();

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Renders the query result as a GitHub-flavored Markdown table for human-readable output.
    /// Pipe characters inside cell values are escaped, newlines are flattened to spaces,
    /// all-NULL columns are excluded, and a footnote lists the omitted columns together with
    /// a truncation warning when the result set was capped.
    /// </summary>
    private static string BuildMarkdownResponse(
        List<string> columns,
        List<Dictionary<string, object?>> rows,
        bool truncated,
        HashSet<string> nullColumns,
        int maxRows,
        string? csvPath)
    {
        var sb = new StringBuilder();

        // columns already contain only visible (non-fully-null) columns — see ExecuteQueryAsync.

        // Header row
        sb.AppendLine("| " + string.Join(" | ", columns) + " |");

        // Separator row
        sb.AppendLine("| " + string.Join(" | ", columns.Select(_ => "---")) + " |");

        // Data rows
        foreach (var row in rows)
        {
            var cells = columns.Select(col =>
            {
                var value = row[col]?.ToString() ?? "NULL";
                return value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
            });
            sb.AppendLine("| " + string.Join(" | ", cells) + " |");
        }

        // Null columns note
        if (nullColumns.Count > 0)
        {
            var columnList = string.Join(", ", nullColumns.Select(c => $"`{c}`"));
            sb.AppendLine($"\n> ℹ️ **Columns with all NULL values (omitted):** {columnList}");
        }

        if (truncated)
            sb.AppendLine($"\n> ⚠️ **TRUNCATED:** result contains only first {maxRows} rows.");

        if (csvPath is not null)
        {
            var csvUri = new Uri(csvPath).AbsoluteUri;
            sb.AppendLine($"\n> 📄 **CSV export:** `{csvPath}` ({csvUri})");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes the result rows to a UTF-8 CSV file inside the configured export directory and
    /// returns the absolute path of the created file. Values containing commas, double quotes,
    /// or newlines are quoted and embedded quotes are doubled per RFC 4180.
    /// </summary>
    private async Task<string> SaveCsvAsync(
        List<string> columns,
        List<Dictionary<string, object?>> rows,
        CancellationToken cancellationToken = default)
    {
        // CsvExportPath is resolved to an absolute path once at startup (see Program.cs)
        var exportDir = _settings.QuerySettings.CsvExportPath;
        Directory.CreateDirectory(exportDir);

        var fileName = $"export_{DateTime.Now:yyyyMMdd_HHmmssfff}.csv";
        var filePath = Path.Combine(exportDir, fileName);

        await using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
        await writer.WriteLineAsync(string.Join(",", columns.Select(EscapeCsv)).AsMemory(), cancellationToken);

        foreach (var row in rows)
            await writer.WriteLineAsync(string.Join(",", columns.Select(col => EscapeCsv(row[col]?.ToString() ?? ""))).AsMemory(), cancellationToken);

        return filePath;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}