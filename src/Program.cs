using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OracleDbMcp.Configuration;
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
var appSettings = builder.Configuration.Get<AppSettings>()
    ?? throw new InvalidOperationException("Failed to load appsettings.json");

builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton<OracleService>();

// Register MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<OracleTools>();

await builder.Build().RunAsync();