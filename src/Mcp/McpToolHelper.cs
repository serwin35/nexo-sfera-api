using System.Text.Json;

namespace NexoSferaApi.Mcp;

public static class McpToolHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Success(object data, string? message = null)
        => JsonSerializer.Serialize(new { success = true, message, data }, JsonOpts);

    public static string Error(string message, string? detail = null)
        => JsonSerializer.Serialize(new { success = false, error = message, detail }, JsonOpts);
}
