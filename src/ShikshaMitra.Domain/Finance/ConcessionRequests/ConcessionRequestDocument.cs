using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Stores the URL and upload status for one supporting document attached to a
/// <see cref="ConcessionRequest"/> (Step 5 — Document Upload).
///
/// <para>
/// <see cref="DocumentId"/> is a foreign key to <c>ConcessionTypeDocument.Id</c> — the
/// document template defined on the concession type. Each upload record fills one
/// document slot for that concession type.
/// </para>
/// </summary>
public class ConcessionRequestDocument : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    /// <summary>Parent concession request this document belongs to.</summary>
    public Guid ConcessionRequestId { get; private set; }

    /// <summary>
    /// The document-template slot being filled.
    /// References <c>ConcessionTypeDocument.Id</c>.
    /// </summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Presigned S3 or permanent URL of the uploaded file.</summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>Upload state: Pending (0) or Uploaded (1).</summary>
    public DocumentUploadStatus Status { get; private set; }

    protected ConcessionRequestDocument()
    {
        /* Required by ORM / deserialization */
    }

    public ConcessionRequestDocument(
        Guid id,
        Guid? tenantId,
        Guid concessionRequestId,
        Guid documentId,
        string url,
        DocumentUploadStatus status) : base(id)
    {
        TenantId            = tenantId;
        ConcessionRequestId = Check.NotNull(concessionRequestId, nameof(concessionRequestId));
        DocumentId          = Check.NotNull(documentId, nameof(documentId));
        Url                 = Check.NotNullOrWhiteSpace(url, nameof(url));
        Status              = status;
    }

    /// <summary>Replaces the URL and status (used during full-replacement writes).</summary>
    internal void Update(string url, DocumentUploadStatus status)
    {
        Url    = Check.NotNullOrWhiteSpace(url, nameof(url));
        Status = status;
    }
}
