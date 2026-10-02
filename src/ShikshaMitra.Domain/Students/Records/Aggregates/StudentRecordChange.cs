using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

/// <summary>A policy-snapshotted command; applied history is retained when corrected.</summary>
public class StudentRecordChange : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid RequestedBy { get; private set; }
	public Guid? CorrectsChangeId { get; private set; }
	public Guid? PolicyVersionId { get; private set; }
	public string Section { get; private set; } = string.Empty;
	public string Action { get; private set; } = string.Empty;
	public string ClientKey { get; private set; } = string.Empty;
	public string Reason { get; private set; } = string.Empty;
	public string BeforeJson { get; private set; } = "{}";
	public string PayloadJson { get; private set; } = "{}";
	public string PolicyJson { get; private set; } = "{}";
	public string ApprovalStagesJson { get; private set; } = "[]";
	public string Status { get; private set; } = "Pending";
	public int ApprovalCount { get; private set; }
	public int RequiredApprovals { get; private set; }
	public DateTimeOffset EffectiveAt { get; private set; }
	public DateTimeOffset? AppliedAt { get; private set; }
	public string? ExceptionCode { get; private set; }

	protected StudentRecordChange() { }

	public StudentRecordChange(Guid id, Guid? tenantId, Guid studentId, Guid yearId, Guid requester,
		string section, string action, string clientKey, string reason, string beforeJson, string payloadJson,
		string policyJson, Guid? policyVersionId, string approvalStagesJson, int requiredApprovals,
		DateTimeOffset effectiveAt, Guid? correctsChangeId = null) : base(id)
	{
		TenantId = tenantId; StudentId = studentId; AcademicYearId = yearId; RequestedBy = requester;
		Section = Check.NotNullOrWhiteSpace(section, nameof(section), 40);
		Action = Check.NotNullOrWhiteSpace(action, nameof(action), 40);
		ClientKey = Check.NotNullOrWhiteSpace(clientKey, nameof(clientKey), 80);
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 2000).Trim();
		BeforeJson = Check.NotNullOrWhiteSpace(beforeJson, nameof(beforeJson), 65536);
		PayloadJson = Check.NotNullOrWhiteSpace(payloadJson, nameof(payloadJson), 65536);
		PolicyJson = Check.NotNullOrWhiteSpace(policyJson, nameof(policyJson), 65536);
		ApprovalStagesJson = approvalStagesJson;
		PolicyVersionId = policyVersionId; CorrectsChangeId = correctsChangeId;
		if (requiredApprovals is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(requiredApprovals));
		RequiredApprovals = requiredApprovals; EffectiveAt = effectiveAt;
		Status = requiredApprovals == 0 ? "Approved" : "Pending";
	}

	public void Approve(Guid actor)
	{
		RequireStatus("Pending");
		if (actor == RequestedBy) throw new BusinessException("Students:Records:SelfApproval");
		ApprovalCount++;
		if (ApprovalCount == RequiredApprovals) Status = "Approved";
	}
	public void Reject(Guid actor)
	{
		RequireStatus("Pending");
		if (actor == RequestedBy) throw new BusinessException("Students:Records:SelfApproval");
		Status = "Rejected";
	}
	public void Cancel(Guid actor)
	{
		if (actor != RequestedBy) throw new BusinessException("Students:Records:RequesterOnly");
		if (Status is not ("Pending" or "Scheduled")) throw new BusinessException("Students:Records:InvalidChangeState");
		Status = "Cancelled";
	}
	public void Schedule() { RequireStatus("Approved"); Status = "Scheduled"; }
	public void Apply(DateTimeOffset now)
	{
		if (Status is not ("Approved" or "Scheduled") || now < EffectiveAt)
			throw new BusinessException("Students:Records:InvalidChangeState");
		Status = "Applied"; AppliedAt = now; ExceptionCode = null;
	}
	public void MarkConflict(string code)
	{
		if (Status is not ("Approved" or "Scheduled")) throw new BusinessException("Students:Records:InvalidChangeState");
		Status = "Conflict"; ExceptionCode = Check.NotNullOrWhiteSpace(code, nameof(code), 120);
	}
	private void RequireStatus(string expected)
	{
		if (Status != expected) throw new BusinessException("Students:Records:InvalidChangeState");
	}
}
