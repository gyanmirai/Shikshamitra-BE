using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid? StudentId { get; private set; }
	public string ActivationReference { get; private set; } = string.Empty;
	public AdmissionCrmActivationStatus Status { get; private set; }
	public Guid EducationBoardId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public Guid AcademicMediumStageId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid DivisionId { get; private set; }
	public Guid GrNumberSeriesId { get; private set; }
	public int? RollNumber { get; private set; }
	public ShikshaMitra.Students.GenderEnum Gender { get; private set; }
	public bool RequireStudentAccount { get; private set; }
	public bool RequireGuardianAccount { get; private set; }
	public string ReadinessSnapshotJson { get; private set; } = "{}";
	public string StepsJson { get; private set; } = "[]";
	public string ServiceHandoffRequestsJson { get; private set; } = "[]";
	public Guid RequestedByUserId { get; private set; }
	public DateTime RequestedAt { get; private set; }
	public Guid? ApprovedByUserId { get; private set; }
	public DateTime? ApprovedAt { get; private set; }
	public int AttemptCount { get; private set; }
	public string? RepairStep { get; private set; }
	public string? RepairReason { get; private set; }
	public DateTime? FirstDayReadyAt { get; private set; }
	public Guid? CancellationRequestedByUserId { get; private set; }
	public DateTime? CancellationRequestedAt { get; private set; }
	public string? CancellationReason { get; private set; }
	public Guid? CancelledByUserId { get; private set; }
	public DateTime? CancelledAt { get; private set; }

	protected AdmissionCrmActivation() { }

	public AdmissionCrmActivation(Guid id, Guid? tenantId, Guid academicYearId, Guid leadId,
		string activationReference, Guid educationBoardId, Guid academicMediumId,
		Guid academicMediumStageId, Guid standardId, Guid divisionId, Guid grNumberSeriesId,
		int? rollNumber, ShikshaMitra.Students.GenderEnum gender, string readinessSnapshotJson,
		string serviceHandoffRequestsJson,
		bool requireStudentAccount, bool requireGuardianAccount,
		Guid requestedByUserId, DateTime requestedAt) : base(id)
	{
		TenantId = tenantId; AcademicYearId = academicYearId; LeadId = leadId;
		ActivationReference = Check.NotNullOrWhiteSpace(activationReference, nameof(activationReference), AdmissionCrmConsts.MaxActivationReferenceLength);
		EducationBoardId = educationBoardId; AcademicMediumId = academicMediumId;
		AcademicMediumStageId = academicMediumStageId; StandardId = standardId; DivisionId = divisionId;
		GrNumberSeriesId = grNumberSeriesId; RollNumber = rollNumber; Gender = gender;
		RequireStudentAccount = requireStudentAccount; RequireGuardianAccount = requireGuardianAccount;
		ReadinessSnapshotJson = Check.Length(readinessSnapshotJson, nameof(readinessSnapshotJson), AdmissionCrmConsts.MaxActivationStepsJsonLength);
		ServiceHandoffRequestsJson = ValidateArray(serviceHandoffRequestsJson);
		RequestedByUserId = requestedByUserId; RequestedAt = requestedAt.ToUniversalTime();
		Status = AdmissionCrmActivationStatus.ApprovalPending;
	}

	private static string ValidateArray(string json)
	{
		var value = Check.NotNullOrWhiteSpace(json, nameof(json), AdmissionCrmConsts.MaxActivationStepsJsonLength);
		using var document = System.Text.Json.JsonDocument.Parse(value);
		if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
			throw new BusinessException("AdmissionCrm:InvalidActivationServiceHandoffs");
		return value;
	}

	public void Approve(Guid userId, DateTime at)
	{
		if (Status != AdmissionCrmActivationStatus.ApprovalPending) throw new BusinessException("AdmissionCrm:ActivationNotAwaitingApproval");
		if (RequestedByUserId == userId) throw new BusinessException("AdmissionCrm:DistinctActivationApproverRequired");
		ApprovedByUserId = userId; ApprovedAt = at.ToUniversalTime(); Status = AdmissionCrmActivationStatus.Approved;
	}

	public void StartAttempt()
	{
		if (Status is not (AdmissionCrmActivationStatus.Approved or AdmissionCrmActivationStatus.RepairRequired or AdmissionCrmActivationStatus.InProgress))
			throw new BusinessException("AdmissionCrm:ActivationNotExecutable");
		AttemptCount++; Status = AdmissionCrmActivationStatus.InProgress; RepairStep = null; RepairReason = null;
	}

	public void LinkStudent(Guid studentId) => StudentId = studentId;
	public void UpdateSteps(string stepsJson) => StepsJson = Check.Length(stepsJson, nameof(stepsJson), AdmissionCrmConsts.MaxActivationStepsJsonLength);

	public void RequireRepair(string step, string reason)
	{
		RepairStep = Check.NotNullOrWhiteSpace(step, nameof(step), AdmissionCrmConsts.MaxNameLength);
		RepairReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		Status = AdmissionCrmActivationStatus.RepairRequired;
	}

	public void Complete(DateTime at)
	{
		if (!StudentId.HasValue) throw new BusinessException("AdmissionCrm:ActivationStudentMissing");
		FirstDayReadyAt = at.ToUniversalTime(); Status = AdmissionCrmActivationStatus.FirstDayReady;
	}

	public void RequestCancellation(string reason, Guid userId, DateTime at)
	{
		if (Status != AdmissionCrmActivationStatus.FirstDayReady)
			throw new BusinessException("AdmissionCrm:ActivationNotCancellable");
		CancellationReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		CancellationRequestedByUserId = userId;
		CancellationRequestedAt = at.ToUniversalTime();
		Status = AdmissionCrmActivationStatus.CancellationPending;
	}

	public void Cancel(Guid userId, DateTime at)
	{
		if (Status != AdmissionCrmActivationStatus.CancellationPending)
			throw new BusinessException("AdmissionCrm:ActivationCancellationNotPending");
		if (CancellationRequestedByUserId == userId)
			throw new BusinessException("AdmissionCrm:DistinctActivationCancellationApproverRequired");
		CancelledByUserId = userId;
		CancelledAt = at.ToUniversalTime();
		Status = AdmissionCrmActivationStatus.Cancelled;
	}
}
