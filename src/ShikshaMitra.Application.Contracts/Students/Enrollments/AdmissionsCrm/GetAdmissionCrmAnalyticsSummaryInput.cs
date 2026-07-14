using System;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class GetAdmissionCrmAnalyticsSummaryInput
{
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public AdmissionInquiryChannel? Channel { get; set; }
	public Guid? InquirySourceId { get; set; }
	public string? CampaignName { get; set; }
	public Guid? OwnerUserId { get; set; }
	public AdmissionInquiryStage? Stage { get; set; }
	public Guid? TargetStandardId { get; set; }
	public int? MinimumLeadScore { get; set; }
	public int? MaximumLeadScore { get; set; }
}
