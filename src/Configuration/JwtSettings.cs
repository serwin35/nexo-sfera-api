namespace NexoSferaApi.Configuration;

/// <summary>
/// JWT authentication settings
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Secret key for signing tokens (min 32 characters)
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Token issuer (usually the API URL)
    /// </summary>
    public string Issuer { get; set; } = "NexoSferaApi";

    /// <summary>
    /// Token audience (usually the client application)
    /// </summary>
    public string Audience { get; set; } = "NexoSferaClient";

    /// <summary>
    /// Token expiration time in minutes
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Refresh token expiration time in days
    /// </summary>
    public int RefreshExpirationDays { get; set; } = 7;
}

/// <summary>
/// API key settings for simple authentication
/// </summary>
public class ApiKeySettings
{
    /// <summary>
    /// List of valid API keys
    /// </summary>
    public List<ApiKeyEntry> Keys { get; set; } = new();
}

/// <summary>
/// Single API key entry
/// </summary>
public class ApiKeyEntry
{
    /// <summary>
    /// The API key value
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Name/description for this key
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this key is active
    /// </summary>
    public bool IsActive { get; set; } = true;
}
