using System;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmChannelSettingDto
{
	public AdmissionInquiryChannel Channel { get; set; }
	public bool IsEnabled { get; set; } = true;
	public Guid? DefaultInquirySourceId { get; set; }
	public Guid? DefaultFormTemplateId { get; set; }
	public Guid? DefaultOwnerUserId { get; set; }
	public Guid? DefaultAcknowledgementTemplateId { get; set; }
	public int DisplayOrder { get; set; }
}
