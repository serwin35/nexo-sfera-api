using InsERT.Mox.ObiektyBiznesowe;
using InsERT.Mox.Validation;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed read of the validation errors of an SDK business object after a failed <c>Zapisz()</c> or <c>Usun()</c>:
/// <c>IObiektBiznesowy.Bledy</c>, with the errors of each entity and of its members.
/// </summary>
/// <remarks>
/// The late-bound readers in the controllers look up <c>Errors</c> / <c>MemberErrors</c> by reflection, but the entities
/// implement <c>ITypedDataErrorInfo</c> explicitly, so those lookups find nothing; and <c>dynamic</c> never binds the
/// <c>PodajBledy</c> extension method. Both come back empty. Best effort: a failure to read the errors yields an empty list.
/// </remarks>
internal static class SdkErrors
{
    public static List<string> Of(IObiektBiznesowy businessObject)
    {
        var messages = new List<string>();
        try
        {
            foreach (var entity in businessObject.Bledy ?? Enumerable.Empty<ITypedDataErrorInfo>())
            {
                if (entity == null) continue;

                foreach (var error in entity.Errors ?? Enumerable.Empty<IDataError>())
                {
                    Add(messages, error?.ToString());
                }

                foreach (var memberError in entity.MemberErrors ?? Enumerable.Empty<IGrouping<IDataError, string>>())
                {
                    var members = string.Join(", ", memberError.Where(m => !string.IsNullOrWhiteSpace(m)));
                    var text = memberError.Key?.ToString();
                    Add(messages, string.IsNullOrEmpty(members) ? text : $"{members}: {text}");
                }
            }
        }
        catch
        {
            // Error details are informative only; the caller reports the failed save anyway.
        }

        return messages;
    }

    private static void Add(List<string> messages, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var trimmed = text.Trim();
        if (!messages.Contains(trimmed)) messages.Add(trimmed);
    }
}
