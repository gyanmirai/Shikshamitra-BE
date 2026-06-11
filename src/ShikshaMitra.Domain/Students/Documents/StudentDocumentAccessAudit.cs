using System;
using ShikshaMitra.Students.Documents;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Documents;

public class StudentDocumentAccessAudit : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid? DocumentId { get; private set; }
	public Guid? DocumentTypeId { get; private set; }
	public StudentDocumentAuditAction Action { get; private set; }
	public Guid? ActorUserId { get; private set; }
	public DateTimeOffset EventTime { get; private set; }
	public string? FileName { get; private set; }
	public string? ContentType { get; private set; }
	public long? Size { get; private set; }
	public string? Sha256Hash { get; private set; }
	public string? ReasonCode { get; private set; }

	private StudentDocumentAccessAudit()
	{
	}

	public StudentDocumentAccessAudit(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid? documentId,
		Guid? documentTypeId,
		StudentDocumentAuditAction action,
		Guid? actorUserId,
		DateTimeOffset eventTime,
		string? fileName,
		string? contentType,
		long? size,
		string? sha256Hash,
		string? reasonCode) : base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		DocumentId = documentId;
		DocumentTypeId = documentTypeId;
		Action = action;
		ActorUserId = actorUserId;
		EventTime = eventTime;
		FileName = fileName;
		ContentType = contentType;
		Size = size;
		Sha256Hash = sha256Hash;
		ReasonCode = reasonCode;
	}
}
