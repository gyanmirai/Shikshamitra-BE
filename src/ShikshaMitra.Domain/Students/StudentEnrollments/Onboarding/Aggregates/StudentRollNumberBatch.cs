using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Immutable full-division proposal with one approval and atomic application lifecycle.</summary>
public class StudentRollNumberBatch : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid DivisionId { get; private set; }
	public Guid RequestedBy { get; private set; }
	public string Operation { get; private set; } = "assign-missing";
	public string Status { get; private set; } = "Preview";
	public string RosterHash { get; private set; } = "";
	public string RuleJson { get; private set; } = "{}";
	public string RowsJson { get; private set; } = "[]";
	public string PolicyJson { get; private set; } = "{}";
	public string ApprovalStagesJson { get; private set; } = "[]";
	public string DecisionsJson { get; private set; } = "[]";
	public string Reason { get; private set; } = "";
	public string? SubmissionKey { get; private set; }
	public DateTimeOffset PreviewedAt { get; private set; }
	public DateTimeOffset? AppliedAt { get; private set; }
	protected StudentRollNumberBatch() { }
	public StudentRollNumberBatch(Guid id, Guid tenantId, Guid yearId, Guid standardId, Guid divisionId,
		Guid requester, string operation, string rosterHash, string ruleJson, string rowsJson, string policyJson, DateTimeOffset now) : base(id)
	{
		if (operation is not ("assign-missing" or "renumber")) throw new BusinessException(StudentRollNumberErrorCodes.InvalidBatch);
		TenantId = tenantId; AcademicYearId = yearId; StandardId = standardId; DivisionId = divisionId; RequestedBy = requester;
		Operation = operation; RosterHash = rosterHash; RuleJson = ruleJson; RowsJson = rowsJson; PolicyJson = policyJson; PreviewedAt = now;
	}
	public IReadOnlyList<StudentRollBatchRow> Rows() => JsonSerializer.Deserialize<List<StudentRollBatchRow>>(RowsJson)!;
	public IReadOnlyList<StudentRollBatchDecision> Decisions() => JsonSerializer.Deserialize<List<StudentRollBatchDecision>>(DecisionsJson)!;
	public void Submit(string key, string reason, string stagesJson, DateTimeOffset now)
	{
		if (Status != "Preview") throw new BusinessException(StudentRollNumberErrorCodes.InvalidState);
		if (now - PreviewedAt > TimeSpan.FromMinutes(15)) throw new BusinessException(StudentRollNumberErrorCodes.StalePreview);
		if (Rows().Count == 0 || Rows().Any(x => x.Issue != null || x.ProposedRollNumber is not > 0)) throw new BusinessException(StudentRollNumberErrorCodes.BatchBlocked);
		if (Operation == "renumber" && string.IsNullOrWhiteSpace(reason)) throw new BusinessException(StudentRollNumberErrorCodes.InvalidBatch);
		SubmissionKey = Check.NotNullOrWhiteSpace(key, nameof(key), 100);
		Reason = Operation == "renumber" ? Check.NotNullOrWhiteSpace(reason, nameof(reason), 1000).Trim() : Check.Length(reason.Trim(), nameof(reason), 1000)!;
		ApprovalStagesJson = stagesJson;
		using var stages = JsonDocument.Parse(stagesJson);
		Status = stages.RootElement.GetArrayLength() == 0 ? "Approved" : "PendingApproval";
	}
	public void Decide(Guid actor, bool approve, string reason, DateTimeOffset now)
	{
		if (Status != "PendingApproval" || actor == RequestedBy || Decisions().Any(x => x.ActorId == actor)) throw new BusinessException(StudentRollNumberErrorCodes.ApproverRequired);
		var decisions = Decisions().ToList(); decisions.Add(new(actor, decisions.Count, approve, reason.Trim(), now));
		DecisionsJson = JsonSerializer.Serialize(decisions);
		using var stages = JsonDocument.Parse(ApprovalStagesJson);
		Status = !approve ? "Rejected" : decisions.Count == stages.RootElement.GetArrayLength() ? "Approved" : "PendingApproval";
	}
	public void Apply(DateTimeOffset now)
	{
		if (Status != "Approved") throw new BusinessException(StudentRollNumberErrorCodes.InvalidState);
		Status = "Applied"; AppliedAt = now;
	}
}
