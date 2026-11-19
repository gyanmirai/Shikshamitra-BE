using System;
using System.Linq;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class StudentJourneyAuditWriter(ICurrentUser user, ICurrentTenant tenant, IGuidGenerator ids) : ITransientDependency
{
	private string? ActorName => !user.Id.HasValue ? null :
		string.Join(" ", new[] { user.Name, user.SurName }.Where(value => !string.IsNullOrWhiteSpace(value))) is { Length: > 0 } name ? name : user.UserName;

	public AdmissionActivity CreateActivity(string activityType, string description, Guid? applicantId = null,
		Guid? applicationId = null, Guid? inquiryId = null, string? metadataJson = null)
	{
		var item = new AdmissionActivity(ids.Create(), tenant.Id, activityType, description, applicantId, applicationId, inquiryId, metadataJson);
		item.RecordHandler(user.Id, ActorName);
		return item;
	}

	public StudentOnboardingEvent CreateOnboardingEvent(Guid onboardingCaseId, string eventType, string description, string? metadataJson = null)
	{
		var item = new StudentOnboardingEvent(ids.Create(), tenant.Id, onboardingCaseId, eventType, description, metadataJson);
		item.RecordHandler(user.Id, ActorName);
		return item;
	}

	public AdmissionWorkflowTransitionHistory CreateTransition(Guid applicationId, Guid workflowInstanceId,
		Guid fromStageInstanceId, Guid? toStageInstanceId, Guid? transitionDefinitionId,
		AdmissionWorkflowTransitionAction action, string idempotencyKey, string? reason, string? conditionEvaluationSnapshotJson = null)
	{
		var item = new AdmissionWorkflowTransitionHistory(ids.Create(), tenant.Id, applicationId, workflowInstanceId,
			fromStageInstanceId, toStageInstanceId, transitionDefinitionId, action, idempotencyKey, reason, conditionEvaluationSnapshotJson);
		item.RecordHandler(user.Id, ActorName);
		return item;
	}
}
