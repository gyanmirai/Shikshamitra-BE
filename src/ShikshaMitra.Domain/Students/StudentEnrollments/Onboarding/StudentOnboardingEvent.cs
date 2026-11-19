using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class StudentOnboardingEvent : CreationAuditedEntity<Guid>, IMultiTenant
{
	public void RecordHandler(Guid? actorId, string? actorName)
	{
		CreatorId = actorId;
		MetadataJson = ShikshaMitra.Students.StudentEnrollments.Admissions.StudentJourneyActorMetadata.Capture(MetadataJson, actorId, actorName);
	}
	public Guid? TenantId { get; private set; }
	public Guid OnboardingCaseId { get; private set; }
	public string EventType { get; private set; } = string.Empty;
	public string Description { get; private set; } = string.Empty;
	public string? MetadataJson { get; private set; }

	protected StudentOnboardingEvent() { }

	public StudentOnboardingEvent(Guid id, Guid? tenantId, Guid onboardingCaseId,
		string eventType, string description, string? metadataJson = null) : base(id)
	{
		TenantId = tenantId;
		OnboardingCaseId = Check.NotDefaultOrNull<Guid>(onboardingCaseId, nameof(onboardingCaseId));
		EventType = Check.NotNullOrWhiteSpace(eventType, nameof(eventType), 80).Trim();
		Description = Check.NotNullOrWhiteSpace(description, nameof(description), 500).Trim();
		MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson;
	}
}
