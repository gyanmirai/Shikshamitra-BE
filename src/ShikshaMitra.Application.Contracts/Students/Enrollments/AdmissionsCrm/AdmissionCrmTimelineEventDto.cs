using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmTimelineEventDto : EntityDto<Guid>
{
	public Guid LeadId { get; set; }
	public Guid AcademicYearId { get; set; }
	public AdmissionCrmTimelineEventType EventType { get; set; }
	public string Title { get; set; } = string.Empty;
	public string? Details { get; set; }
	public Guid? RelatedEntityId { get; set; }
	public Guid? ActorUserId { get; set; }
	public DateTimeOffset OccurredAt { get; set; }
}
