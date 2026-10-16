using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NexoSferaApi.Services;

namespace NexoSferaApi.Tests;

public sealed class NexoApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKey = "test-api-key-for-contract-tests";

    static NexoApiFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("Sfera__AutoSyncSdk", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var contentRoot = FindSourceRoot();
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(contentRoot);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKeys:Keys:0:Key"] = ApiKey,
                ["ApiKeys:Keys:0:Name"] = "Contract test",
                ["ApiKeys:Keys:0:IsActive"] = "true",
                ["Diagnostics:Enabled"] = "false",
                ["Diagnostics:WriteEnabled"] = "false",
                ["Settings:ConnectionWriteEnabled"] = "false",
                ["Swagger:Enabled"] = "false"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISferaService>();
            services.AddSingleton(_ => DispatchProxy.Create<ISferaService, SferaServiceProxy>());
        });
    }

    private static string FindSourceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "NexoSferaApi.csproj");
            if (File.Exists(candidate))
            {
                return Path.Combine(current.FullName, "src");
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the NexoSferaApi source directory.");
    }
}

public class SferaServiceProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var returnType = targetMethod?.ReturnType ?? typeof(void);
        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return CreateCompletedTaskMethod.MakeGenericMethod(returnType.GenericTypeArguments[0]).Invoke(null, null);
        }

        return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
    }

    private static readonly MethodInfo CreateCompletedTaskMethod = typeof(SferaServiceProxy)
        .GetMethod(nameof(CreateCompletedTask), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Task<T?> CreateCompletedTask<T>()
    {
        return Task.FromResult(default(T));
    }
}
