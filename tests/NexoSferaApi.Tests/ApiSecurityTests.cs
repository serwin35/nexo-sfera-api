using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using NexoSferaApi.Filters;
using NexoSferaApi.Middleware;
using Xunit;

namespace NexoSferaApi.Tests;

public sealed class ApiSecurityTests(NexoApiFactory factory) : IClassFixture<NexoApiFactory>
{
    private readonly HttpClient _anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    private readonly HttpClient _authenticatedClient = CreateAuthenticatedClient(factory);

    [Fact]
    public async Task OnlyHealthStatusIsAnonymousAndReturnsNoOperationalDetails()
    {
        var response = await _anonymousClient.GetAsync("/api/health");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("Degraded", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, document.RootElement.EnumerateObject().Count());

        foreach (var path in new[]
        {
            "/api/health/sfera",
            "/api/health/sdk-sync",
            "/api/health/sdk-version-check",
            "/api/settings/info",
            "/api/diagnostics/services",
            "/api/customers/debug/sfera-info",
            "/api/documents/debug/properties/1",
            "/api/products/debug/properties/1"
        })
        {
            var protectedResponse = await _anonymousClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        }

        var reconnect = await _anonymousClient.PostAsync("/api/health/reconnect", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, reconnect.StatusCode);

        var mcp = await _anonymousClient.PostAsync("/mcp", content: new StringContent("{}"));
        Assert.Equal(HttpStatusCode.Unauthorized, mcp.StatusCode);

        var queryStringKey = await _anonymousClient.GetAsync("/api/settings/info?api_key=test-api-key-for-contract-tests");
        Assert.Equal(HttpStatusCode.Unauthorized, queryStringKey.StatusCode);
    }

    [Fact]
    public async Task UnauthorizedReconnectRequestsDoNotConsumeTheRateLimit()
    {
        foreach (var path in new[] { "/api/health/reconnect", "/api/settings/connection/reconnect" })
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var response = await _anonymousClient.PostAsync(path, content: null);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task DisabledFlagsHideDiagnosticsSettingsWritesAndSwaggerInTestingEnvironment()
    {
        var diagnostics = await _authenticatedClient.GetAsync("/api/diagnostics/services");
        var diagnosticGetWithSideEffects = await _authenticatedClient.GetAsync("/api/diagnostics/test-pz-creation-methods");
        var settingsWrite = await _authenticatedClient.PutAsync("/api/settings/connection", content: null);
        var swaggerJson = await _authenticatedClient.GetAsync("/swagger/v1/swagger.json");
        var root = await _authenticatedClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, diagnostics.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, diagnosticGetWithSideEffects.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, settingsWrite.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, swaggerJson.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, root.StatusCode);
    }

    [Fact]
    public async Task DisabledFlagsAlsoHideDiagnosticRoutesInDevelopmentEnvironment()
    {
        using var developmentFactory = new NexoApiFactory(environmentName: "Development");
        var authenticatedClient = CreateAuthenticatedClient(developmentFactory);

        Assert.Equal(HttpStatusCode.NotFound, (await authenticatedClient.GetAsync("/api/customers/debug/sfera-info")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await authenticatedClient.GetAsync("/api/diagnostics/services")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await authenticatedClient.PutAsync("/api/settings/connection", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await authenticatedClient.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await authenticatedClient.GetAsync("/swagger")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await authenticatedClient.GetAsync("/swagger/index.html")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await authenticatedClient.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    [Fact]
    public async Task EnabledSwaggerAndDiagnosticsStillRequireAnApiKey()
    {
        using var enabledFactory = new NexoApiFactory(diagnosticsEnabled: true, swaggerEnabled: true);
        var anonymousClient = enabledFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var authenticatedClient = CreateAuthenticatedClient(enabledFactory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync("/api/diagnostics/services")).StatusCode);
        foreach (var path in new[] { "/", "/swagger", "/swagger/index.html", "/swagger/v1/swagger.json" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync(path)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await authenticatedClient.GetAsync("/swagger")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await authenticatedClient.GetAsync("/swagger/index.html")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await authenticatedClient.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await authenticatedClient.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, (await authenticatedClient.GetAsync("/api/diagnostics/services")).StatusCode);
    }

    [Fact]
    public async Task EnabledWriteFlagsAllowOnlyTheirAuthenticatedRouteCategories()
    {
        using var diagnosticsFactory = new NexoApiFactory(diagnosticsEnabled: true);
        var diagnosticsClient = CreateAuthenticatedClient(diagnosticsFactory);
        Assert.NotEqual(HttpStatusCode.NotFound, (await diagnosticsClient.GetAsync("/api/diagnostics/services")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await diagnosticsClient.GetAsync("/api/diagnostics/test-pz-creation-methods")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await diagnosticsClient.PutAsync("/api/settings/connection", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await diagnosticsClient.GetAsync("/swagger/v1/swagger.json")).StatusCode);

        using var diagnosticWritesFactory = new NexoApiFactory(diagnosticsEnabled: true, diagnosticWritesEnabled: true);
        var diagnosticWritesClient = CreateAuthenticatedClient(diagnosticWritesFactory);
        Assert.NotEqual(HttpStatusCode.NotFound, (await diagnosticWritesClient.GetAsync("/api/diagnostics/test-pz-creation-methods")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await diagnosticWritesClient.PutAsync("/api/settings/connection", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await diagnosticWritesClient.GetAsync("/swagger/v1/swagger.json")).StatusCode);

        using var connectionWritesFactory = new NexoApiFactory(connectionWritesEnabled: true);
        var connectionWritesClient = CreateAuthenticatedClient(connectionWritesFactory);
        Assert.NotEqual(HttpStatusCode.NotFound, (await connectionWritesClient.PutAsync("/api/settings/connection", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await connectionWritesClient.GetAsync("/api/diagnostics/services")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await connectionWritesClient.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }

    [Fact]
    public async Task DiagnosticUnhandledExceptionsDoNotExposeTechnicalDetails()
    {
        var middleware = new ErrorHandlingMiddleware(
            _ => throw new Exception("private SDK connection details"),
            factory.Services.GetRequiredService<ILogger<ErrorHandlingMiddleware>>(),
            factory.Services.GetRequiredService<IWebHostEnvironment>());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/diagnostics/services";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var responseBody = await reader.ReadToEndAsync();
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.DoesNotContain("private SDK connection details", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", responseBody, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyHealthEndpointHasAllowAnonymousMetadata()
    {
        var endpoints = GetControllerEndpoints();
        var anonymousPaths = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(GetNormalizedPath)
            .ToArray();

        Assert.Equal(new[] { "/api/health" }, anonymousPaths);

        foreach (var endpoint in endpoints.Where(candidate => candidate.Metadata.GetMetadata<IAllowAnonymous>() is null))
        {
            Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
        }
    }

    [Fact]
    public async Task EveryProtectedControllerRouteRejectsRequestsWithoutAnApiKey()
    {
        foreach (var endpoint in GetControllerEndpoints().Where(candidate => candidate.Metadata.GetMetadata<IAllowAnonymous>() is null))
        {
            var httpMethods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
            Assert.NotNull(httpMethods);

            foreach (var httpMethod in httpMethods)
            {
                using var request = CreateRequest(httpMethod, GetSamplePath(endpoint));
                using var response = await _anonymousClient.SendAsync(request);

                Assert.True(
                    response.StatusCode == HttpStatusCode.Unauthorized,
                    $"Expected 401 for {httpMethod} {request.RequestUri}, received {(int)response.StatusCode}.");
            }
        }
    }

    [Fact]
    public void EveryDiagnosticAndDebugRouteHasTheDiagnosticsFlagFilter()
    {
        var diagnostics = GetControllerEndpoints()
            .Where(endpoint =>
            {
                var path = GetNormalizedPath(endpoint);
                return path.StartsWith("/api/diagnostics", StringComparison.Ordinal)
                    || path.Contains("/debug/", StringComparison.Ordinal);
            });

        Assert.NotEmpty(diagnostics);
        foreach (var endpoint in diagnostics)
        {
            var action = Assert.IsType<ControllerActionDescriptor>(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
            var attributes = action.ControllerTypeInfo.GetCustomAttributes(true)
                .Concat(action.MethodInfo.GetCustomAttributes(true));

            Assert.Contains(attributes, attribute => attribute is DevelopmentOnlyAttribute);
        }
    }

    [Fact]
    public void MutatingDiagnosticRoutesRequireTheSeparateWriteFlag()
    {
        var mutatingPaths = new HashSet<string>(StringComparer.Ordinal)
        {
            "/api/diagnostics/services/try/{typename}",
            "/api/diagnostics/test-pz-creation",
            "/api/diagnostics/disable-pz-financial-aspect",
            "/api/diagnostics/test-pz-creation-with-dates",
            "/api/diagnostics/test-pz-creation-methods"
        };

        var endpoints = GetControllerEndpoints()
            .Where(endpoint => mutatingPaths.Contains(GetNormalizedPath(endpoint)));
        var foundPaths = endpoints.Select(GetNormalizedPath).ToHashSet(StringComparer.Ordinal);

        Assert.True(mutatingPaths.SetEquals(foundPaths));
        foreach (var endpoint in endpoints)
        {
            var action = Assert.IsType<ControllerActionDescriptor>(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
            var attributes = action.ControllerTypeInfo.GetCustomAttributes(true)
                .Concat(action.MethodInfo.GetCustomAttributes(true));

            Assert.Contains(attributes, attribute =>
                attribute is ConfigurationFlagAttribute flag && flag.ConfigurationKey == "Diagnostics:WriteEnabled");
        }
    }

    [Fact]
    public void ConfigurationFilesKeepSecurityFlagsDisabled()
    {
        var sourceRoot = new DirectoryInfo(factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath);
        foreach (var file in Directory.EnumerateFiles(sourceRoot.FullName, "appsettings*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            AssertFlagDisabled(document.RootElement, "Diagnostics", "Enabled");
            AssertFlagDisabled(document.RootElement, "Diagnostics", "WriteEnabled");
            AssertFlagDisabled(document.RootElement, "Settings", "ConnectionWriteEnabled");
            AssertFlagDisabled(document.RootElement, "Swagger", "Enabled");

            if (document.RootElement.TryGetProperty("ApiKeys", out var apiKeys)
                && apiKeys.TryGetProperty("Keys", out var keys))
            {
                var sfera = document.RootElement.GetProperty("Sfera");
                Assert.Equal(string.Empty, sfera.GetProperty("NexoLogin").GetString());
                Assert.Equal(string.Empty, sfera.GetProperty("NexoPassword").GetString());

                foreach (var key in keys.EnumerateArray())
                {
                    Assert.False(key.GetProperty("IsActive").GetBoolean());
                    Assert.Equal(string.Empty, key.GetProperty("Key").GetString());
                    Assert.Equal(string.Empty, key.GetProperty("NexoLogin").GetString());
                    Assert.Equal(string.Empty, key.GetProperty("NexoPassword").GetString());
                }
            }
        }

        var publishDirectory = Environment.GetEnvironmentVariable("NEXO_PUBLISH_DIR");
        Assert.False(string.IsNullOrWhiteSpace(publishDirectory), "CI must set NEXO_PUBLISH_DIR to the publish output path.");
        var publishedSettings = Directory.EnumerateFiles(Path.GetFullPath(publishDirectory!), "appsettings*.json").ToArray();
        Assert.NotEmpty(publishedSettings);

        foreach (var file in publishedSettings)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            AssertFlagDisabled(document.RootElement, "Diagnostics", "Enabled");
            AssertFlagDisabled(document.RootElement, "Diagnostics", "WriteEnabled");
            AssertFlagDisabled(document.RootElement, "Settings", "ConnectionWriteEnabled");
            AssertFlagDisabled(document.RootElement, "Swagger", "Enabled");
        }
    }

    [Fact]
    public void ConnectionSettingsWriteEndpointRequiresItsDedicatedFlag()
    {
        var endpoint = Assert.Single(GetControllerEndpoints().Where(candidate =>
            GetNormalizedPath(candidate) == "/api/settings/connection"
            && candidate.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.IHttpMethodMetadata>()?.HttpMethods.Contains("PUT") == true));
        var action = Assert.IsType<ControllerActionDescriptor>(endpoint.Metadata.GetMetadata<ControllerActionDescriptor>());
        var attributes = action.ControllerTypeInfo.GetCustomAttributes(true)
            .Concat(action.MethodInfo.GetCustomAttributes(true));

        Assert.Contains(attributes, attribute =>
            attribute is ConfigurationFlagAttribute flag && flag.ConfigurationKey == "Settings:ConnectionWriteEnabled");
    }

    private static HttpClient CreateAuthenticatedClient(NexoApiFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", NexoApiFactory.ApiKey);
        return client;
    }

    private IEnumerable<RouteEndpoint> GetControllerEndpoints()
    {
        return factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null);
    }

    private static string GetNormalizedPath(RouteEndpoint endpoint)
    {
        return "/" + (endpoint.RoutePattern.RawText ?? string.Empty).Trim('/').ToLowerInvariant();
    }

    private static string GetSamplePath(RouteEndpoint endpoint)
    {
        var template = endpoint.RoutePattern.RawText ?? string.Empty;
        var path = "/" + template.Trim('/');
        return Regex.Replace(path, "\\{[^}]+\\}", "1");
    }

    private static HttpRequestMessage CreateRequest(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (HttpMethod.Post.Method.Equals(method, StringComparison.OrdinalIgnoreCase)
            || HttpMethod.Put.Method.Equals(method, StringComparison.OrdinalIgnoreCase)
            || HttpMethod.Patch.Method.Equals(method, StringComparison.OrdinalIgnoreCase))
        {
            request.Content = new StringContent("{}");
        }

        return request;
    }

    private static void AssertFlagDisabled(JsonElement root, string section, string key)
    {
        if (root.TryGetProperty(section, out var sectionElement)
            && sectionElement.TryGetProperty(key, out var flagElement))
        {
            Assert.Equal(JsonValueKind.False, flagElement.ValueKind);
        }
    }
}
