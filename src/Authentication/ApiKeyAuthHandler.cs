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
    public const string AuthenticationScheme = "Bearer";
    public const string HeaderName = "Authorization";
    public const string BearerPrefix = "Bearer ";
    public const string LegacyHeaderName = "X-API-Key";
}

/// <summary>
/// Options for API Key authentication
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

/// <summary>
/// API Key authentication handler supporting Bearer token format
/// Supports: Authorization: Bearer {api-key}, X-API-Key: {api-key}, ?api_key={api-key}
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
        string? providedApiKey = null;

        // 1. Check Authorization: Bearer {api-key} header (preferred)
        if (Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var authHeaderValues))
        {
            var authHeader = authHeaderValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith(ApiKeyAuthenticationDefaults.BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                providedApiKey = authHeader[ApiKeyAuthenticationDefaults.BearerPrefix.Length..].Trim();
            }
        }

        // 2. Fallback to X-API-Key header (legacy support)
        if (string.IsNullOrEmpty(providedApiKey) && Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.LegacyHeaderName, out var legacyHeaderValues))
        {
            providedApiKey = legacyHeaderValues.FirstOrDefault();
        }

        // 3. Fallback to query parameter (for testing/webhooks)
        if (string.IsNullOrEmpty(providedApiKey) && Request.Query.TryGetValue("api_key", out var queryValues))
        {
            providedApiKey = queryValues.FirstOrDefault();
        }

        if (string.IsNullOrEmpty(providedApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("API Key not provided. Use 'Authorization: Bearer {api-key}' header."));
        }

        // Validate API key
        var matchingKey = _apiKeySettings.Keys
            .FirstOrDefault(k => k.Key == providedApiKey && k.IsActive);

        if (matchingKey == null)
        {
            Logger.LogWarning("Invalid API key attempt: {ApiKey}", providedApiKey[..Math.Min(8, providedApiKey.Length)] + "...");
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
        }

        // Create claims with optional Nexo credentials override
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, matchingKey.Name),
            new Claim("ApiKeyName", matchingKey.Name),
            new Claim(ClaimTypes.AuthenticationMethod, ApiKeyAuthenticationDefaults.AuthenticationScheme)
        };

        // Add optional override claims for multi-tenant support
        if (!string.IsNullOrEmpty(matchingKey.NexoLogin))
            claims.Add(new Claim("NexoLogin", matchingKey.NexoLogin));
        if (!string.IsNullOrEmpty(matchingKey.NexoPassword))
            claims.Add(new Claim("NexoPassword", matchingKey.NexoPassword));
        if (!string.IsNullOrEmpty(matchingKey.Database))
            claims.Add(new Claim("NexoDatabase", matchingKey.Database));
        if (!string.IsNullOrEmpty(matchingKey.DefaultWarehouse))
            claims.Add(new Claim("NexoWarehouse", matchingKey.DefaultWarehouse));
        if (!string.IsNullOrEmpty(matchingKey.DefaultBranch))
            claims.Add(new Claim("NexoBranch", matchingKey.DefaultBranch));
        if (!string.IsNullOrEmpty(matchingKey.CompanyDescription))
            claims.Add(new Claim("NexoCompany", matchingKey.CompanyDescription));

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.AuthenticationScheme);

        Logger.LogInformation("API key authenticated: {KeyName} (Company: {Company})",
            matchingKey.Name,
            matchingKey.CompanyDescription ?? "default");

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
