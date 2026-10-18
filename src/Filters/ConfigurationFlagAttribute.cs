using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NexoSferaApi.Filters;

/// <summary>
/// Hides an endpoint until its configuration flag is explicitly enabled.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ConfigurationFlagAttribute(string configurationKey) : Attribute, IResourceFilter
{
    public string ConfigurationKey { get; } = configurationKey;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue<bool>(configurationKey))
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
