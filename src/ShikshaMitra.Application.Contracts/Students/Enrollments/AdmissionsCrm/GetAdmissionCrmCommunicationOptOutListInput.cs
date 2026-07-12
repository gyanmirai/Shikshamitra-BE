using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class GetAdmissionCrmCommunicationOptOutListInput : PagedAndSortedResultRequestDto
{
	public Guid? LeadId { get; set; }
	public AdmissionCrmMessageChannel? Channel { get; set; }
	public bool? IsActive { get; set; }
	public string? ContactSearch { get; set; }
}
