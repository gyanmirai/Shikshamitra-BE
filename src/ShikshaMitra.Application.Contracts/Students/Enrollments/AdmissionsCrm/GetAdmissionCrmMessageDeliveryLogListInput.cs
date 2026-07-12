using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class GetAdmissionCrmMessageDeliveryLogListInput : PagedAndSortedResultRequestDto
{
	public Guid? LeadId { get; set; }
	public Guid? TemplateId { get; set; }
	public AdmissionCrmMessageChannel? Channel { get; set; }
	public string? Status { get; set; }
	[MaxLength(AdmissionCrmConsts.MaxProviderMessageIdLength)]
	public string? ProviderMessageId { get; set; }
}
