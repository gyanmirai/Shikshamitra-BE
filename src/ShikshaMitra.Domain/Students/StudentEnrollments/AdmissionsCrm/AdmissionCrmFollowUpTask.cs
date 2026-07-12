using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFollowUpTask : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public DateTime DueAt { get; private set; }
	public Guid? AssignedUserId { get; private set; }
	public AdmissionCrmTaskStatus Status { get; private set; }
	public string? Notes { get; private set; }
	public DateTime? CompletedAt { get; private set; }
	public string? CompletionNotes { get; private set; }

	protected AdmissionCrmFollowUpTask()
	{
	}

	public AdmissionCrmFollowUpTask(
		Guid id,
		Guid? tenantId,
		Guid leadId,
		Guid academicYearId,
		string title,
		DateTime dueAt,
		Guid? assignedUserId,
		string? notes)
		: base(id)
	{
		TenantId = tenantId;
		LeadId = leadId;
		AcademicYearId = academicYearId;
		Title = Check.NotNullOrWhiteSpace(
			title,
			nameof(title),
			maxLength: AdmissionCrmConsts.MaxDescriptionLength);
		DueAt = dueAt;
		AssignedUserId = assignedUserId;
		Status = AdmissionCrmTaskStatus.Pending;
		Notes = NormalizeOptional(notes, nameof(notes), AdmissionCrmConsts.MaxNotesLength);
	}

	public void Complete(DateTime completedAt, string? completionNotes)
	{
		if (Status != AdmissionCrmTaskStatus.Pending)
		{
			throw new BusinessException("AdmissionCrm:TaskNotPending")
				.WithData("TaskId", Id)
				.WithData("Status", Status);
		}

		Status = AdmissionCrmTaskStatus.Completed;
		CompletedAt = completedAt;
		CompletionNotes = NormalizeOptional(
			completionNotes,
			nameof(completionNotes),
			AdmissionCrmConsts.MaxNotesLength);
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
