namespace NexoSferaApi.Filters;

/// <summary>
/// Marks a write action that applies the <c>invoiceForm</c> body field (<c>InvoiceForm</c>). Every other action refuses
/// the field with 400 through <see cref="InvoiceFormFieldFilter"/>, so a client never has it silently ignored.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AppliesInvoiceFormAttribute : Attribute
{
}
