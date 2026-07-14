using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEntranceExamDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public string ApplicantName { get; set; } = string.Empty;
	public Guid EntranceExamSlotId { get; set; }
	public string? SlotNameSnapshot { get; set; }
	public DateTimeOffset ExamStartTime { get; set; }
	public DateTimeOffset ExamEndTime { get; set; }
	public int ExamRoomNo { get; set; }
	public bool CapacityOverrideUsed { get; set; }
	public string? CapacityOverrideReason { get; set; }
	public DateTime ScheduledAt { get; set; }
	public AdmissionCrmExamAttendanceStatus AttendanceStatus { get; set; }
	public DateTime? AttendanceMarkedAt { get; set; }
	public double? TotalMarks { get; set; }
	public double? PassingMarks { get; set; }
	public double? ObtainedMarks { get; set; }
	public double? Percentage { get; set; }
	public EntranceExamStatusEnum ExamStatus { get; set; }
	public DateTime? ResultRecordedAt { get; set; }
	public int? MeritRank { get; set; }
	public AdmissionCrmEligibilityDecision EligibilityDecision { get; set; }
	public string? EligibilityDecisionReason { get; set; }
	public bool EligibilityOverrideUsed { get; set; }
	public string? EligibilityOverrideReason { get; set; }
	public DateTime? EligibilityDecidedAt { get; set; }
}
