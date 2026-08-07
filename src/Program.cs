using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OracleDbMcp.Configuration;
using OracleDbMcp.Resources;
using OracleDbMcp.Services;
using OracleDbMcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Logging must go to stderr — stdout is reserved for MCP STDIO transport
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
    options.LogToStandardErrorThreshold = LogLevel.Trace);

// Load appsettings.json from executable directory
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

// Bind configuration
var appSettings = builder.Configuration.Get<AppSettings>() ?? new AppSettings();

// Resolve the CSV export directory once at startup (env var > config > OS default) so it
// never depends on the volatile working directory of the MCP host process.
appSettings.QuerySettings.CsvExportPath = ResolveExportPath(appSettings.QuerySettings.CsvExportPath);

// Clamp an invalid MaxRows (0 or negative) to the documented default instead of silently
// returning empty results with a truncation warning.
if (appSettings.QuerySettings.MaxRows < 1)
    appSettings.QuerySettings.MaxRows = QuerySettings.DefaultMaxRows;

builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton<OracleService>();

// Register MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<OracleTools>()
    .WithResources<ExportResources>();

await builder.Build().RunAsync();

static string ResolveExportPath(string configured)
{
    var envPath = Environment.GetEnvironmentVariable("ORACLE_DB_MCP_EXPORTS_PATH");
    if (!string.IsNullOrWhiteSpace(envPath))
        return Path.GetFullPath(envPath);

    if (!string.IsNullOrWhiteSpace(configured))
        return Path.GetFullPath(configured, AppContext.BaseDirectory);

    var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (string.IsNullOrWhiteSpace(baseDir))
        return Path.Combine(Path.GetTempPath(), "OracleDbMcp", "exports");

    return Path.Combine(baseDir, "OracleDbMcp", "exports");
}