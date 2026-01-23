using System.ComponentModel.DataAnnotations;

namespace NexoSferaApi.Models.Requests;

/// <summary>
/// Request model for associating documents
/// </summary>
public class DocumentAssociationRequest
{
    /// <summary>
    /// ID of the target document to associate with
    /// </summary>
    [Required]
    public int TargetDocumentId { get; set; }

    /// <summary>
    /// Type of relation: "related" (default), "realization", "correction"
    /// </summary>
    public string RelationType { get; set; } = "related";
}

/// <summary>
/// Request model for bulk document associations
/// </summary>
public class BulkDocumentAssociationRequest
{
    /// <summary>
    /// Source document ID
    /// </summary>
    [Required]
    public int SourceDocumentId { get; set; }

    /// <summary>
    /// List of target document IDs to associate
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<int> TargetDocumentIds { get; set; } = new();

    /// <summary>
    /// Type of relation: "related" (default), "realization", "correction"
    /// </summary>
    public string RelationType { get; set; } = "related";
}
