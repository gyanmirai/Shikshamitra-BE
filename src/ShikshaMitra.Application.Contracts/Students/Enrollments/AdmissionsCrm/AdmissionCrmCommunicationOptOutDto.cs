using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCommunicationOptOutDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid? LeadId { get; set; }
	public AdmissionCrmMessageChannel Channel { get; set; }
	public string ContactValue { get; set; } = string.Empty;
	public string NormalizedContactValue { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public string Reason { get; set; } = string.Empty;
	public DateTime OptedOutAt { get; set; }
	public DateTime? OptedInAt { get; set; }
	public string? OptInReason { get; set; }
}
