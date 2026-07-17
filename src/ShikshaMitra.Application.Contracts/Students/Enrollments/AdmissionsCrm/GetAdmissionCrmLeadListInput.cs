using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class GetAdmissionCrmLeadListInput : PagedAndSortedResultRequestDto
{
	public string? FilterText { get; set; }
	public AdmissionInquiryStage? Stage { get; set; }
	public AdmissionInquiryChannel? Channel { get; set; }
	public Guid? InquirySourceId { get; set; }
	public string? CampaignName { get; set; }
	public Guid? OwnerUserId { get; set; }
	public AdmissionCrmLeadPriority? Priority { get; set; }
	public AdmissionCrmRouteType? IntendedRouteType { get; set; }
	public string? DesiredClassName { get; set; }
	public Guid? AcademicMediumId { get; set; }
}
