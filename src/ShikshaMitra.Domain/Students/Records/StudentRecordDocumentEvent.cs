using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public class StudentRecordDocumentEvent : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid DocumentId { get; private set; }
	public Guid ActorUserId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public string DocumentType { get; private set; } = "";
	public string Action { get; private set; } = "";
	public string? Reason { get; private set; }
	protected StudentRecordDocumentEvent() { }
	public StudentRecordDocumentEvent(Guid id, Guid tenantId, Guid studentId, Guid documentId, Guid actor, string documentType, string action, string? reason, Guid? academicYearId = null) : base(id)
	{
		TenantId = Check.NotDefaultOrNull<Guid>(tenantId, nameof(tenantId)); StudentId = studentId; DocumentId = documentId;
		ActorUserId = Check.NotDefaultOrNull<Guid>(actor, nameof(actor)); DocumentType = documentType; Action = action; AcademicYearId = academicYearId;
		if (action is "Reprint" or "Void" or "Duplicate") Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 2000, 5).Trim();
		else Reason = reason;
	}
}
