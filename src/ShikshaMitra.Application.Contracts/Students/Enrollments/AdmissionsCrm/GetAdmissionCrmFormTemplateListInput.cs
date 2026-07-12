using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class GetAdmissionCrmFormTemplateListInput : PagedAndSortedResultRequestDto
{
	public string? FilterText { get; set; }
	public bool? IsActive { get; set; }
	public bool? IsPublic { get; set; }
	public AdmissionInquiryChannel? Channel { get; set; }
}
