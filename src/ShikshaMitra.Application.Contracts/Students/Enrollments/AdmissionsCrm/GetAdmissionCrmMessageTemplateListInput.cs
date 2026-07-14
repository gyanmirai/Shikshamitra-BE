using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class GetAdmissionCrmMessageTemplateListInput : PagedAndSortedResultRequestDto
{
	public string? FilterText { get; set; }
	public AdmissionCrmMessageChannel? Channel { get; set; }
	public AdmissionCrmMessageTemplateStatus? Status { get; set; }
	public bool? IsActive { get; set; }
}
