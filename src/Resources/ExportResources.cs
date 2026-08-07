using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OracleDbMcp.Configuration;
using System.Text;

namespace OracleDbMcp.Resources;

/// <summary>
/// Exposes the CSV files produced by query_oracle exports as MCP resources so that MCP clients
/// can discover and read them via resources/read using file://exports/... URIs.
/// </summary>
[McpServerResourceType]
public class ExportResources
{
    private readonly AppSettings _settings;

    /// <summary>
    /// Initializes the resources with the application configuration.
    /// </summary>
    public ExportResources(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Lists the file names of previously exported CSV files, newest first.
    /// </summary>
    [McpServerResource(UriTemplate = "file://exports", Name = "Exports", Title = "Exported CSV files", MimeType = "text/plain")]
    public string ListExports()
    {
        var exportDir = _settings.QuerySettings.CsvExportPath;
        if (string.IsNullOrWhiteSpace(exportDir) || !Directory.Exists(exportDir))
            return string.Empty;

        return string.Join("\n", Directory.EnumerateFiles(exportDir, "*.csv")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Select(Path.GetFileName));
    }

    /// <summary>
    /// Returns the content of a previously exported CSV file.
    /// </summary>
    /// <param name="fileName">Name of the exported CSV file inside the export directory.</param>
    /// <param name="cancellationToken">Cancellation token supplied by the MCP host.</param>
    [McpServerResource(UriTemplate = "file://exports/{fileName}", Name = "Export", Title = "Exported CSV file", MimeType = "text/csv")]
    public async Task<TextResourceContents> ReadExport(string fileName, CancellationToken cancellationToken)
    {
        var exportDir = _settings.QuerySettings.CsvExportPath;
        var fullPath = Path.GetFullPath(fileName, exportDir);
        if (!fullPath.StartsWith(exportDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new McpException($"Invalid export file name: {fileName}");

        if (!File.Exists(fullPath))
            throw new McpException($"Export file not found: {fileName}");

        return new TextResourceContents
        {
            Uri = $"file://exports/{fileName}",
            MimeType = "text/csv",
            Text = await File.ReadAllTextAsync(fullPath, Encoding.UTF8, cancellationToken),
        };
    }
}
