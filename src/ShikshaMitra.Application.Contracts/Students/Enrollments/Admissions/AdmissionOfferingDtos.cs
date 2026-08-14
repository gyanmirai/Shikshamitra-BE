using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionOfferingDto : AuditedEntityDto<Guid>
{
	public Guid AdmissionCycleId { get; set; }
	public Guid? CampusId { get; set; }
	public Guid BoardId { get; set; }
	public Guid MediumId { get; set; }
	public Guid StageId { get; set; }
	public Guid StandardId { get; set; }
	public string Code { get; set; } = string.Empty;
	public DateTime OpenDate { get; set; }
	public DateTime CloseDate { get; set; }
	public bool IsActive { get; set; }
	public Guid? WorkflowDefinitionId { get; set; }
	public Guid? InquiryFormDefinitionId { get; set; }
	public Guid? ApplicationFormDefinitionId { get; set; }
}

public class CreateUpdateAdmissionOfferingDto
{
	public Guid? CampusId { get; set; }
	public Guid BoardId { get; set; }
	public Guid MediumId { get; set; }
	public Guid StageId { get; set; }
	public Guid StandardId { get; set; }
	public string Code { get; set; } = string.Empty;
	public DateTime OpenDate { get; set; }
	public DateTime CloseDate { get; set; }
	public bool IsActive { get; set; } = true;
	public Guid? WorkflowDefinitionId { get; set; }
	public Guid? InquiryFormDefinitionId { get; set; }
	public Guid? ApplicationFormDefinitionId { get; set; }
}

public class AdmissionOfferingListInput : PagedAndSortedResultRequestDto
{
	public Guid? AdmissionCycleId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? StageId { get; set; }
	public Guid? StandardId { get; set; }
	public bool? IsActive { get; set; }
	public string? Search { get; set; }
}
