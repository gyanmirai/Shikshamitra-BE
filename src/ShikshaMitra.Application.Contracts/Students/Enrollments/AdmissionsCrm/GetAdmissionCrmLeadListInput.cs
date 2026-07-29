using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class GetAdmissionCrmLeadListInput : PagedAndSortedResultRequestDto
{
	public string? FilterText { get; set; }
	public string? ApplicantSearch { get; set; }
	public string? RegistrationFormNumber { get; set; }
	public DateTime? LastActivityFrom { get; set; }
	public DateTime? LastActivityUntil { get; set; }
	public AdmissionInquiryStage? Stage { get; set; }
	public AdmissionCrmPipelineColumnKey? PipelineColumn { get; set; }
	public AdmissionInquiryChannel? Channel { get; set; }
	public Guid? InquirySourceId { get; set; }
	public Guid? InquiryTypeId { get; set; }
	public string? CampaignName { get; set; }
	public Guid? OwnerUserId { get; set; }
	public AdmissionCrmLeadPriority? Priority { get; set; }
	public AdmissionCrmRouteType? IntendedRouteType { get; set; }
	public string? DesiredClassName { get; set; }
	public Guid? AcademicMediumId { get; set; }
}
