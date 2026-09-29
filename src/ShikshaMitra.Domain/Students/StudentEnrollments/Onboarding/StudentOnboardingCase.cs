using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class StudentOnboardingCase : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid ApplicantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid OfferingId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid IntendedBoardId { get; private set; }
	public Guid IntendedMediumId { get; private set; }
	public Guid IntendedStageId { get; private set; }
	public Guid IntendedStandardId { get; private set; }
	public StudentOnboardingStatus Status { get; private set; }
	public string IdempotencyKey { get; private set; } = string.Empty;
	public string? ActivationIdempotencyKey { get; private set; }
	public Guid? AssignedToUserId { get; private set; }
	public DateTime? GuardianContactVerifiedAt { get; private set; }
	public DateTime DueAt { get; private set; }
	public bool IsHistoricalBackfill { get; private set; }
	public Guid? DivisionId { get; private set; }
	public Guid? SeatReservationId { get; private set; }
	public Guid? IdentifierAllocationId { get; private set; }
	public int? RollNumber { get; private set; }
	public Guid? StudentEnrollmentId { get; private set; }
	public DateTime? StartedAt { get; private set; }
	public DateTime? ReadyAt { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public DateTime? CancelledAt { get; private set; }
	public string? CancellationReason { get; private set; }
	public string? LastError { get; private set; }

	protected StudentOnboardingCase() { }

	public StudentOnboardingCase(
		Guid id,
		Guid? tenantId,
		Guid applicationId,
		Guid applicantId,
		Guid studentId,
		Guid offeringId,
		Guid academicYearId,
		Guid intendedBoardId,
		Guid intendedMediumId,
		Guid intendedStageId,
		Guid intendedStandardId,
		string idempotencyKey,
		DateTime? guardianContactVerifiedAt,
		DateTime dueAt) : base(id)
	{
		TenantId = tenantId;
		ApplicationId = Check.NotDefaultOrNull<Guid>(applicationId, nameof(applicationId));
		ApplicantId = Check.NotDefaultOrNull<Guid>(applicantId, nameof(applicantId));
		StudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		OfferingId = Check.NotDefaultOrNull<Guid>(offeringId, nameof(offeringId));
		AcademicYearId = Check.NotDefaultOrNull<Guid>(academicYearId, nameof(academicYearId));
		IntendedBoardId = Check.NotDefaultOrNull<Guid>(intendedBoardId, nameof(intendedBoardId));
		IntendedMediumId = Check.NotDefaultOrNull<Guid>(intendedMediumId, nameof(intendedMediumId));
		IntendedStageId = Check.NotDefaultOrNull<Guid>(intendedStageId, nameof(intendedStageId));
		IntendedStandardId = Check.NotDefaultOrNull<Guid>(intendedStandardId, nameof(intendedStandardId));
		IdempotencyKey = Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey), 128).Trim();
		GuardianContactVerifiedAt = guardianContactVerifiedAt?.ToUniversalTime();
		DueAt = dueAt.ToUniversalTime();
		if (GuardianContactVerifiedAt.HasValue && DueAt <= GuardianContactVerifiedAt.Value)
			throw new ArgumentException("The onboarding due time must be after finalization.", nameof(dueAt));
		Status = StudentOnboardingStatus.Pending;
	}

	public void CompleteHistoricalImport(Guid studentEnrollmentId, Guid? divisionId, int? rollNumber, DateTime completedAt)
	{
		EnsureEditable();
		StudentEnrollmentId = Check.NotDefaultOrNull<Guid>(studentEnrollmentId, nameof(studentEnrollmentId));
		DivisionId = divisionId;
		RollNumber = rollNumber;
		CompletedAt = completedAt.ToUniversalTime();
		DueAt = CompletedAt.Value;
		Status = StudentOnboardingStatus.Completed;
		IsHistoricalBackfill = true;
		LastError = null;
	}

	public void Start(DateTime at)
	{
		EnsureEditable();
		if (Status == StudentOnboardingStatus.Pending)
		{
			Status = StudentOnboardingStatus.InProgress;
			StartedAt = at.ToUniversalTime();
		}
	}

	public void RecordProgress(DateTime at)
	{
		EnsureEditable();
		if (!StartedAt.HasValue) StartedAt = at.ToUniversalTime();
		Status = StudentOnboardingStatus.InProgress;
		ReadyAt = null;
		LastError = null;
	}

	public void AssignOwner(Guid? userId)
	{
		EnsureEditable();
		AssignedToUserId = userId;
	}

	public void RecordPlacement(Guid divisionId, Guid reservationId, DateTime at)
	{
		EnsureEditable();
		Start(at);
		DivisionId = Check.NotDefaultOrNull<Guid>(divisionId, nameof(divisionId));
		SeatReservationId = Check.NotDefaultOrNull<Guid>(reservationId, nameof(reservationId));
		Status = StudentOnboardingStatus.InProgress;
		ReadyAt = null;
	}

	public void RecordIdentifier(Guid? allocationId, int rollNumber, DateTime at)
	{
		EnsureEditable();
		if (rollNumber <= 0) throw new BusinessException(StudentOnboardingErrorCodes.RollNumberInvalid);
		Start(at);
		IdentifierAllocationId = allocationId;
		RollNumber = rollNumber;
		Status = StudentOnboardingStatus.InProgress;
		ReadyAt = null;
	}

	public void ClearRollNumberForPlacementChange(DateTime at)
	{
		EnsureEditable();
		RollNumber = null;
		Start(at);
		Status = StudentOnboardingStatus.InProgress;
		ReadyAt = null;
	}

	public void MarkReady(DateTime at)
	{
		EnsureEditable();
		if (!DivisionId.HasValue || !SeatReservationId.HasValue || !RollNumber.HasValue)
			throw new BusinessException(StudentOnboardingErrorCodes.RequiredStepsIncomplete);
		Status = StudentOnboardingStatus.ReadyForActivation;
		ReadyAt = at.ToUniversalTime();
		LastError = null;
	}

	public void BeginActivation(string idempotencyKey)
	{
		var normalized = Check.NotNullOrWhiteSpace(idempotencyKey, nameof(idempotencyKey), 128).Trim();
		if (ActivationIdempotencyKey != null && !string.Equals(ActivationIdempotencyKey, normalized, StringComparison.Ordinal))
			throw new BusinessException(StudentOnboardingErrorCodes.ConcurrencyConflict);
		ActivationIdempotencyKey = normalized;
	}

	public void RequireAttention(string reason)
	{
		if (Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		LastError = Check.NotNullOrWhiteSpace(reason, nameof(reason), 2000).Trim();
		Status = StudentOnboardingStatus.NeedsAttention;
	}

	public void Complete(Guid studentEnrollmentId, DateTime at)
	{
		if (Status == StudentOnboardingStatus.Completed && StudentEnrollmentId == studentEnrollmentId) return;
		if (Status != StudentOnboardingStatus.ReadyForActivation)
			throw new BusinessException(StudentOnboardingErrorCodes.RequiredStepsIncomplete);
		StudentEnrollmentId = Check.NotDefaultOrNull<Guid>(studentEnrollmentId, nameof(studentEnrollmentId));
		CompletedAt = at.ToUniversalTime();
		Status = StudentOnboardingStatus.Completed;
		LastError = null;
	}

	public string GetAdmissionRoute() => ExtraProperties.TryGetValue("AdmissionRoute", out var route)
		? route?.ToString() ?? "Fresh admission" : "Fresh admission";

	public void SetAdmissionRoute(string route)
	{
		EnsureEditable();
		if (route is not ("Fresh admission" or "Transfer student" or "Mid-year admission" or "Returning student"))
			throw new BusinessException(StudentOnboardingErrorCodes.PolicyScopeInvalid);
		ExtraProperties["AdmissionRoute"] = route;
		RecordProgress(DateTime.UtcNow);
	}

	public void BeginProvisioning(Guid studentEnrollmentId)
	{
		if (StudentEnrollmentId == studentEnrollmentId) return;
		if (Status != StudentOnboardingStatus.ReadyForActivation)
			throw new BusinessException(StudentOnboardingErrorCodes.RequiredStepsIncomplete);
		StudentEnrollmentId = Check.NotDefaultOrNull<Guid>(studentEnrollmentId, nameof(studentEnrollmentId));
		Status = StudentOnboardingStatus.Provisioning;
		CompletedAt = null;
		LastError = null;
	}

	public void CompleteProvisioning(DateTime at)
	{
		if (Status == StudentOnboardingStatus.Completed) return;
		if (!StudentEnrollmentId.HasValue || Status == StudentOnboardingStatus.Cancelled)
			throw new BusinessException(StudentOnboardingErrorCodes.RequiredStepsIncomplete);
		Status = StudentOnboardingStatus.Completed;
		CompletedAt = at.ToUniversalTime();
		LastError = null;
	}

	public void Cancel(string reason, DateTime at)
	{
		EnsureEditable();
		CancellationReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 1000).Trim();
		CancelledAt = at.ToUniversalTime();
		Status = StudentOnboardingStatus.Cancelled;
	}

	private void EnsureEditable()
	{
		if (StudentEnrollmentId.HasValue || Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
	}
}
