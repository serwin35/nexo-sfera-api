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
/// Konfiguracja klucza API z opcjonalnymi danymi dostępowymi do Nexo
/// </summary>
public class ApiKeyEntry
{
    /// <summary>
    /// Klucz API (token Bearer)
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Nazwa/opis klucza (np. "Sklep Online", "Magazyn")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Czy klucz jest aktywny
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Opcjonalny login Nexo dla tego klucza (nadpisuje domyślny z Sfera config)
    /// </summary>
    public string? NexoLogin { get; set; }

    /// <summary>
    /// Opcjonalne hasło Nexo dla tego klucza
    /// </summary>
    public string? NexoPassword { get; set; }

    /// <summary>
    /// Opcjonalna baza danych dla tego klucza (multi-tenant)
    /// </summary>
    public string? Database { get; set; }

    /// <summary>
    /// Opcjonalny domyślny magazyn dla tego klucza
    /// </summary>
    public string? DefaultWarehouse { get; set; }

    /// <summary>
    /// Opcjonalny oddział dla tego klucza
    /// </summary>
    public string? DefaultBranch { get; set; }

    /// <summary>
    /// Opis firmy/kontekstu (np. "Firma ABC Sp. z o.o.")
    /// </summary>
    public string? CompanyDescription { get; set; }
}
