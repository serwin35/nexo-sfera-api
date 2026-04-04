using System.Reflection;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.AspNetCore;
using NexoSferaApi.Configuration;
using NexoSferaApi.Services;
using NexoSferaApi.Middleware;
using NexoSferaApi.Authentication;
using NexoSferaApi.Helpers;

// Synchronize SDK DLLs from nexo installation before loading any assemblies
try
{
    var tempConfig = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    var autoSync = tempConfig.GetValue("Sfera:AutoSyncSdk", true);
    if (autoSync)
    {
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
        var syncLogger = loggerFactory.CreateLogger("NexoSdkSync");

        var installPath = tempConfig["Sfera:NexoInstallPath"]
            ?? Environment.GetEnvironmentVariable("NEXO_INSTALL_PATH");
        var syncSource = tempConfig.GetValue("Sfera:SyncSdkSource", false);

        string? sourceLibDir = null;
        if (syncSource)
        {
            // Resolve lib/nexo-sdk/ relative to project root (two levels up from bin output)
            var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
            sourceLibDir = Path.Combine(projectRoot, "lib", "nexo-sdk");
        }

        NexoSdkSynchronizer.Synchronize(installPath, AppContext.BaseDirectory, syncLogger, syncSource, sourceLibDir);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[SDK Sync] Non-fatal error: {ex.Message}");
}

// Initialize Entity Framework 6 for .NET 8 compatibility
// Must be called before any EF6/Sfera operations
EF6Initializer.Initialize();

var builder = WebApplication.CreateBuilder(args);

// Add environment variables as configuration source with custom prefix mapping
builder.Configuration
    .AddEnvironmentVariables()
    .AddEnvironmentVariables("SFERA_"); // For SFERA_* variables

// Map environment variables to configuration (for backward compatibility)
MapEnvironmentToConfiguration(builder.Configuration);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddEndpointsApiExplorer();

// Configure enhanced Swagger with XML documentation and API Key auth
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Nexo Sfera REST API",
        Version = "v1.5.0",
        Description = @"# Nexo Sfera REST API by DMservice

REST API do integracji z systemem InsERT Nexo / Subiekt / Rachmistrz / Rewizor.

## 🚀 Funkcjonalności

### Kontrahenci (Customers)
- Pełny CRUD dla kontrahentów i grup kontrahentów
- Wyszukiwanie po NIP, ID, nazwie
- Zarządzanie adresami i danymi kontaktowymi

### Asortyment (Products)
- Zarządzanie produktami, usługami, kompletami
- Szablony produktów, grupy, cechy
- Kody EAN, jednostki miar, poziomy cen

### Dokumenty handlowe (Documents)
- Faktury sprzedaży (FS) i zakupu (FZ)
- Paragony (PA, PAi, PAf)
- Zamówienia od klientów (ZK) i do dostawców (ZD)
- Korekty dokumentów

### Dokumenty magazynowe (Warehouse)
- Wydania zewnętrzne (WZ) i przyjęcia (PZ)
- Rozchody wewnętrzne (RW) i przychody (PW)
- Przesunięcia międzymagazynowe (MM)
- Kompletacja i dekompletacja (ZM)

### Finanse (Payments)
- Operacje kasowe (KP, KW)
- Operacje bankowe (BP, BW)
- Rozrachunki i należności

### Magazyn (Inventory)
- Stany magazynowe i rezerwacje
- Partie i daty ważności
- Wycena FIFO/LIFO
- Inwentaryzacja

### Słowniki i konfiguracja
- Stawki VAT, jednostki miar, waluty
- Formy płatności, poziomy cen
- Grupy produktów i kontrahentów

## 🔐 Autentykacja

API używa tokenów Bearer (API Key). Dodaj nagłówek `Authorization` do każdego żądania:

```
Authorization: Bearer twoj-klucz-api
```

## 📞 Kontakt

**DMservice** - Integracje systemów ERP
📧 mateusz.serwinowski@dmservice.pl
",
        Contact = new OpenApiContact
        {
            Name = "DMservice - Mateusz Serwinowski",
            Email = "mateusz.serwinowski@dmservice.pl",
            Url = new Uri("https://dmservice.pl")
        },
        License = new OpenApiLicense
        {
            Name = "Licencja komercyjna DMservice"
        }
    });

    // Add Bearer token authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "API Key",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Enter your API key (without 'Bearer ' prefix - it will be added automatically)"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments for documentation
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Group endpoints by controller tag
    c.TagActionsBy(api =>
    {
        if (api.GroupName != null)
        {
            return new[] { api.GroupName };
        }

        var controllerName = api.ActionDescriptor.RouteValues["controller"];
        return new[] { controllerName ?? "Other" };
    });

    c.DocInclusionPredicate((name, api) => true);
});

// Configure Sfera connection settings
builder.Services.Configure<SferaSettings>(builder.Configuration.GetSection("Sfera"));

// Configure API Key settings
builder.Services.Configure<ApiKeySettings>(builder.Configuration.GetSection("ApiKeys"));

// Add authentication
builder.Services.AddAuthentication(ApiKeyAuthenticationDefaults.AuthenticationScheme)
    .AddApiKeyAuthentication();

builder.Services.AddAuthorization();

// Register Sfera service as singleton (connection is expensive)
builder.Services.AddSingleton<ISferaService, SferaService>();

// Register helpers
builder.Services.AddSingleton<StockValidationHelper>();
builder.Services.AddSingleton<ProductSymbolService>();

// Register MCP (Model Context Protocol) server for AI agent integration
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Add CORS if needed
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline

// Add error handling middleware first
app.UseErrorHandling();

// Enable Swagger in all environments (protected by auth in production)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nexo Sfera API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Nexo Sfera API Documentation";
    c.DefaultModelsExpandDepth(-1); // Hide schemas by default
    c.EnableDeepLinking();
    c.EnableFilter();
    c.ShowExtensions();
});

// Redirect root to Swagger
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.UseHttpsRedirection();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// MCP server endpoints at /mcp for AI agent integration
app.MapMcp("/mcp");

// Initialize Sfera connection on startup
using (var scope = app.Services.CreateScope())
{
    var sferaService = scope.ServiceProvider.GetRequiredService<ISferaService>();
    await sferaService.InitializeAsync();
}

app.Run();

// Helper function to map environment variables to configuration
static void MapEnvironmentToConfiguration(ConfigurationManager config)
{
    // Map SFERA_* environment variables to Sfera:* configuration
    var envMappings = new Dictionary<string, string>
    {
        { "SFERA_SERVER", "Sfera:Server" },
        { "SFERA_DATABASE", "Sfera:Database" },
        { "SFERA_USE_WINDOWS_AUTH", "Sfera:UseWindowsAuth" },
        { "SFERA_SQL_LOGIN", "Sfera:SqlLogin" },
        { "SFERA_SQL_PASSWORD", "Sfera:SqlPassword" },
        { "SFERA_NEXO_LOGIN", "Sfera:NexoLogin" },
        { "SFERA_NEXO_PASSWORD", "Sfera:NexoPassword" },
        { "SFERA_PRODUCT", "Sfera:Product" },
        { "SFERA_NEXO_INSTALL_PATH", "Sfera:NexoInstallPath" },
        { "SFERA_AUTO_SYNC_SDK", "Sfera:AutoSyncSdk" },
        { "API_KEY", "ApiKeys:Keys:0:Key" },
        { "API_PORT", "Kestrel:Endpoints:Http:Url" }
    };

    foreach (var mapping in envMappings)
    {
        var envValue = Environment.GetEnvironmentVariable(mapping.Key);
        if (!string.IsNullOrEmpty(envValue))
        {
            if (mapping.Key == "API_PORT")
            {
                config[mapping.Value] = $"http://0.0.0.0:{envValue}";
            }
            else
            {
                config[mapping.Value] = envValue;
            }
        }
    }
}
