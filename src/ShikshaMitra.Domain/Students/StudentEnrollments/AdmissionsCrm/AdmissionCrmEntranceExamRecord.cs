using System;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEntranceExamRecord : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid EntranceExamSlotId { get; private set; }
	public string? SlotNameSnapshot { get; private set; }
	public DateTimeOffset ExamStartTime { get; private set; }
	public DateTimeOffset ExamEndTime { get; private set; }
	public int ExamRoomNo { get; private set; }
	public bool CapacityOverrideUsed { get; private set; }
	public string? CapacityOverrideReason { get; private set; }
	public DateTime ScheduledAt { get; private set; }
	public AdmissionCrmExamAttendanceStatus AttendanceStatus { get; private set; }
	public DateTime? AttendanceMarkedAt { get; private set; }
	public double? TotalMarks { get; private set; }
	public double? PassingMarks { get; private set; }
	public double? ObtainedMarks { get; private set; }
	public double? Percentage { get; private set; }
	public EntranceExamStatusEnum ExamStatus { get; private set; }
	public DateTime? ResultRecordedAt { get; private set; }
	public int? MeritRank { get; private set; }
	public AdmissionCrmEligibilityDecision EligibilityDecision { get; private set; }
	public string? EligibilityDecisionReason { get; private set; }
	public bool EligibilityOverrideUsed { get; private set; }
	public string? EligibilityOverrideReason { get; private set; }
	public DateTime? EligibilityDecidedAt { get; private set; }

	protected AdmissionCrmEntranceExamRecord()
	{
	}

	public AdmissionCrmEntranceExamRecord(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid leadId,
		AdmissionCrmExamSlot slot,
		bool capacityOverrideUsed,
		string? capacityOverrideReason,
		DateTime scheduledAt)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		ScheduledAt = scheduledAt;
		AttendanceStatus = AdmissionCrmExamAttendanceStatus.NotMarked;
		ExamStatus = EntranceExamStatusEnum.Pending;
		EligibilityDecision = AdmissionCrmEligibilityDecision.Pending;
		EligibilityDecisionReason = null;
		EligibilityOverrideUsed = false;
		EligibilityOverrideReason = null;
		EligibilityDecidedAt = null;
		ApplySlot(slot, capacityOverrideUsed, capacityOverrideReason);
	}

	public void Reschedule(
		AdmissionCrmExamSlot slot,
		bool capacityOverrideUsed,
		string? capacityOverrideReason,
		DateTime scheduledAt)
	{
		if (ResultRecordedAt.HasValue)
		{
			throw new BusinessException("AdmissionCrm:CannotRescheduleAfterResult")
				.WithData("ExamRecordId", Id);
		}

		ApplySlot(slot, capacityOverrideUsed, capacityOverrideReason);
		ScheduledAt = scheduledAt;
		AttendanceStatus = AdmissionCrmExamAttendanceStatus.NotMarked;
		AttendanceMarkedAt = null;
		ExamStatus = EntranceExamStatusEnum.Pending;
		EligibilityDecision = AdmissionCrmEligibilityDecision.Pending;
		EligibilityDecisionReason = null;
		EligibilityOverrideUsed = false;
		EligibilityOverrideReason = null;
		EligibilityDecidedAt = null;
	}

	public void MarkAttendance(AdmissionCrmExamAttendanceStatus attendanceStatus, DateTime markedAt)
	{
		if (!Enum.IsDefined(attendanceStatus) ||
		    attendanceStatus == AdmissionCrmExamAttendanceStatus.NotMarked)
		{
			throw new BusinessException("AdmissionCrm:InvalidAttendanceStatus");
		}

		if (ResultRecordedAt.HasValue)
		{
			throw new BusinessException("AdmissionCrm:CannotChangeAttendanceAfterResult")
				.WithData("ExamRecordId", Id);
		}

		AttendanceStatus = attendanceStatus;
		AttendanceMarkedAt = markedAt;

		if (attendanceStatus is AdmissionCrmExamAttendanceStatus.Absent or
		    AdmissionCrmExamAttendanceStatus.NoShow or
		    AdmissionCrmExamAttendanceStatus.Excused)
		{
			ExamStatus = EntranceExamStatusEnum.Absent;
			EligibilityDecision = AdmissionCrmEligibilityDecision.Pending;
			EligibilityDecisionReason = null;
			EligibilityOverrideUsed = false;
			EligibilityOverrideReason = null;
			EligibilityDecidedAt = null;
		}
		else
		{
			ExamStatus = EntranceExamStatusEnum.Pending;
			EligibilityDecision = AdmissionCrmEligibilityDecision.Pending;
			EligibilityDecisionReason = null;
			EligibilityOverrideUsed = false;
			EligibilityOverrideReason = null;
			EligibilityDecidedAt = null;
		}
	}

	public void RecordResult(
		double totalMarks,
		double passingMarks,
		double obtainedMarks,
		int? meritRank,
		DateTime recordedAt)
	{
		if (AttendanceStatus is AdmissionCrmExamAttendanceStatus.Absent or
		    AdmissionCrmExamAttendanceStatus.NoShow or
		    AdmissionCrmExamAttendanceStatus.Excused)
		{
			throw new BusinessException("AdmissionCrm:CannotRecordResultForAbsentApplicant")
				.WithData("ExamRecordId", Id);
		}

		ValidateMarks(totalMarks, passingMarks, obtainedMarks);

		if (AttendanceStatus == AdmissionCrmExamAttendanceStatus.NotMarked)
		{
			AttendanceStatus = AdmissionCrmExamAttendanceStatus.Present;
			AttendanceMarkedAt = recordedAt;
		}

		TotalMarks = totalMarks;
		PassingMarks = passingMarks;
		ObtainedMarks = obtainedMarks;
		Percentage = obtainedMarks / totalMarks * 100;
		ExamStatus = obtainedMarks >= passingMarks
			? EntranceExamStatusEnum.Pass
			: EntranceExamStatusEnum.Fail;
		ResultRecordedAt = recordedAt;
		MeritRank = ValidateMeritRank(meritRank);
		EligibilityDecision = ExamStatus == EntranceExamStatusEnum.Pass
			? AdmissionCrmEligibilityDecision.Eligible
			: AdmissionCrmEligibilityDecision.Rejected;
		EligibilityDecisionReason = ExamStatus == EntranceExamStatusEnum.Pass
			? "Applicant passed the entrance exam."
			: "Applicant did not meet the entrance exam passing marks.";
		EligibilityDecidedAt = recordedAt;
		EligibilityOverrideUsed = false;
		EligibilityOverrideReason = null;
	}

	public void SetEligibilityDecision(
		AdmissionCrmEligibilityDecision decision,
		string reason,
		bool overrideUsed,
		string? overrideReason,
		int? meritRank,
		DateTime decidedAt)
	{
		if (decision == AdmissionCrmEligibilityDecision.Pending)
		{
			throw new BusinessException("AdmissionCrm:EligibilityDecisionRequired");
		}

		if (overrideUsed && string.IsNullOrWhiteSpace(overrideReason))
		{
			throw new BusinessException("AdmissionCrm:EligibilityOverrideReasonRequired");
		}

		if (!overrideUsed &&
		    ExamStatus != EntranceExamStatusEnum.Pass &&
		    decision is AdmissionCrmEligibilityDecision.Eligible or AdmissionCrmEligibilityDecision.Waitlisted)
		{
			throw new BusinessException("AdmissionCrm:EligibilityOverrideRequired");
		}

		EligibilityDecision = decision;
		EligibilityDecisionReason = Check.NotNullOrWhiteSpace(
			reason,
			nameof(reason),
			maxLength: AdmissionCrmConsts.MaxNotesLength);
		EligibilityOverrideUsed = overrideUsed;
		EligibilityOverrideReason = NormalizeOptional(
			overrideReason,
			nameof(overrideReason),
			AdmissionCrmConsts.MaxNotesLength);
		MeritRank = ValidateMeritRank(meritRank);
		EligibilityDecidedAt = decidedAt;
	}

	public void SetMeritRank(int? meritRank)
	{
		MeritRank = ValidateMeritRank(meritRank);
	}

	private void ApplySlot(
		AdmissionCrmExamSlot slot,
		bool capacityOverrideUsed,
		string? capacityOverrideReason)
	{
		if (slot.AcademicYearId != AcademicYearId)
		{
			throw new BusinessException("AdmissionCrm:ExamSlotAcademicYearMismatch")
				.WithData("AcademicYearId", AcademicYearId)
				.WithData("EntranceExamSlotId", slot.Id);
		}

		if (capacityOverrideUsed && string.IsNullOrWhiteSpace(capacityOverrideReason))
		{
			throw new BusinessException("AdmissionCrm:CapacityOverrideReasonRequired");
		}

		EntranceExamSlotId = slot.Id;
		SlotNameSnapshot = NormalizeOptional(
			slot.SlotName,
			nameof(slot.SlotName),
			AdmissionCrmConsts.MaxNameLength);
		ExamStartTime = slot.StartDate;
		ExamEndTime = slot.EndDate;
		ExamRoomNo = slot.RoomNo;
		CapacityOverrideUsed = capacityOverrideUsed;
		CapacityOverrideReason = NormalizeOptional(
			capacityOverrideReason,
			nameof(capacityOverrideReason),
			AdmissionCrmConsts.MaxNotesLength);
	}

	private static void ValidateMarks(double totalMarks, double passingMarks, double obtainedMarks)
	{
		if (totalMarks <= 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidExamTotalMarks");
		}

		if (passingMarks < 0 || passingMarks > totalMarks)
		{
			throw new BusinessException("AdmissionCrm:InvalidExamPassingMarks");
		}

		if (obtainedMarks < 0 || obtainedMarks > totalMarks)
		{
			throw new BusinessException("AdmissionCrm:InvalidExamObtainedMarks");
		}
	}

	private static int? ValidateMeritRank(int? meritRank)
	{
		if (meritRank.HasValue && meritRank.Value <= 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidMeritRank")
				.WithData("MeritRank", meritRank.Value);
		}

		return meritRank;
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
