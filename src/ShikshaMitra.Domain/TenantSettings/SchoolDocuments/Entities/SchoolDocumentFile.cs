using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.SchoolDocuments;

public class SchoolDocumentFile : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid DocumentId { get; private set; }
	public string FileName { get; private set; } = "";
	public string ContentType { get; private set; } = "";
	public long Size { get; private set; }
	public string BlobName { get; private set; } = "";

	protected SchoolDocumentFile() { }

	public SchoolDocumentFile(Guid id, Guid tenantId, Guid documentId, string fileName, string contentType, long size) : base(id)
	{
		TenantId = tenantId;
		DocumentId = documentId;
		FileName = fileName;
		ContentType = contentType;
		Size = size;
		BlobName = $"school-documents/{tenantId:N}/{documentId:N}/{id:N}";
	}
}
