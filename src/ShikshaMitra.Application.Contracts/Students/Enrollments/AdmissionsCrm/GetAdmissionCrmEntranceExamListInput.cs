using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class GetAdmissionCrmEntranceExamListInput : PagedAndSortedResultRequestDto
{
	public string? FilterText { get; set; }
	public string? ApplicantSearch { get; set; }
	public DateTime? ExamStartFrom { get; set; }
	public DateTime? ExamStartUntil { get; set; }
	public AdmissionCrmExamAttendanceStatus? AttendanceStatus { get; set; }
	public EntranceExamStatusEnum? ExamStatus { get; set; }
	public AdmissionCrmEligibilityDecision? EligibilityDecision { get; set; }
}
