using System;
using System.Text.Json;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

public sealed class StudentRecordTransition : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid BatchId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid? SourceEnrollmentId { get; private set; }
	public Guid? TargetAcademicYearId { get; private set; }
	public Guid? TargetStandardId { get; private set; }
	public Guid? TargetDivisionId { get; private set; }
	public string Outcome { get; private set; } = "";
	public string Status { get; private set; } = "Submitted";
	public string Reason { get; private set; } = "";
	public DateTime EffectiveDate { get; private set; }
	public string InputJson { get; private set; } = "{}";
	public string BeforeJson { get; private set; } = "{}";
	public string? AfterJson { get; private set; }
	public Guid? PolicyVersionId { get; private set; }
	public string PolicyJson { get; private set; } = "{}";
	public string ApprovalRuleJson { get; private set; } = "{}";
	public Guid RequestedBy { get; private set; }
	public int ApprovalStageCount { get; private set; }
	public int CurrentApprovalStage { get; private set; }
	public Guid? LastApprovedBy { get; private set; }
	public Guid? RenewalCaseId { get; private set; }
	public Guid? TargetEnrollmentId { get; private set; }
	public Guid? CorrectionOfId { get; private set; }
	public Guid? ReturnHandoffId => ExtraProperties.TryGetValue("ReturnHandoffId", out var raw) && Guid.TryParse(raw?.ToString(), out var id) ? id : null;
	public DateTime? AppliedAt { get; private set; }
	public string? LastError { get; private set; }
	private StudentRecordTransition() { }
	public StudentRecordTransition(Guid id, Guid tenantId, Guid batchId, Guid studentId, Guid yearId, Guid sourceEnrollmentId,
		string outcome, string reason, DateTime effectiveDate, string inputJson, string beforeJson,
		Guid? policyVersionId, string policyJson, string approvalRuleJson, Guid requestedBy, int approvalStageCount, Guid? correctionOfId = null)
		: this(id, tenantId, batchId, studentId, yearId, sourceEnrollmentId, outcome, reason, effectiveDate, inputJson,
			beforeJson, policyVersionId, policyJson, approvalRuleJson, requestedBy, approvalStageCount, correctionOfId, false) { }
	private StudentRecordTransition(Guid id, Guid tenantId, Guid batchId, Guid studentId, Guid yearId, Guid? sourceEnrollmentId,
		string outcome, string reason, DateTime effectiveDate, string inputJson, string beforeJson,
		Guid? policyVersionId, string policyJson, string approvalRuleJson, Guid requestedBy, int approvalStageCount,
		Guid? correctionOfId, bool historicalAdmissionReturn) : base(id)
	{
		TenantId = Check.NotDefaultOrNull<Guid>(tenantId, nameof(tenantId));
		BatchId = Check.NotDefaultOrNull<Guid>(batchId, nameof(batchId));
		StudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		AcademicYearId = Check.NotDefaultOrNull<Guid>(yearId, nameof(yearId));
		SourceEnrollmentId = historicalAdmissionReturn && !sourceEnrollmentId.HasValue ? null : Check.NotDefaultOrNull<Guid>(sourceEnrollmentId, nameof(sourceEnrollmentId));
		if (!Array.Exists(StudentRecordsPolicy.Outcomes, x => x == outcome) && !(outcome == "Correction" && correctionOfId.HasValue))
			throw new BusinessException("Students:Records:InvalidTransition");
		if (approvalStageCount is < 0 or > 3) throw new BusinessException(StudentRecordsErrorCodes.InvalidPolicy);
		Outcome = outcome; Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 1000).Trim(); EffectiveDate = DateTime.SpecifyKind(effectiveDate.Date, DateTimeKind.Utc);
		InputJson = Check.NotNullOrWhiteSpace(inputJson, nameof(inputJson), 20000); BeforeJson = beforeJson;
		using (var document = JsonDocument.Parse(InputJson))
		{
			TargetAcademicYearId = Target(document.RootElement, "TargetAcademicYearId");
			TargetStandardId = Target(document.RootElement, "TargetStandardId");
			TargetDivisionId = Target(document.RootElement, "TargetDivisionId");
		}
		PolicyVersionId = policyVersionId; PolicyJson = StudentRecordsPolicyValidator.ParseAndValidate(policyJson).ToJson();
		ApprovalRuleJson = approvalRuleJson; RequestedBy = Check.NotDefaultOrNull<Guid>(requestedBy, nameof(requestedBy));
		ApprovalStageCount = approvalStageCount; CorrectionOfId = correctionOfId;
		Status = approvalStageCount == 0 ? "Approved" : "Submitted";
	}
	public void Approve(Guid actor)
	{
		EnsureState("Submitted");
		if (actor == RequestedBy) throw new BusinessException("Students:Records:SelfApprovalDenied");
		CurrentApprovalStage++; LastApprovedBy = actor;
		if (CurrentApprovalStage == ApprovalStageCount) Status = "Approved";
	}
	public void CaptureOutstandingApproval(string evidence)
	{
		if (CurrentApprovalStage < 1 || LastApprovedBy == null || Status is not ("Submitted" or "Approved")) throw new BusinessException("Students:Records:InvalidChangeState");
		var key = "SchoolExitOutstandingStage" + CurrentApprovalStage;
		if (ExtraProperties.ContainsKey(key)) throw new BusinessException("Students:Records:InvalidChangeState");
		ExtraProperties[key] = evidence;
	}
	public string? GetOutstandingApproval(int stage) => ExtraProperties.TryGetValue("SchoolExitOutstandingStage" + stage, out var value) ? value?.ToString() : null;
	public static StudentRecordTransition CreateAdmissionReturn(Guid id, Guid tenantId, Guid batchId, Guid studentId,
		Guid yearId, Guid? sourceEnrollmentId, string reason, DateTime effectiveDate, string inputJson, string beforeJson,
		Guid? policyVersionId, string policyJson, string approvalRuleJson, Guid requestedBy, int approvalStageCount, Guid handoffId)
	{
		var item = new StudentRecordTransition(id, tenantId, batchId, studentId, yearId, sourceEnrollmentId,
			"Return", reason, effectiveDate, inputJson, beforeJson, policyVersionId, policyJson, approvalRuleJson, requestedBy, approvalStageCount, null, true);
		item.ExtraProperties["ReturnHandoffId"] = Check.NotDefaultOrNull<Guid>(handoffId, nameof(handoffId)).ToString("D");
		return item;
	}
	public void WaitForAdmission()
	{
		EnsureApplicable();
		if (!ReturnHandoffId.HasValue || Outcome != "Return") throw new BusinessException("Students:Records:InvalidReturnApplication");
		Status = "AwaitingAdmission";
	}
	public void CancelAdmissionReturn(bool activationHasStarted)
	{
		if (Status != "AwaitingAdmission" || !ReturnHandoffId.HasValue || activationHasStarted)
			throw new BusinessException("Students:Records:InvalidChangeState");
		Status = "Cancelled";
	}
	public void CaptureOnboardingRollRule(string json)
	{
		if (Status is not ("Submitted" or "Approved") || CurrentApprovalStage != 0 || ExtraProperties.ContainsKey("OnboardingRollRule")) throw new BusinessException("Students:Records:InvalidChangeState");
		using var document = JsonDocument.Parse(json);
		if (document.RootElement.ValueKind is not (JsonValueKind.Null or JsonValueKind.Object)) throw new BusinessException("Students:Records:InvalidPayload");
		ExtraProperties["OnboardingRollRule"] = json;
	}
	public string? GetOnboardingRollRule() => ExtraProperties.TryGetValue("OnboardingRollRule", out var value) ? value?.ToString() : null;
	public void Reject() { EnsureState("Submitted"); Status = "Rejected"; }
	public void Cancel(bool pendingRenewalCancelled = false) { if (!(pendingRenewalCancelled && Status == "AwaitingRenewal") && Status is not ("Submitted" or "Approved" or "Scheduled" or "Exception")) throw new BusinessException("Students:Records:InvalidChangeState"); Status = "Cancelled"; }
	public void Schedule() { EnsureState("Approved"); Status = "Scheduled"; }
	public void WaitForRenewal(Guid renewalId) { EnsureApplicable(); RenewalCaseId = renewalId; Status = "AwaitingRenewal"; }
	public void Apply(Guid? targetEnrollmentId, string afterJson, DateTime now)
	{
		if (Status != "AwaitingRenewal" && !(Status == "AwaitingAdmission" && ReturnHandoffId.HasValue)) EnsureApplicable();
		TargetEnrollmentId = targetEnrollmentId; AfterJson = afterJson; AppliedAt = now.ToUniversalTime(); Status = "Applied"; LastError = null;
	}
	public void MarkException(string code) { EnsureApplicable(); LastError = code; Status = "Exception"; }
	public void Retry() { EnsureState("Exception"); Status = "Approved"; LastError = null; }
	private void EnsureApplicable() { if (Status is not ("Approved" or "Scheduled")) throw new BusinessException("Students:Records:InvalidChangeState"); }
	private void EnsureState(string status) { if (Status != status) throw new BusinessException("Students:Records:InvalidChangeState"); }
	private static Guid? Target(JsonElement input, string name)
	{
		foreach (var property in input.EnumerateObject())
			if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind != JsonValueKind.Null)
				return property.Value.ValueKind == JsonValueKind.String && property.Value.TryGetGuid(out var id) && id != Guid.Empty ? id : throw new BusinessException("Students:Records:InvalidPayload");
		return null;
	}
}
