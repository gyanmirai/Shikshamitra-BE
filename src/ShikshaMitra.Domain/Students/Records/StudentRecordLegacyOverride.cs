using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

/// <summary>An audited exception for verified historical enrollment; never an admission shortcut.</summary>
public class StudentRecordLegacyOverride : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid RequestedByUserId { get; private set; }
	public Guid? DecidedByUserId { get; private set; }
	public string Reason { get; private set; } = "";
	public string? EvidenceReference { get; private set; }
	public string? DecisionRemarks { get; private set; }
	public string Status { get; private set; } = "Pending";
	public DateTime? DecidedAt { get; private set; }
	protected StudentRecordLegacyOverride() { }
	public StudentRecordLegacyOverride(Guid id, Guid tenantId, Guid studentId, Guid requester, string reason, string? evidence) : base(id)
	{
		TenantId = tenantId;
		StudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		RequestedByUserId = Check.NotDefaultOrNull<Guid>(requester, nameof(requester));
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 2000, 10).Trim();
		EvidenceReference = evidence?.Trim();
		if (EvidenceReference?.Length > 1000) throw new BusinessException("Students:Records:InvalidLegacyEvidence");
	}
	public void Decide(Guid userId, bool approve, string? remarks, DateTime at)
	{
		if (Status != "Pending") throw new BusinessException("Students:Records:LegacyOverrideAlreadyDecided");
		if (userId == Guid.Empty || userId == RequestedByUserId) throw new BusinessException("Students:Records:SelfApprovalDenied");
		if (remarks?.Length > 2000) throw new BusinessException("Students:Records:InvalidLegacyRemarks");
		Status = approve ? "Approved" : "Rejected";
		DecidedByUserId = userId;
		DecisionRemarks = remarks?.Trim();
		DecidedAt = at;
	}
}
