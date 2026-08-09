using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEligibilityHandoffDto
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public Guid ExamRecordId { get; set; }
	public int ApplicantNumber { get; set; }
	public bool CanStartAdmission { get; set; }
	public bool RequiresManualReview { get; set; }
	public List<string> MissingAdmissionFields { get; set; } = new();
	public string ApplicantName { get; set; } = string.Empty;
	public DateTimeOffset? DateOfBirth { get; set; }
	public string? ParentGuardianName { get; set; }
	public string? PrimaryContactNo { get; set; }
	public string? Email { get; set; }
	public string AdmissionIntent { get; set; } = string.Empty;
	public Guid? TargetStandardId { get; set; }
	public string? DesiredClassName { get; set; }
	public Guid? InquirySourceId { get; set; }
	public string? InquirySourceSnapshot { get; set; }
	public AdmissionInquiryStage LeadStage { get; set; }
	public AdmissionCrmExamAttendanceStatus AttendanceStatus { get; set; }
	public EntranceExamStatusEnum ExamStatus { get; set; }
	public double? TotalMarks { get; set; }
	public double? PassingMarks { get; set; }
	public double? ObtainedMarks { get; set; }
	public double? Percentage { get; set; }
	public int? MeritRank { get; set; }
	public AdmissionCrmEligibilityDecision EligibilityDecision { get; set; }
	public string? EligibilityDecisionReason { get; set; }
	public bool EligibilityOverrideUsed { get; set; }
	public string? EligibilityOverrideReason { get; set; }
	public DateTime? ResultRecordedAt { get; set; }
	public DateTime? EligibilityDecidedAt { get; set; }
}
