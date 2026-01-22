using InsERT.Moria.Sfera;

namespace NexoSferaApi.Services;

public interface ISferaService
{
    Task InitializeAsync();
    Uchwyt GetSfera();
    bool IsConnected { get; }

    /// <summary>
    /// Gets a typed manager using reflection-based PodajObiektTypu&lt;T&gt;() call
    /// </summary>
    dynamic? GetManager(string assemblyName, string typeName);
}
