using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmTimelineEvent : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public AdmissionCrmTimelineEventType EventType { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public string? Details { get; private set; }
	public Guid? RelatedEntityId { get; private set; }
	public Guid? ActorUserId { get; private set; }
	public DateTimeOffset OccurredAt { get; private set; }
	public DateTime OccurredAtUtc { get; private set; }

	protected AdmissionCrmTimelineEvent()
	{
	}

	public AdmissionCrmTimelineEvent(
		Guid id,
		Guid? tenantId,
		Guid leadId,
		Guid academicYearId,
		AdmissionCrmTimelineEventType eventType,
		string title,
		string? details,
		Guid? relatedEntityId,
		Guid? actorUserId,
		DateTimeOffset occurredAt)
		: base(id)
	{
		TenantId = tenantId;
		LeadId = leadId;
		AcademicYearId = academicYearId;
		EventType = eventType;
		Title = Check.NotNullOrWhiteSpace(
			title,
			nameof(title),
			maxLength: AdmissionCrmConsts.MaxTimelineTitleLength);
		Details = NormalizeOptional(details, nameof(details), AdmissionCrmConsts.MaxTimelineDetailsLength);
		RelatedEntityId = relatedEntityId;
		ActorUserId = actorUserId;
		OccurredAt = occurredAt;
		OccurredAtUtc = occurredAt.UtcDateTime;
	}

	private static string? NormalizeOptional(string? value, string name, int maxLength)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: Check.Length(value.Trim(), name, maxLength);
	}
}
