using InsERT.Moria.Sfera;

namespace NexoSferaApi.Services;

public interface ISferaService
{
    Task InitializeAsync();
    Uchwyt GetSfera();
    bool IsConnected { get; }

    /// <summary>
    /// Gets a manager by calling the corresponding method on Sfera (e.g., "Asortymenty" calls sfera.Asortymenty())
    /// </summary>
    dynamic? GetManager(string managerMethodName);

    /// <summary>
    /// Gets a typed manager using reflection-based PodajObiektTypu&lt;T&gt;() call
    /// Use this for interfaces/services, not for standard managers
    /// </summary>
    dynamic? GetManagerByType(string assemblyName, string typeName);
}
