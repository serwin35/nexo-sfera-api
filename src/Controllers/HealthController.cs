using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using NexoSferaApi.Configuration;
using NexoSferaApi.Models;
using NexoSferaApi.Models.Responses;
using NexoSferaApi.Services;
using NexoSferaApi.Helpers;

namespace NexoSferaApi.Controllers;

/// <summary>
/// Health check endpoints - no authentication required
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Tags("Health")]
public class HealthController : ControllerBase
{
    private readonly ISferaService _sferaService;
    private readonly ILogger<HealthController> _logger;
    private readonly SferaSettings _settings;

    public HealthController(ISferaService sferaService, ILogger<HealthController> logger, IOptions<SferaSettings> settings)
    {
        _sferaService = sferaService;
        _logger = logger;
        _settings = settings.Value;
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<HealthCheckResponse> GetHealth()
    {
        var status = _sferaService.IsConnected ? "Healthy" : "Degraded";
        return Ok(new HealthCheckResponse { Status = status });
    }

    /// <summary>
    /// Get Sfera connection status
    /// </summary>
    [HttpGet("sfera")]
    public ActionResult<ApiResponse<SferaStatusResponse>> GetSferaStatus()
    {
        try
        {
            if (!_sferaService.IsConnected)
            {
                return Ok(ApiResponse<SferaStatusResponse>.Ok(new SferaStatusResponse
                {
                    IsConnected = false,
                    Message = "Sfera is not connected"
                }));
            }

            var sfera = _sferaService.GetSfera();

            var response = new SferaStatusResponse
            {
                IsConnected = true,
                Message = "Sfera is connected and ready"
            };

            return Ok(ApiResponse<SferaStatusResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            _logger.LogError("Sfera status check failed with exception type {ExceptionType}", ex.GetType().Name);
            return Ok(ApiResponse<SferaStatusResponse>.Ok(new SferaStatusResponse
            {
                IsConnected = false,
                Message = "Unable to check the Sfera connection."
            }));
        }
    }

    /// <summary>
    /// Reconnect to Sfera. Multi-company note: reconnects ONLY the connection of the
    /// database associated with the calling API key (default database for keys without
    /// a Database override) - other pooled company connections are unaffected.
    /// </summary>
    [HttpPost("reconnect")]
    [EnableRateLimiting("reconnect")]
    public async Task<ActionResult<ApiResponse<bool>>> Reconnect()
    {
        try
        {
            // ReinitializeAsync disposes the stale connection and connects again on the STA thread -
            // InitializeAsync alone is a no-op while a (possibly dead) connection handle exists.
            await _sferaService.ReinitializeAsync();
            return Ok(ApiResponse<bool>.Ok(true, "Successfully reconnected to Sfera"));
        }
        catch (Exception ex)
        {
            _logger.LogError("Sfera reconnect failed with exception type {ExceptionType}", ex.GetType().Name);
            return StatusCode(500, ApiResponse<bool>.Error("Failed to reconnect to Sfera"));
        }
    }

    /// <summary>
    /// Get the result of the SDK synchronization that ran on startup
    /// </summary>
    [HttpGet("sdk-sync")]
    public ActionResult<ApiResponse<SdkSyncResult>> GetSdkSyncResult()
    {
        var result = NexoSdkSynchronizer.LastSyncResult;
        if (result == null)
        {
            return Ok(ApiResponse<SdkSyncResult>.Ok(
                new SdkSyncResult { Status = SdkSyncStatus.NotRun, Message = "SDK sync has not run." }));
        }

        return Ok(ApiResponse<SdkSyncResult>.Ok(result));
    }

    /// <summary>
    /// Live check comparing current SDK version with nexo installation version.
    /// Reports whether a restart is needed to pick up updated DLLs.
    /// </summary>
    [HttpGet("sdk-version-check")]
    public ActionResult<ApiResponse<SdkSyncResult>> CheckSdkVersion()
    {
        var result = NexoSdkSynchronizer.CheckVersions(_settings.NexoInstallPath, AppContext.BaseDirectory);
        return Ok(ApiResponse<SdkSyncResult>.Ok(result));
    }
}

public class HealthCheckResponse
{
    public string Status { get; set; } = "Healthy";
}

public class SferaStatusResponse
{
    public bool IsConnected { get; set; }
    public string? Message { get; set; }
}
