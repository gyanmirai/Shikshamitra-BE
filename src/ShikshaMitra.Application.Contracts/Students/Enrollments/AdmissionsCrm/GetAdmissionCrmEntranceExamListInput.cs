using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class GetAdmissionCrmEntranceExamListInput : PagedAndSortedResultRequestDto
{
	public string? FilterText { get; set; }
	public AdmissionCrmExamAttendanceStatus? AttendanceStatus { get; set; }
	public EntranceExamStatusEnum? ExamStatus { get; set; }
	public AdmissionCrmEligibilityDecision? EligibilityDecision { get; set; }
}
