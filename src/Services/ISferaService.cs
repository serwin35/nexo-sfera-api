using InsERT.Moria.Sfera;

namespace NexoSferaApi.Services;

public interface ISferaService
{
    Task InitializeAsync();
    Uchwyt GetSfera();
    bool IsConnected { get; }
}
