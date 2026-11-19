using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NexoSferaApi.Helpers;
using NexoSferaApi.Models.Responses;

namespace NexoSferaApi.Filters;

/// <summary>
/// Refuses a JSON body with a top-level <c>documentNumber</c> on every action without
/// <see cref="AppliesDocumentNumberAttribute"/>: model binding would drop the unknown field, and the document would take
/// the next free number although the client asked for a specific one.
/// </summary>
/// <remarks>
/// Runs after the other resource filters (authorization, configuration flags), before model binding. Cheap: the body is
/// parsed only when its text contains <c>documentNumber</c>; nested properties and look-alikes such as
/// <c>relatedDocumentNumber</c> do not count.
/// </remarks>
public sealed class DocumentNumberFieldFilter : IAsyncResourceFilter, IOrderedFilter
{
    public int Order => 1000;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (IsWrite(request.Method) && IsJson(request.ContentType)
            && !context.ActionDescriptor.EndpointMetadata.OfType<AppliesDocumentNumberAttribute>().Any())
        {
            request.EnableBuffering();
            string body;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
                       bufferSize: 4096, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
            }

            request.Body.Position = 0;
            if (HasDocumentNumberField(body))
            {
                context.Result = new BadRequestObjectResult(ApiResponse<object>.Error(
                    ExplicitDocumentNumber.Unsupported, new List<string> { ExplicitDocumentNumber.Unsupported }));
                return;
            }
        }

        await next();
    }

    /// <summary>True when the JSON object has a top-level <c>documentNumber</c> (any letter case).</summary>
    public static bool HasDocumentNumberField(string? body) => HasTopLevelField(body, ExplicitDocumentNumber.Field);

    /// <summary>True when the JSON object has the top-level property <paramref name="field"/> (any letter case).</summary>
    public static bool HasTopLevelField(string? body, string field) => HasTopLevelField(body, field, nullCounts: true);

    /// <summary>
    /// True when the JSON object has the top-level property <paramref name="field"/> with a value other than
    /// <c>null</c>: an explicit null asks for nothing, so it is not refused.
    /// </summary>
    public static bool HasTopLevelValue(string? body, string field) => HasTopLevelField(body, field, nullCounts: false);

    private static bool HasTopLevelField(string? body, string field, bool nullCounts)
    {
        if (string.IsNullOrEmpty(body) || body.IndexOf(field, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.EnumerateObject()
                    .Any(property => property.Name.Equals(field, StringComparison.OrdinalIgnoreCase)
                        && (nullCounts || property.Value.ValueKind != JsonValueKind.Null));
        }
        catch (JsonException)
        {
            return false; // model binding reports the invalid body
        }
    }

    internal static bool IsWrite(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method);

    internal static bool IsJson(string? contentType) =>
        !string.IsNullOrEmpty(contentType)
        && (contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
            || contentType.Split(';')[0].Trim().EndsWith("+json", StringComparison.OrdinalIgnoreCase));
}
