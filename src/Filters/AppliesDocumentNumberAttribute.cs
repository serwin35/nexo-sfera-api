namespace NexoSferaApi.Filters;

/// <summary>
/// Marks a write action that applies the <c>documentNumber</c> body field (a number stated outright, see
/// <c>ExplicitDocumentNumber</c>). Every other action refuses the field with 400 through
/// <see cref="DocumentNumberFieldFilter"/>, so a client never has it silently ignored.
/// </summary>
/// <remarks>
/// Adding the field to another request means: the property on the request model, the code that applies it, this
/// attribute on the action, and the endpoint in <c>ExplicitDocumentNumber.SupportedEndpoints</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AppliesDocumentNumberAttribute : Attribute
{
}
