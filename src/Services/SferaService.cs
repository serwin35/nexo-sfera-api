using InsERT.Moria.Sfera;
using InsERT.Mox.Product;
// Extension methods for Sfera managers
using InsERT.Moria.ModelOrganizacyjny;
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
using System.Collections.Concurrent;
using System.Windows.Forms;

namespace NexoSferaApi.Services;

/// <summary>
/// Sfera service that runs ALL SDK operations on a dedicated STA thread.
///
/// The InsERT Nexo SDK was designed for Windows desktop applications (Windows Forms, WPF)
/// which run on a Single-Threaded Apartment (STA) thread. Entity Framework 6 contexts
/// inside the SDK are NOT thread-safe and must be accessed from the same thread.
///
/// This service creates a persistent STA thread that handles:
/// 1. SDK initialization (connection, login, context setup)
/// 2. All subsequent SDK operations
///
/// This mimics how desktop apps like Artoit work - everything on one UI thread.
/// </summary>
public class SferaService : ISferaService, IDisposable
{
    private readonly SferaSettings _settings;
    private readonly ILogger<SferaService> _logger;
    private Uchwyt? _sfera;

    // Dedicated STA thread for ALL SDK operations - mimics desktop app behavior
    private Thread? _sdkThread;
    private BlockingCollection<Action>? _workQueue;
    private readonly CancellationTokenSource _cts = new();
    private bool _threadStarted;
    private readonly object _threadLock = new();

    public bool IsConnected => _sfera != null;

    public SferaService(IOptions<SferaSettings> settings, ILogger<SferaService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        StartSdkThread();
    }

    /// <summary>
    /// Starts the dedicated STA thread for SDK operations.
    /// This thread processes all SDK work items sequentially, just like a desktop app UI thread.
    /// CRITICAL: The thread must have WindowsFormsSynchronizationContext installed for EF6 to work!
    /// </summary>
    private void StartSdkThread()
    {
        lock (_threadLock)
        {
            if (_threadStarted) return;

            _workQueue = new BlockingCollection<Action>();

            _sdkThread = new Thread(() =>
            {
                _logger.LogInformation("SDK STA thread started (ThreadId: {ThreadId})", Environment.CurrentManagedThreadId);

                try
                {
                    // CRITICAL: Initialize Windows Forms context on this STA thread
                    // This is REQUIRED for Entity Framework 6 to work correctly!
                    // Without this, EF6 throws "Index was out of range" errors during document creation.
                    Application.SetHighDpiMode(HighDpiMode.SystemAware);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    // Create a hidden form to initialize the SynchronizationContext
                    // This mimics how desktop apps (like Subiekt) initialize their UI thread
                    using var hiddenForm = new Form { Visible = false };
                    hiddenForm.Show();
                    hiddenForm.Hide();

                    _logger.LogInformation("SDK STA thread: WindowsFormsSynchronizationContext installed: {Context}",
                        SynchronizationContext.Current?.GetType().Name ?? "null");

                    _logger.LogInformation("SDK STA thread: Entering work loop");
                    foreach (var workItem in _workQueue.GetConsumingEnumerable(_cts.Token))
                    {
                        _logger.LogInformation("SDK STA thread: Picked up work item");
                        try
                        {
                            workItem();
                            _logger.LogInformation("SDK STA thread: Work item finished");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error executing SDK work item");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("SDK STA thread shutting down");
                }

                _logger.LogInformation("SDK STA thread exited");
            });

            _sdkThread.SetApartmentState(ApartmentState.STA);
            _sdkThread.IsBackground = true;
            _sdkThread.Name = "Nexo SDK STA Thread";
            _sdkThread.Start();
            _threadStarted = true;
        }
    }

    public async Task InitializeAsync()
    {
        // Run initialization on the dedicated STA thread
        await ExecuteOnSdkThreadAsync(() =>
        {
            if (_sfera != null) return;

            try
            {
                _logger.LogInformation("Connecting to Sfera on STA thread (ThreadId: {ThreadId}): Server={Server}, Database={Database}",
                    Environment.CurrentManagedThreadId, _settings.Server, _settings.Database);

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

                // Set working context - CRITICAL for document creation
                // SDK examples show this is required before any document operations
                SetWorkingContext();

                _logger.LogInformation("Successfully connected to Sfera on STA thread");
                _logger.LogInformation("SDK STA thread: Initialization complete, thread ready for work items");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Sfera");
                throw;
            }
        });
    }

    /// <summary>
    /// Executes an action on the dedicated SDK STA thread and waits for completion.
    /// RunContinuationsAsynchronously prevents continuations from running on the SDK thread.
    /// </summary>
    private Task ExecuteOnSdkThreadAsync(Action action)
    {
        if (_workQueue == null)
            throw new InvalidOperationException("SDK thread not started");

        // CRITICAL: RunContinuationsAsynchronously ensures that await continuations
        // don't run on the SDK thread, which would block it from processing more items
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _workQueue.Add(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    /// <summary>
    /// Executes a function on the dedicated SDK STA thread and returns the result.
    /// RunContinuationsAsynchronously prevents continuations from running on the SDK thread.
    /// </summary>
    private Task<T> ExecuteOnSdkThreadAsync<T>(Func<T> func)
    {
        if (_workQueue == null)
            throw new InvalidOperationException("SDK thread not started");

        // CRITICAL: RunContinuationsAsynchronously ensures that await continuations
        // don't run on the SDK thread, which would block it from processing more items
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callerId = Environment.CurrentManagedThreadId;

        _logger.LogInformation("ExecuteOnSdkThread: Adding work item from thread {CallerId}, queue count: {Count}",
            callerId, _workQueue.Count);

        _workQueue.Add(() =>
        {
            _logger.LogInformation("ExecuteOnSdkThread: Executing work item on thread {ThreadId}",
                Environment.CurrentManagedThreadId);
            try
            {
                var result = func();
                _logger.LogInformation("ExecuteOnSdkThread: Work item completed successfully");
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExecuteOnSdkThread: Work item failed");
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
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
                "GrupyCechAsortymentu" => _sfera.GrupyCech(), // Alias for attribute groups
                "GrupyCech" => _sfera.GrupyCech(),
                // InsERT.Moria.Klienci
                "Podmioty" => _sfera.Podmioty(),
                // GrupyKontrahentow/GrupyPodmiotow - access via Podmioty manager
                "GrupyKontrahentow" => _sfera.Podmioty(), // Groups accessible via Podmioty
                "GrupyPodmiotow" => _sfera.Podmioty(), // Alias for contractor groups
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
                "PrzesunieciaMiedzymagazynowe" => _sfera.WydaniaMiedzymagazynowe(), // Alias
                "RozchodyWewnetrzne" => _sfera.RozchodyWewnetrzne(),
                "PrzychodyWewnetrzne" => _sfera.PrzychodyWewnetrzne(),
                "ZamowieniaOdKlientow" => _sfera.ZamowieniaOdKlientow(),
                "ZamowieniaDoDostawcow" => _sfera.ZamowieniaDoDostawcow(),
                "Oferty" => _sfera.Oferty(),
                // InsERT.Moria.Slowniki
                "StawkiVat" => _sfera.StawkiVat(),
                "JednostkiMiar" => _sfera.JednostkiMiar(),
                "Jednostki" => _sfera.JednostkiMiar(), // Alias
                "Waluty" => _sfera.Waluty(),
                // KursyWalut does not have an extension method - access via Waluty.Kursy or alternative
                "KursyWalut" => throw new NotSupportedException("KursyWalut extension not available. Access exchange rates via Waluty manager."),
                "FormyPlatnosci" => _sfera.FormyPlatnosci(),
                "SposobyPlatnosci" => _sfera.FormyPlatnosci(), // Alias
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

    /// <summary>
    /// Sets working context (warehouse, branch, cash register) - required before document operations
    /// SDK examples show this must be called after login and before any document creation
    /// </summary>
    private void SetWorkingContext()
    {
        if (_sfera == null) return;

        try
        {
            var kontekst = _sfera.Kontekst();

            // Set warehouse context
            if (!string.IsNullOrEmpty(_settings.DefaultWarehouse))
            {
                kontekst.UstawMagazynWedlugSymbolu(_settings.DefaultWarehouse);
                _logger.LogInformation("Set warehouse context: {Warehouse}", _settings.DefaultWarehouse);
            }

            // Set branch context
            if (!string.IsNullOrEmpty(_settings.DefaultBranch))
            {
                kontekst.UstawOddzialWedlugSymbolu(_settings.DefaultBranch);
                _logger.LogInformation("Set branch context: {Branch}", _settings.DefaultBranch);
            }

            // Set cash register context (optional)
            if (!string.IsNullOrEmpty(_settings.DefaultCashRegister))
            {
                kontekst.UstawStanowiskoKasoweWdlugSymbolu(_settings.DefaultCashRegister);
                _logger.LogInformation("Set cash register context: {CashRegister}", _settings.DefaultCashRegister);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set working context. Document operations may fail.");
        }
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

    /// <summary>
    /// Executes an SDK operation on the dedicated STA thread.
    /// All operations run sequentially on the same thread - just like a desktop app.
    /// No additional synchronization needed because the work queue serializes all operations.
    /// </summary>
    public Task<T> ExecuteWithLockAsync<T>(Func<T> operation)
    {
        return ExecuteOnSdkThreadAsync(operation);
    }

    /// <summary>
    /// Executes an async SDK operation on the dedicated STA thread.
    /// Note: The async operation itself runs on the SDK thread, blocking it until complete.
    /// For truly async operations, consider wrapping the result.
    /// </summary>
    public async Task<T> ExecuteWithLockAsync<T>(Func<Task<T>> operation)
    {
        // We need to run the async operation on the SDK thread, but we can't await inside
        // the work queue action. So we run it synchronously with .Result
        // This is acceptable because we're on a dedicated thread.
        return await ExecuteOnSdkThreadAsync(() => operation().GetAwaiter().GetResult());
    }

    /// <summary>
    /// Gets the database connection string built from SferaSettings.
    /// Used for diagnostic ADO.NET queries.
    /// </summary>
    public string? GetConnectionString()
    {
        if (!IsConnected) return null;

        try
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
            {
                DataSource = _settings.Server,
                InitialCatalog = _settings.Database
            };

            if (_settings.UseWindowsAuth)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID = _settings.SqlLogin;
                builder.Password = _settings.SqlPassword;
                builder.IntegratedSecurity = false;
            }

            builder.TrustServerCertificate = true;
            return builder.ConnectionString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building connection string");
            return null;
        }
    }

    public void Dispose()
    {
        // Signal the SDK thread to stop
        _cts.Cancel();
        _workQueue?.CompleteAdding();

        // Wait for the thread to finish (with timeout)
        if (_sdkThread?.IsAlive == true)
        {
            if (!_sdkThread.Join(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning("SDK thread did not exit gracefully within timeout");
            }
        }

        // Dispose Sfera on the SDK thread if possible, otherwise just dispose
        try
        {
            _sfera?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing Sfera");
        }

        _sfera = null;
        _workQueue?.Dispose();
        _cts.Dispose();
    }
}
