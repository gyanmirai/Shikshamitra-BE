using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Records;

/// <summary>Short year-specific onboarding, without invented Admission identifiers.</summary>
public sealed class StudentRecordRenewalCase : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid DivisionId { get; private set; }
	public int? RollNumber { get; private set; }
	public Guid? TransitionId { get; private set; }
	public Guid? StudentEnrollmentId { get; private set; }
	public string Status { get; private set; } = "PendingChecklist";
	public string PolicyJson { get; private set; } = "{}";
	public string OnboardingRulesJson { get; private set; } = "{}";
	public bool GuardianReviewConfirmed { get; private set; }
	public Guid? GuardianReviewedBy { get; private set; }
	public DateTime? GuardianReviewedAt { get; private set; }
	public string? ReviewRemarks { get; private set; }
	public string SourceSnapshotJson { get; private set; } = "{}";
	public string IdempotencyKey { get; private set; } = "";
	private StudentRecordRenewalCase() { }
	public StudentRecordRenewalCase(Guid id, Guid tenantId, Guid studentId, Guid yearId, Guid standardId, Guid divisionId,
		int? roll, Guid? transitionId, string policyJson, string onboardingRulesJson, string sourceSnapshotJson, string key) : base(id)
	{
		TenantId = tenantId; StudentId = studentId; AcademicYearId = yearId; StandardId = standardId; DivisionId = divisionId;
		RollNumber = roll; TransitionId = transitionId; PolicyJson = policyJson; OnboardingRulesJson = onboardingRulesJson;
		SourceSnapshotJson = sourceSnapshotJson; IdempotencyKey = Check.NotNullOrWhiteSpace(key, nameof(key), 128);
	}
	public void ConfirmGuardianReview(Guid actorId, DateTime now, string remarks)
	{
		if (Status != "PendingChecklist") throw new BusinessException("Students:Records:InvalidRenewalState");
		GuardianReviewConfirmed = true; GuardianReviewedBy = actorId; GuardianReviewedAt = now.ToUniversalTime();
		ReviewRemarks = Check.NotNullOrWhiteSpace(remarks, nameof(remarks), 1000);
	}
	public void Activate(Guid enrollmentId)
	{
		if (Status != "PendingChecklist") throw new BusinessException("Students:Records:InvalidRenewalState");
		StudentEnrollmentId = enrollmentId; Status = "Provisioning";
	}
	public void Complete()
	{
		if (Status != "Provisioning") throw new BusinessException("Students:Records:InvalidRenewalState");
		Status = "Completed";
	}
	public void Cancel()
	{
		if (Status != "PendingChecklist" || StudentEnrollmentId.HasValue) throw new BusinessException("Students:Records:InvalidRenewalState");
		Status = "Cancelled";
	}
	public void ChangeProvisioningPlacement(Guid standardId, Guid divisionId, int? rollNumber)
	{
		if (Status != "Provisioning" || standardId != StandardId || divisionId == Guid.Empty || rollNumber is <= 0)
			throw new BusinessException("Students:Records:InvalidRenewalState");
		DivisionId = divisionId; RollNumber = rollNumber;
	}
}
