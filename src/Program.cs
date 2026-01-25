using System.Reflection;
using Microsoft.OpenApi.Models;
using NexoSferaApi.Configuration;
using NexoSferaApi.Services;
using NexoSferaApi.Middleware;
using NexoSferaApi.Authentication;
using NexoSferaApi.Helpers;

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
        Version = "v1",
        Description = @"REST API for InsERT Nexo Sfera ERP integration.

## Features
- **Customers** - CRUD operations for contractors (kontrahenci)
- **Products** - Product/assortment management (asortyment)
- **Documents** - Commercial documents (invoices, orders, receipts)
- **Warehouse** - Warehouse documents (WZ, PZ, RW, PW, MM)
- **Payments** - Cash and bank operations (KP, KW, BP, BW)
- **Inventory** - Stock levels, batches, reservations
- **Assembly (ZM)** - Assembly and disassembly operations

## Authentication
This API uses Bearer token authentication. Include your API key in the `Authorization` header:
```
Authorization: Bearer your-api-key-here
```
",
        Contact = new OpenApiContact
        {
            Name = "Nexo Sfera API Support",
            Email = "support@example.com"
        },
        License = new OpenApiLicense
        {
            Name = "Proprietary"
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

// Initialize Sfera connection on startup
var sferaService = app.Services.GetRequiredService<ISferaService>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Initializing Sfera connection...");
    await sferaService.InitializeAsync();
    logger.LogInformation("Sfera connection initialized successfully");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to initialize Sfera connection. API will start but Sfera operations will fail.");
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
