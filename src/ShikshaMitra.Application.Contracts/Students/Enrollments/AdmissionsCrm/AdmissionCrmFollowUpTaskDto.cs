using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFollowUpTaskDto : EntityDto<Guid>
{
	public Guid LeadId { get; set; }
	public Guid AcademicYearId { get; set; }
	public string Title { get; set; } = string.Empty;
	public DateTime DueAt { get; set; }
	public Guid? AssignedUserId { get; set; }
	public AdmissionCrmTaskStatus Status { get; set; }
	public string? Notes { get; set; }
	public DateTime? CompletedAt { get; set; }
	public string? CompletionNotes { get; set; }
}
