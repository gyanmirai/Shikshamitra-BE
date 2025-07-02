using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment;
public class StudentSubmissionEntity : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }

	public Guid HomeworkAssignmentId { get; set; }
	public HomeworkAssignmentEntity HomeworkAssignment { get; set; }

	public Guid StudentId { get; set; }

	public DateTimeOffset? SubmissionDate { get; set; }

	public string? Feedback { get; set; }

	public SubmissionStatus Status { get; set; }

	public decimal? Marks { get; set; }

	public string? SubmittedDocumentUrl { get; set; }

	public Guid? GradedBy { get; set; }
	public DateTimeOffset? GradedDate { get; set; }
}
