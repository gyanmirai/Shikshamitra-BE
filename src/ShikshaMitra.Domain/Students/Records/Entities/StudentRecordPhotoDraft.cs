using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public class StudentRecordPhotoDraft : CreationAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid UploadedBy { get; private set; }
	public string BlobName { get; private set; } = string.Empty;
	public string ContentType { get; private set; } = string.Empty;
	public string FileName { get; private set; } = string.Empty;
	public DateTimeOffset ExpiresAt { get; private set; }
	protected StudentRecordPhotoDraft() { }
	public StudentRecordPhotoDraft(Guid id, Guid? tenantId, Guid studentId, Guid actor, string blobName, string contentType,
		string fileName, DateTimeOffset expires) : base(id)
	{
		TenantId = tenantId; StudentId = studentId; UploadedBy = actor; ExpiresAt = expires;
		BlobName = Check.NotNullOrWhiteSpace(blobName, nameof(blobName), 512);
		ContentType = Check.NotNullOrWhiteSpace(contentType, nameof(contentType), 80);
		FileName = Check.NotNullOrWhiteSpace(fileName, nameof(fileName), 255);
	}
}
