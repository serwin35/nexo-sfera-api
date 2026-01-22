using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NexoSferaApi.Configuration;

namespace NexoSferaApi.Authentication;

/// <summary>
/// API Key authentication scheme name
/// </summary>
public static class ApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "ApiKey";
    public const string HeaderName = "X-API-Key";
}

/// <summary>
/// Options for API Key authentication
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

/// <summary>
/// API Key authentication handler
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly ApiKeySettings _apiKeySettings;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<ApiKeySettings> apiKeySettings)
        : base(options, logger, encoder)
    {
        _apiKeySettings = apiKeySettings.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for API key in header
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var apiKeyHeaderValues))
        {
            // Also check query parameter for easier testing
            if (!Request.Query.TryGetValue("api_key", out var apiKeyQueryValues))
            {
                return Task.FromResult(AuthenticateResult.Fail("API Key not provided"));
            }
            apiKeyHeaderValues = apiKeyQueryValues!;
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();

        if (string.IsNullOrEmpty(providedApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API Key not provided"));
        }

        // Validate API key
        var matchingKey = _apiKeySettings.Keys
            .FirstOrDefault(k => k.Key == providedApiKey && k.IsActive);

        if (matchingKey == null)
        {
            Logger.LogWarning("Invalid API key attempt: {ApiKey}", providedApiKey[..Math.Min(8, providedApiKey.Length)] + "...");
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
        }

        // Create claims
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, matchingKey.Name),
            new Claim("ApiKeyName", matchingKey.Name),
            new Claim(ClaimTypes.AuthenticationMethod, ApiKeyAuthenticationDefaults.AuthenticationScheme)
        };

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.AuthenticationScheme);

        Logger.LogInformation("API key authenticated: {KeyName}", matchingKey.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Extension methods for API Key authentication
/// </summary>
public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(
        this AuthenticationBuilder builder,
        Action<ApiKeyAuthenticationOptions>? configureOptions = null)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationDefaults.AuthenticationScheme,
            configureOptions);
    }
}
