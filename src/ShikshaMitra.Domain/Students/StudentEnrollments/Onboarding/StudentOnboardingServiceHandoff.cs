using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class StudentOnboardingServiceHandoff : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid OnboardingCaseId { get; private set; }
	public string ServiceKey { get; private set; } = string.Empty;
	public bool Required { get; private set; }
	public StudentOnboardingServiceHandoffStatus Status { get; private set; }
	public string IdempotencyKey { get; private set; } = string.Empty;
	public int AttemptCount { get; private set; }
	public DateTime? LastAttemptAt { get; private set; }
	public DateTime? AcknowledgedAt { get; private set; }
	public string? LastError { get; private set; }

	protected StudentOnboardingServiceHandoff() { }

	public StudentOnboardingServiceHandoff(Guid id, Guid? tenantId, Guid onboardingCaseId,
		string serviceKey, bool required, string idempotencyKey) : base(id)
	{
		TenantId = tenantId;
		OnboardingCaseId = Check.NotDefaultOrNull<Guid>(onboardingCaseId, nameof(onboardingCaseId));
		ServiceKey = Check.NotNullOrWhiteSpace(serviceKey, nameof(serviceKey), 80).Trim().ToLowerInvariant();
		Required = required;
		IdempotencyKey = Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey), 160).Trim();
		Status = StudentOnboardingServiceHandoffStatus.Pending;
	}

	public void MarkDispatched(DateTime at)
	{
		if (Status == StudentOnboardingServiceHandoffStatus.Acknowledged) return;
		Status = StudentOnboardingServiceHandoffStatus.Dispatched;
		AttemptCount++;
		LastAttemptAt = at.ToUniversalTime();
		LastError = null;
	}

	public void Acknowledge(DateTime at)
	{
		Status = StudentOnboardingServiceHandoffStatus.Acknowledged;
		AcknowledgedAt = at.ToUniversalTime();
		LastError = null;
	}

	public void Fail(string error, DateTime at)
	{
		Status = StudentOnboardingServiceHandoffStatus.Failed;
		AttemptCount++;
		LastAttemptAt = at.ToUniversalTime();
		LastError = Check.NotNullOrWhiteSpace(error, nameof(error), 2000).Trim();
	}

	public void QueueRetry()
	{
		if (Status != StudentOnboardingServiceHandoffStatus.Failed)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		Status = StudentOnboardingServiceHandoffStatus.Pending;
		LastError = null;
	}
}
