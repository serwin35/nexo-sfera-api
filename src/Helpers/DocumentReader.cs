using InsERT.Moria.ModelDanych;
using InsERT.Moria.Sfera;
using NexoSferaApi.Models.Dto;

namespace NexoSferaApi.Helpers;

/// <summary>
/// Typed access to document state that the dynamic mappers read from members missing in SDK 61.1
/// (<c>Anulowany</c>, <c>DataUtworzenia</c>, <c>DataModyfikacji</c>).
/// </summary>
/// <remarks>
/// <para>
/// Cancelled = the document status is an invalidation status (<c>StatusDokumentu.Uniewazniony</c>). Creation and last
/// change time come from the entity header (<c>Dokument.Naglowek</c>: <c>NaglowekEncji.Utworzono</c> /
/// <c>Zmieniono</c>, <c>DateTimeOffset</c>). A payment does not change the invoice itself, it changes its settlement
/// (<c>Dokument.Rozrachunek</c>), so the settlement header time is reported and used by <c>modifiedSince</c> as well.
/// </para>
/// <para>All lookups are single SQL queries. Must run on the SDK thread.</para>
/// </remarks>
public static class DocumentReader
{
    /// <summary>Header state of one document.</summary>
    public sealed class Stamp
    {
        public int Id { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
        public DateTimeOffset? ModifiedAt { get; set; }
        public DateTimeOffset? SettlementModifiedAt { get; set; }
        public bool? IsCanceled { get; set; }
    }

    /// <summary>Stamps of the given documents (one SQL query; meant for one page of ids).</summary>
    public static Dictionary<int, Stamp> Stamps(Uchwyt sfera, IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0) return new Dictionary<int, Stamp>();

        var list = ids.Distinct().ToList();
        return Project(sfera.Dokumenty().Dane.Wszystkie().Where(d => list.Contains(d.Id)))
            .ToList()
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>Stamps of every document (one SQL query over the id and header columns), for sorting by modification time.</summary>
    public static Dictionary<int, Stamp> AllStamps(Uchwyt sfera) =>
        Project(sfera.Dokumenty().Dane.Wszystkie())
            .ToList()
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.First());

    /// <summary>Ids of documents changed at or after <paramref name="since"/>: the document itself or its settlement.</summary>
    public static HashSet<int> ModifiedSince(Uchwyt sfera, DateTimeOffset since) =>
        sfera.Dokumenty().Dane.Wszystkie()
            .Where(d => (d.Naglowek != null && d.Naglowek.Zmieniono >= since)
                        || (d.Rozrachunek != null && d.Rozrachunek.Naglowek != null && d.Rozrachunek.Naglowek.Zmieniono >= since))
            .Select(d => d.Id)
            .ToList()
            .ToHashSet();

    /// <summary>Ids of cancelled (invalidated) documents.</summary>
    public static HashSet<int> CanceledIds(Uchwyt sfera) =>
        sfera.Dokumenty().Dane.Wszystkie()
            .Where(d => d.StatusDokumentu != null && d.StatusDokumentu.Uniewazniony)
            .Select(d => d.Id)
            .ToList()
            .ToHashSet();

    /// <summary>Fills isCanceled and the header times of list rows.</summary>
    public static void ApplyStamps(IEnumerable<DocumentListItemDto> items, IReadOnlyDictionary<int, Stamp> stamps)
    {
        foreach (var item in items)
        {
            if (!stamps.TryGetValue(item.Id, out var stamp)) continue;

            item.IsCanceled = stamp.IsCanceled;
            item.CreatedAt = stamp.CreatedAt;
            item.ModifiedAt = stamp.ModifiedAt;
            item.SettlementModifiedAt = stamp.SettlementModifiedAt;
        }
    }

    /// <summary>Overwrites isCanceled, createdAt and modifiedAt of the document detail (local time, as before).</summary>
    public static void EnrichDetail(DocumentDto dto, object entity)
    {
        if (entity is not Dokument dokument) return;

        var status = SdkMember.Read(() => dokument.StatusDokumentu, null);
        if (status != null) dto.IsCanceled = SdkMember.Read(() => status.Uniewazniony, false);

        var header = SdkMember.Read(() => dokument.Naglowek, null);
        if (header != null)
        {
            dto.CreatedAt = SdkMember.Read<DateTimeOffset?>(() => header.Utworzono, null)?.LocalDateTime ?? dto.CreatedAt;
            dto.ModifiedAt = SdkMember.Read<DateTimeOffset?>(() => header.Zmieniono, null)?.LocalDateTime ?? dto.ModifiedAt;
        }

        var settlementHeader = SdkMember.Read(() => dokument.Rozrachunek == null ? null : dokument.Rozrachunek.Naglowek, null);
        dto.SettlementModifiedAt = settlementHeader == null
            ? null
            : SdkMember.Read<DateTimeOffset?>(() => settlementHeader.Zmieniono, null);
    }

    private static IQueryable<Stamp> Project(IQueryable<Dokument> documents) =>
        documents.Select(d => new Stamp
        {
            Id = d.Id,
            CreatedAt = d.Naglowek == null ? (DateTimeOffset?)null : d.Naglowek.Utworzono,
            ModifiedAt = d.Naglowek == null ? (DateTimeOffset?)null : d.Naglowek.Zmieniono,
            SettlementModifiedAt = d.Rozrachunek == null || d.Rozrachunek.Naglowek == null
                ? (DateTimeOffset?)null
                : d.Rozrachunek.Naglowek.Zmieniono,
            IsCanceled = d.StatusDokumentu == null ? (bool?)null : d.StatusDokumentu.Uniewazniony,
        });
}
