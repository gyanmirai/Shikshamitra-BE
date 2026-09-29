using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class StudentOnboardingStep : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid OnboardingCaseId { get; private set; }
	public string Key { get; private set; } = string.Empty;
	public string Label { get; private set; } = string.Empty;
	public int DisplayOrder { get; private set; }
	public bool Required { get; private set; }
	public StudentOnboardingStepStatus Status { get; private set; }
	public string? Detail { get; private set; }
	public Guid? CompletedByUserId { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public string? WaiverReason { get; private set; }

	protected StudentOnboardingStep() { }

	public StudentOnboardingStep(Guid id, Guid? tenantId, Guid onboardingCaseId, string key,
		string label, int displayOrder, bool required, StudentOnboardingStepStatus status = StudentOnboardingStepStatus.Pending)
		: base(id)
	{
		TenantId = tenantId;
		OnboardingCaseId = Check.NotDefaultOrNull<Guid>(onboardingCaseId, nameof(onboardingCaseId));
		Key = Check.NotNullOrWhiteSpace(key, nameof(key), 64).Trim();
		Label = Check.NotNullOrWhiteSpace(label, nameof(label), 160).Trim();
		DisplayOrder = displayOrder;
		Required = required;
		Status = status;
	}

	public void Begin(string? detail)
	{
		Status = StudentOnboardingStepStatus.InProgress;
		Detail = Normalize(detail);
		ClearCompletion();
	}

	public void Reset(string? detail)
	{
		Status = Required ? StudentOnboardingStepStatus.Pending : StudentOnboardingStepStatus.NotRequired;
		Detail = Normalize(detail);
		ClearCompletion();
	}

	public void Complete(Guid? userId, DateTime at, string? detail)
	{
		Status = StudentOnboardingStepStatus.Completed;
		Detail = Normalize(detail);
		CompletedByUserId = userId;
		CompletedAt = at.ToUniversalTime();
		WaiverReason = null;
	}

	public void Block(string detail)
	{
		Status = StudentOnboardingStepStatus.Blocked;
		Detail = Check.NotNullOrWhiteSpace(detail, nameof(detail), 1000).Trim();
		ClearCompletion();
	}

	public void Waive(Guid userId, DateTime at, string reason)
	{
		WaiverReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 1000).Trim();
		Status = StudentOnboardingStepStatus.Waived;
		CompletedByUserId = userId;
		CompletedAt = at.ToUniversalTime();
		Detail = "Waived by an authorized user.";
	}

	public void SetRequired(bool required)
	{
		Required = required;
		if (!required && Status is StudentOnboardingStepStatus.Pending or StudentOnboardingStepStatus.Blocked or StudentOnboardingStepStatus.InProgress)
			Status = StudentOnboardingStepStatus.NotRequired;
		else if (required && Status == StudentOnboardingStepStatus.NotRequired)
			Status = StudentOnboardingStepStatus.Pending;
	}

	private void ClearCompletion()
	{
		CompletedByUserId = null;
		CompletedAt = null;
		WaiverReason = null;
	}

	private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
