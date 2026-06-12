using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NexoSferaApi.Configuration;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Configuration and settings management endpoints
/// </summary>
[ApiController]
[Route("api/settings")]
[Authorize]
[Tags("Settings")]
public class SettingsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<SferaSettings> _sferaSettings;
    private readonly ISferaService _sferaService;
    private readonly ILogger<SettingsController> _logger;
    private readonly IWebHostEnvironment _environment;

    public SettingsController(
        IConfiguration configuration,
        IOptionsMonitor<SferaSettings> sferaSettings,
        ISferaService sferaService,
        ILogger<SettingsController> logger,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _sferaSettings = sferaSettings;
        _sferaService = sferaService;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Get current connection settings (passwords masked)
    /// </summary>
    [HttpGet("connection")]
    [ProducesResponseType(typeof(ApiResponse<ConnectionSettingsDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<ConnectionSettingsDto>> GetConnectionSettings()
    {
        var settings = _sferaSettings.CurrentValue;

        var dto = new ConnectionSettingsDto
        {
            Server = settings.Server,
            Database = settings.Database,
            UseWindowsAuth = settings.UseWindowsAuth,
            SqlLogin = settings.UseWindowsAuth ? null : MaskValue(settings.SqlLogin),
            NexoLogin = settings.NexoLogin,
            Product = settings.Product,
            IsConnected = _sferaService.IsConnected,
            Environment = _environment.EnvironmentName
        };

        return Ok(ApiResponse<ConnectionSettingsDto>.Ok(dto));
    }

    /// <summary>
    /// Get API health and version info
    /// </summary>
    [HttpGet("info")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ApiInfoDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<ApiInfoDto>> GetApiInfo()
    {
        var assembly = typeof(SettingsController).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "1.0.0";

        var dto = new ApiInfoDto
        {
            Name = "Nexo Sfera REST API",
            Version = version,
            Environment = _environment.EnvironmentName,
            DotNetVersion = Environment.Version.ToString(),
            ServerTime = DateTime.UtcNow,
            IsConnected = _sferaService.IsConnected
        };

        return Ok(ApiResponse<ApiInfoDto>.Ok(dto));
    }

    /// <summary>
    /// Test database connection with current settings
    /// </summary>
    [HttpPost("connection/test")]
    [ProducesResponseType(typeof(ApiResponse<ConnectionTestResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ConnectionTestResultDto>>> TestConnection()
    {
        var result = new ConnectionTestResultDto();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (!_sferaService.IsConnected)
            {
                await _sferaService.InitializeAsync();
            }

            // Try to access Sfera to verify connection
            var sfera = _sferaService.GetSfera();
            result.Success = true;
            result.Message = "Connection successful";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Connection failed: {ex.Message}";
            _logger.LogError(ex, "Connection test failed");
        }

        stopwatch.Stop();
        result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

        return Ok(ApiResponse<ConnectionTestResultDto>.Ok(result));
    }

    /// <summary>
    /// Reconnect to Sfera with current settings
    /// </summary>
    [HttpPost("connection/reconnect")]
    [ProducesResponseType(typeof(ApiResponse<ConnectionTestResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ConnectionTestResultDto>>> Reconnect()
    {
        var result = new ConnectionTestResultDto();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Reinitialize on the SDK STA thread - never dispose the singleton service itself,
            // that would permanently kill the work queue for the whole application.
            await _sferaService.ReinitializeAsync();

            result.Success = true;
            result.Message = "Reconnection successful";
            _logger.LogInformation("Sfera reconnected successfully");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Reconnection failed: {ex.Message}";
            _logger.LogError(ex, "Reconnection failed");
        }

        stopwatch.Stop();
        result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

        return Ok(ApiResponse<ConnectionTestResultDto>.Ok(result));
    }

    /// <summary>
    /// Update connection settings (requires app restart or reconnect)
    /// Writes to appsettings.json in Development, returns instructions in Production
    /// </summary>
    [HttpPut("connection")]
    [ProducesResponseType(typeof(ApiResponse<UpdateSettingsResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public ActionResult<ApiResponse<UpdateSettingsResultDto>> UpdateConnectionSettings(
        [FromBody] UpdateConnectionSettingsRequest request)
    {
        var result = new UpdateSettingsResultDto();

        // Validate request
        if (string.IsNullOrWhiteSpace(request.Server))
        {
            return BadRequest(ApiResponse<object>.Error("Server is required"));
        }
        if (string.IsNullOrWhiteSpace(request.Database))
        {
            return BadRequest(ApiResponse<object>.Error("Database is required"));
        }
        if (!request.UseWindowsAuth && string.IsNullOrWhiteSpace(request.SqlLogin))
        {
            return BadRequest(ApiResponse<object>.Error("SQL Login is required when not using Windows auth"));
        }

        // In Development, update appsettings.json
        if (_environment.IsDevelopment())
        {
            try
            {
                var appSettingsPath = Path.Combine(_environment.ContentRootPath, "appsettings.json");
                var json = System.IO.File.ReadAllText(appSettingsPath);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(json);

                using var stream = new MemoryStream();
                using var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true });

                writer.WriteStartObject();

                foreach (var property in jsonDoc.RootElement.EnumerateObject())
                {
                    if (property.Name == "Sfera")
                    {
                        writer.WritePropertyName("Sfera");
                        writer.WriteStartObject();
                        writer.WriteString("Server", request.Server);
                        writer.WriteString("Database", request.Database);
                        writer.WriteBoolean("UseWindowsAuth", request.UseWindowsAuth);
                        writer.WriteString("SqlLogin", request.UseWindowsAuth ? null : request.SqlLogin);
                        writer.WriteString("SqlPassword", request.UseWindowsAuth ? null : request.SqlPassword);
                        writer.WriteString("NexoLogin", request.NexoLogin ?? _sferaSettings.CurrentValue.NexoLogin);
                        writer.WriteString("NexoPassword", request.NexoPassword ?? _sferaSettings.CurrentValue.NexoPassword);
                        writer.WriteString("Product", request.Product ?? _sferaSettings.CurrentValue.Product);
                        writer.WriteEndObject();
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
                writer.Flush();

                var newJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                System.IO.File.WriteAllText(appSettingsPath, newJson);

                result.Updated = true;
                result.Message = "Settings updated. Use /api/settings/connection/reconnect to apply changes.";
                result.RequiresRestart = false;

                _logger.LogInformation("Connection settings updated via API");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update settings file");
                result.Updated = false;
                result.Message = $"Failed to update settings: {ex.Message}";
            }
        }
        else
        {
            // In Production, return environment variable instructions
            result.Updated = false;
            result.RequiresRestart = true;
            result.Message = "In Production, update settings using environment variables and restart the service.";
            result.EnvironmentVariables = new Dictionary<string, string>
            {
                { "SFERA_SERVER", request.Server },
                { "SFERA_DATABASE", request.Database },
                { "SFERA_USE_WINDOWS_AUTH", request.UseWindowsAuth.ToString().ToLower() },
                { "SFERA_SQL_LOGIN", request.SqlLogin ?? "" },
                { "SFERA_SQL_PASSWORD", request.SqlPassword ?? "" },
                { "SFERA_NEXO_LOGIN", request.NexoLogin ?? "" },
                { "SFERA_NEXO_PASSWORD", request.NexoPassword ?? "" },
                { "SFERA_PRODUCT", request.Product ?? "Subiekt" }
            };
        }

        return Ok(ApiResponse<UpdateSettingsResultDto>.Ok(result));
    }

    private static string? MaskValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length <= 4) return "****";
        return value[..2] + "****" + value[^2..];
    }
}

#region DTOs

public class ConnectionSettingsDto
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public bool UseWindowsAuth { get; set; }
    public string? SqlLogin { get; set; }
    public string NexoLogin { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string Environment { get; set; } = string.Empty;
}

public class ApiInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string DotNetVersion { get; set; } = string.Empty;
    public DateTime ServerTime { get; set; }
    public bool IsConnected { get; set; }
}

public class ConnectionTestResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long ResponseTimeMs { get; set; }
}

public class UpdateConnectionSettingsRequest
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public bool UseWindowsAuth { get; set; } = true;
    public string? SqlLogin { get; set; }
    public string? SqlPassword { get; set; }
    public string? NexoLogin { get; set; }
    public string? NexoPassword { get; set; }
    public string? Product { get; set; }
}

public class UpdateSettingsResultDto
{
    public bool Updated { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool RequiresRestart { get; set; }
    public Dictionary<string, string>? EnvironmentVariables { get; set; }
}

#endregion
