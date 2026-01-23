using InsERT.Moria.Sfera;
using InsERT.Mox.Product;
// Extension methods for Sfera managers
using InsERT.Moria.Asortymenty;
using InsERT.Moria.Klienci;
using InsERT.Moria.Dokumenty;
using InsERT.Moria.Logistyka;
using InsERT.Moria.Slowniki;
using InsERT.Moria.Finanse;
using InsERT.Moria.CennikiICeny;
using InsERT.Moria.Promocje;
using InsERT.Moria.Inwentaryzacja;
using InsERT.Moria.Deklaracje;
using InsERT.Moria.Intrastat;
using InsERT.Moria.KontrolaSkarbowa;
using InsERT.Moria.Raporty;
using InsERT.Moria.Wydruki;
using InsERT.Moria.HandelElektroniczny;
using InsERT.Moria.Naklejki;
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
    /// Gets a manager by calling the corresponding extension method on Sfera
    /// Extension methods require static type resolution, cannot use dynamic
    /// </summary>
    public dynamic? GetManager(string managerMethodName)
    {
        if (_sfera == null)
        {
            throw new InvalidOperationException("Sfera is not initialized. Call InitializeAsync first.");
        }

        try
        {
            // Call extension methods on static Uchwyt type (not dynamic!)
            // Extension methods are resolved at compile time
            // Extension methods are in InsERT.Moria.* namespaces
            // They extend Uchwyt and must be called on static type (not dynamic!)
            return managerMethodName switch
            {
                // InsERT.Moria.Asortymenty
                "Asortymenty" => _sfera.Asortymenty(),
                "SzablonyAsortymentu" => _sfera.SzablonyAsortymentu(),
                "GrupyAsortymentu" => _sfera.GrupyAsortymentu(),
                "CechyAsortymentu" => _sfera.CechyAsortymentu(),
                // InsERT.Moria.Klienci
                "Podmioty" => _sfera.Podmioty(),
                // GrupyKontrahentow does not have an extension method - access via Podmioty or alternative
                "GrupyKontrahentow" => throw new NotSupportedException("GrupyKontrahentow extension not available. Use Podmioty manager instead."),
                // InsERT.Moria.Dokumenty
                "Dokumenty" => _sfera.Dokumenty(),
                "DokumentySprzedazy" => _sfera.DokumentySprzedazy(),
                "DokumentyZakupu" => _sfera.DokumentyZakupu(),
                "DokumentyElektroniczne" => _sfera.DokumentyElektroniczne(),
                "KorektyDokumentowSprzedazy" => _sfera.KorektyDokumentowSprzedazy(),
                "KorektyDokumentowZakupu" => _sfera.KorektyDokumentowZakupu(),
                // InsERT.Moria.Logistyka
                "Magazyny" => _sfera.Magazyny(),
                "WydaniaZewnetrzne" => _sfera.WydaniaZewnetrzne(),
                "PrzyjeciaZewnetrzne" => _sfera.PrzyjeciaZewnetrzne(),
                "WydaniaMiedzymagazynowe" => _sfera.WydaniaMiedzymagazynowe(),
                "RozchodyWewnetrzne" => _sfera.RozchodyWewnetrzne(),
                "ZamowieniaOdKlientow" => _sfera.ZamowieniaOdKlientow(),
                "ZamowieniaDoDostawcow" => _sfera.ZamowieniaDoDostawcow(),
                "Oferty" => _sfera.Oferty(),
                // InsERT.Moria.Slowniki
                "StawkiVat" => _sfera.StawkiVat(),
                "JednostkiMiar" => _sfera.JednostkiMiar(),
                "Waluty" => _sfera.Waluty(),
                // KursyWalut does not have an extension method - access via Waluty.Kursy or alternative
                "KursyWalut" => throw new NotSupportedException("KursyWalut extension not available. Access exchange rates via Waluty manager."),
                "FormyPlatnosci" => _sfera.FormyPlatnosci(),
                // InsERT.Moria.Cenniki
                "Cenniki" => _sfera.Cenniki(),
                "PoziomyCen" => _sfera.PoziomyCen(),
                // InsERT.Moria.Rabaty
                "Rabaty" => _sfera.Rabaty(),
                // InsERT.Moria.Finanse
                "OperacjeKasowe" => _sfera.OperacjeKasowe(),
                "OperacjeBankowe" => _sfera.OperacjeBankowe(),
                "Rozrachunki" => _sfera.Rozrachunki(),
                "StanowiskaKasowe" => _sfera.StanowiskaKasowe(),
                "RachunkiBankowe" => _sfera.RachunkiBankowe(),
                "RaportyKasowe" => _sfera.RaportyKasowe(),
                "WyciagiBankowe" => _sfera.WyciagiBankowe(),
                "RodzajeOperacjiKasowych" => _sfera.RodzajeOperacjiKasowych(),
                "RodzajeOperacjiBankowych" => _sfera.RodzajeOperacjiBankowych(),
                // InsERT.Moria.Klienci - Countries
                "Panstwa" => _sfera.Panstwa(),
                // InsERT.Moria.Inwentaryzacja - Stocktaking
                "SpisyInwentaryzacyjne" => _sfera.SpisyInwentaryzacyjne(),
                // InsERT.Moria.Deklaracje - Tax declarations
                "Deklaracje" => _sfera.Deklaracje(),
                // InsERT.Moria.Intrastat - Intrastat declarations
                "DeklaracjeIntrastat" => _sfera.DeklaracjeIntrastat(),
                // InsERT.Moria.KontrolaSkarbowa - JPK (Jednolity Plik Kontrolny)
                "JednolitePlikiKontrolne" => _sfera.JednolitePlikiKontrolne(),
                "PaczkiJednolitychPlikowKontrolnych" => _sfera.PaczkiJednolitychPlikowKontrolnych(),
                "FabrykaDefinicjiJPK" => _sfera.FabrykaDefinicjiJPK(),
                "WersjeDefinicjiJPK" => _sfera.WersjeDefinicjiJPK(),
                "KonfiguracjeGeneratora" => _sfera.KonfiguracjeGeneratora(),
                // InsERT.Moria.Raporty - Reports
                "Raporty" => _sfera.Raporty(),
                // InsERT.Moria.Wydruki - Print templates
                "NaglowkiWydruku" => _sfera.NaglowkiWydruku(),
                "StopkiWydruku" => _sfera.StopkiWydruku(),
                "ParametryWydruku" => _sfera.ParametryWydruku(),
                "LogaWydruku" => _sfera.LogaWydruku(),
                // InsERT.Moria.HandelElektroniczny - E-commerce
                "KontaIntegracji" => _sfera.KontaIntegracji(),
                "OfertyInternetowe" => _sfera.OfertyInternetowe(),
                "ListyWysylkowe" => _sfera.ListyWysylkowe(),
                "PaczkiWysylkowe" => _sfera.PaczkiWysylkowe(),
                "GabarytyPaczki" => _sfera.GabarytyPaczki(),
                "GrupyOfert" => _sfera.GrupyOfert(),
                // InsERT.Moria.Naklejki - Labels
                "SzablonyNaklejek" => _sfera.SzablonyNaklejek(),
                // InsERT.Moria.Dokumenty.Logistyka - Logistics document configurations
                "Konfiguracje" => _sfera.Konfiguracje(),
                // Methods that don't have extension methods - need different approach
                "DokumentyHandlowe" => throw new NotSupportedException("DokumentyHandlowe requires direct Sfera access"),
                "OfertyDlaKlientow" => throw new NotSupportedException("OfertyDlaKlientow requires direct Sfera access"),
                _ => throw new ArgumentException($"Unknown manager: {managerMethodName}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting manager {MethodName}", managerMethodName);
            throw; // Re-throw so we can see the real error
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
