using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NexoSferaApi.Helpers;
using NexoSferaApi.Models.Responses;

namespace NexoSferaApi.Filters;

/// <summary>
/// Refuses a JSON body with a top-level <c>invoiceForm</c> on every action without
/// <see cref="AppliesInvoiceFormAttribute"/>: the request models of several endpoints share the property, and model
/// binding would bind it where nothing applies it. Same rules as <see cref="DocumentNumberFieldFilter"/>, except that an
/// explicit <c>"invoiceForm": null</c> passes: it asks for no form.
/// </summary>
public sealed class InvoiceFormFieldFilter : IAsyncResourceFilter, IOrderedFilter
{
    public int Order => 1001;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (DocumentNumberFieldFilter.IsWrite(request.Method) && DocumentNumberFieldFilter.IsJson(request.ContentType)
            && !context.ActionDescriptor.EndpointMetadata.OfType<AppliesInvoiceFormAttribute>().Any())
        {
            request.EnableBuffering();
            string body;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
                       bufferSize: 4096, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync(context.HttpContext.RequestAborted);
            }

            request.Body.Position = 0;
            // An explicit "invoiceForm": null asks for nothing; only a value is refused here.
            if (DocumentNumberFieldFilter.HasTopLevelValue(body, InvoiceForm.Field))
            {
                context.Result = new BadRequestObjectResult(ApiResponse<object>.Error(
                    InvoiceForm.Unsupported, new List<string> { InvoiceForm.Unsupported }));
                return;
            }
        }

        await next();
    }
}
