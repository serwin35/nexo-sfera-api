using InsERT.Moria.Sfera;
using InsERT.Mox.Product;
using Microsoft.Extensions.Options;
using NexoSferaApi.Configuration;

namespace NexoSferaApi.Services;

public class SferaService : ISferaService, IDisposable
{
    private readonly SferaSettings _settings;
    private readonly ILogger<SferaService> _logger;
    private Uchwyt? _sfera;
    private readonly object _lock = new();

    public bool IsConnected => _sfera != null;

    public SferaService(IOptions<SferaSettings> settings, ILogger<SferaService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                if (_sfera != null) return;

                try
                {
                    _logger.LogInformation("Connecting to Sfera: Server={Server}, Database={Database}",
                        _settings.Server, _settings.Database);

                    var menedzerPolaczen = new MenedzerPolaczen();

                    DanePolaczenia danePolaczenia;
                    if (_settings.UseWindowsAuth)
                    {
                        danePolaczenia = DanePolaczenia.Jawne(
                            serwer: _settings.Server,
                            baza: _settings.Database,
                            autentykacjaWindowsDoSerwera: true);
                    }
                    else
                    {
                        danePolaczenia = DanePolaczenia.Jawne(
                            serwer: _settings.Server,
                            baza: _settings.Database,
                            uzytkownikSerwera: _settings.SqlLogin,
                            hasloUzytkownikaSerwera: _settings.SqlPassword);
                    }

                    var productId = GetProductId(_settings.Product);
                    _sfera = menedzerPolaczen.Polacz(danePolaczenia, productId);

                    if (!_sfera.ZalogujOperatora(_settings.NexoLogin, _settings.NexoPassword))
                    {
                        throw new InvalidOperationException($"Failed to login operator: {_settings.NexoLogin}");
                    }

                    _logger.LogInformation("Successfully connected to Sfera");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to Sfera");
                    throw;
                }
            }
        });
    }

    public Uchwyt GetSfera()
    {
        if (_sfera == null)
        {
            throw new InvalidOperationException("Sfera is not initialized. Call InitializeAsync first.");
        }
        return _sfera;
    }

    /// <summary>
    /// Gets a manager by calling the corresponding method on Sfera (e.g., "Asortymenty" calls sfera.Asortymenty())
    /// </summary>
    public dynamic? GetManager(string managerMethodName)
    {
        if (_sfera == null)
        {
            throw new InvalidOperationException("Sfera is not initialized. Call InitializeAsync first.");
        }

        try
        {
            // Use dynamic invocation - this handles extension methods and optional parameters
            dynamic sfera = _sfera;
            return managerMethodName switch
            {
                "Asortymenty" => sfera.Asortymenty(),
                "SzablonyAsortymentu" => sfera.SzablonyAsortymentu(),
                "Podmioty" => sfera.Podmioty(),
                "Dokumenty" => sfera.Dokumenty(),
                "DokumentySprzedazy" => sfera.DokumentySprzedazy(),
                "DokumentyZakupu" => sfera.DokumentyZakupu(),
                "DokumentyHandlowe" => sfera.DokumentyHandlowe(),
                "DokumentyElektroniczne" => sfera.DokumentyElektroniczne(),
                "KorektyDokumentowSprzedazy" => sfera.KorektyDokumentowSprzedazy(),
                "KorektyDokumentowZakupu" => sfera.KorektyDokumentowZakupu(),
                "Magazyny" => sfera.Magazyny(),
                "WydaniaZewnetrzne" => sfera.WydaniaZewnetrzne(),
                "PrzyjeciaZewnetrzne" => sfera.PrzyjeciaZewnetrzne(),
                "WydaniaMiedzymagazynowe" => sfera.WydaniaMiedzymagazynowe(),
                "RozchodyWewnetrzne" => sfera.RozchodyWewnetrzne(),
                "PrzychodWewnetrzny" => sfera.PrzychodWewnetrzny(),
                "ZamowieniaOdKlientow" => sfera.ZamowieniaOdKlientow(),
                "ZamowieniaDoDostawcow" => sfera.ZamowieniaDoDostawcow(),
                "Oferty" => sfera.Oferty(),
                "OfertyDlaKlientow" => sfera.OfertyDlaKlientow(),
                "Pracownicy" => sfera.Pracownicy(),
                "Slowniki" => sfera.Slowniki(),
                "Rabaty" => sfera.Rabaty(),
                _ => throw new ArgumentException($"Unknown manager: {managerMethodName}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting manager {MethodName}", managerMethodName);
            return null;
        }
    }

    /// <summary>
    /// Gets a typed manager using reflection-based PodajObiektTypu&lt;T&gt;() call
    /// Use this for interfaces/services, not for standard managers
    /// </summary>
    public dynamic? GetManagerByType(string assemblyName, string typeName)
    {
        if (_sfera == null)
        {
            throw new InvalidOperationException("Sfera is not initialized. Call InitializeAsync first.");
        }

        // Find the type from loaded assemblies
        Type? managerType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (asm.FullName != null && asm.FullName.Contains(assemblyName))
                {
                    managerType = asm.GetType(typeName);
                    if (managerType != null) break;
                }
            }
            catch { }
        }

        if (managerType == null)
        {
            _logger.LogWarning("Manager type {TypeName} not found in assembly {AssemblyName}", typeName, assemblyName);
            return null;
        }

        // Find the generic PodajObiektTypu method
        Type sferaType = _sfera.GetType();
        System.Reflection.MethodInfo? genericMethod = null;
        foreach (var m in sferaType.GetMethods())
        {
            if (m.Name == "PodajObiektTypu" && m.IsGenericMethod && m.GetParameters().Length == 0)
            {
                genericMethod = m;
                break;
            }
        }

        if (genericMethod == null)
        {
            _logger.LogWarning("PodajObiektTypu generic method not found on Sfera");
            return null;
        }

        // Create concrete method and invoke
        var concreteMethod = genericMethod.MakeGenericMethod(managerType);
        return concreteMethod.Invoke(_sfera, null);
    }

    private static ProductId GetProductId(string product)
    {
        return product.ToLower() switch
        {
            "subiekt" => ProductId.Subiekt,
            "rachmistrz" => ProductId.Rachmistrz,
            "rewizor" => ProductId.Rewizor,
            "gratyfikant" => ProductId.Gratyfikant,
            _ => ProductId.Subiekt
        };
    }

    public void Dispose()
    {
        _sfera?.Dispose();
        _sfera = null;
    }
}
