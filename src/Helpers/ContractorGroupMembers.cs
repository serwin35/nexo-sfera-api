using InsERT.Moria.Klienci;
using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed contractor group membership. A contractor belongs to any number of groups through <c>Podmiot.Grupy</c>
/// (many-to-many with <c>Grupa</c>, the reverse side is <c>Grupa.Podmioty</c>).
/// </summary>
/// <remarks>
/// The late-bound code read and wrote <c>Podmiot.Grupa</c>, which does not exist: the member list and the member counts
/// were always empty, adding a member failed and removing one always answered "not in this group".
/// Must run on the SDK thread.
/// </remarks>
public static class ContractorGroupMembers
{
    public const string Added = "added";
    public const string Removed = "removed";
    public const string AlreadyMember = "alreadyMember";
    public const string NotMember = "notMember";
    public const string ContractorNotFound = "contractorNotFound";
    public const string SaveFailed = "saveFailed";

    /// <summary>Largest page of <c>GET /api/contractor-groups/{id}/contractors</c>.</summary>
    public const int MaxPageSize = 500;

    /// <summary>Result of <see cref="Add"/> or <see cref="Remove"/> for one contractor.</summary>
    public sealed class Change
    {
        public int ContractorId { get; init; }
        public string Status { get; set; } = string.Empty;
        public string? ContractorSymbol { get; set; }
        public string? ContractorName { get; set; }
        public List<string> Errors { get; } = new();
    }

    public static Grupa? FindGroup(Uchwyt sfera, int groupId) =>
        sfera.Grupy().Dane.Wszystkie().FirstOrDefault(g => g.Id == groupId);

    /// <summary>Direct members of the group as one SQL query; members of its subgroups are not included.</summary>
    public static IQueryable<Podmiot> Members(Uchwyt sfera, int groupId) =>
        sfera.Podmioty().Dane.Wszystkie().Where(p => p.Grupy.Any(g => g.Id == groupId));

    /// <summary>Number of direct members of every group that has any, in one SQL query.</summary>
    public static Dictionary<int, int> CountMembersByGroup(Uchwyt sfera) =>
        sfera.Grupy().Dane.Wszystkie()
            .Select(g => new { g.Id, Count = g.Podmioty.Count() })
            .Where(g => g.Count > 0)
            .ToDictionary(g => g.Id, g => g.Count);

    /// <summary>List item in the shape of <c>GET /api/customers</c>.</summary>
    public static CustomerListItemDto MapToListItem(Podmiot podmiot) => new()
    {
        Id = podmiot.Id,
        Name = podmiot.NazwaSkrocona ?? string.Empty,
        TaxId = podmiot.NIP,
        Phone = podmiot.Telefon,
        IsActive = podmiot.Aktywny,
        ContractorType = (ContractorType)podmiot.RodzajKontrahenta,
    };

    /// <summary>Adds the contractor to the group and saves the contractor card. A current member is left as it is.</summary>
    public static Change Add(Uchwyt sfera, Grupa group, int contractorId, Func<object, List<string>> saveErrors) =>
        Apply(sfera, group, contractorId, add: true, saveErrors);

    /// <summary>Removes the contractor from the group and saves the contractor card. Other groups stay as they are.</summary>
    public static Change Remove(Uchwyt sfera, Grupa group, int contractorId, Func<object, List<string>> saveErrors) =>
        Apply(sfera, group, contractorId, add: false, saveErrors);

    private static Change Apply(Uchwyt sfera, Grupa group, int contractorId, bool add, Func<object, List<string>> saveErrors)
    {
        var change = new Change { ContractorId = contractorId };
        var podmioty = sfera.Podmioty();
        var entity = podmioty.Dane.Wszystkie().FirstOrDefault(p => p.Id == contractorId);
        if (entity == null)
        {
            change.Status = ContractorNotFound;
            return change;
        }

        change.ContractorSymbol = SdkMember.Text(() => entity.Sygnatura == null ? null : entity.Sygnatura.PelnaSygnatura);
        change.ContractorName = entity.NazwaSkrocona;

        using var podmiot = podmioty.Znajdz(entity);
        if (podmiot == null)
        {
            change.Status = ContractorNotFound;
            return change;
        }

        var current = podmiot.Dane.Grupy.FirstOrDefault(g => g.Id == group.Id);
        if (add && current != null)
        {
            change.Status = AlreadyMember;
            return change;
        }

        if (!add && current == null)
        {
            change.Status = NotMember;
            return change;
        }

        if (add)
        {
            podmiot.Dane.Grupy.Add(group);
        }
        else
        {
            podmiot.Dane.Grupy.Remove(current!);
        }

        if (!podmiot.Zapisz())
        {
            // Not saved: disposing the business object discards the change.
            change.Status = SaveFailed;
            change.Errors.AddRange(saveErrors(podmiot));
            return change;
        }

        change.Status = add ? Added : Removed;
        return change;
    }
}
