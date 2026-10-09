using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public sealed record OnboardingDocumentPolicySnapshot(Guid StageId, AdmissionDocumentPolicy Policy);

[RemoteService(false)]
public class OnboardingRequirementSnapshotService(OnboardingConfigurationManager configuration,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	IRepository<AdmissionCycle, Guid> cycles,
	IRepository<AdmissionApplication, Guid> applications,
	AdmissionDocumentEvaluationService evaluator) : ITransientDependency
{
	public async Task<bool> CanDeferAsync(AdmissionApplication application)
	{
		var cycle = await cycles.GetAsync(application.AdmissionCycleId);
		var config = await configuration.GetAsync(cycle.AcademicYearId);
		return config != null && config.Profiles.Any(p => OnboardingRuleConfiguration.Ids(p, "offeringIds").Contains(application.AdmissionOfferingId));
	}
	public async Task CaptureAsync(StudentOnboardingCase item)
	{
		if (item.StudentEnrollmentId.HasValue || item.Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled) return;
		if (!item.ExtraProperties.ContainsKey("ReadinessRulesSnapshot"))
		{
			var config = await configuration.GetAsync(item.AcademicYearId);
			var profile = config?.Profiles.SingleOrDefault(p => OnboardingRuleConfiguration.Ids(p, "offeringIds").Contains(item.OfferingId));
            if (!profile.HasValue || profile.Value.ValueKind != JsonValueKind.Object) return;
            item.ExtraProperties["ReadinessRulesSnapshot"] = profile.Value.GetProperty("rules").GetRawText();
		}
		if (item.ExtraProperties.ContainsKey("DocumentPoliciesSnapshot")) return;
		var workflow = await workflows.FindAsync(w => w.ApplicationId == item.ApplicationId);
		var snapshots = new List<OnboardingDocumentPolicySnapshot>();
		if (workflow != null)
			foreach (var stage in await stages.GetListAsync(s => s.WorkflowInstanceId == workflow.Id && s.StageType == AdmissionWorkflowStageType.DocumentVerification && s.Status != AdmissionRuntimeStageStatus.Pending))
			{
				// Freeze the stored published policy, including legacy collection modes, instead of editor normalization.
                var policy = AdmissionDocumentPolicy.Read((await definitions.GetAsync(stage.DefinitionStageId)).ConfigurationJson);
				if (policy != null)
				{
					var evaluation = await evaluator.EvaluateAsync(await applications.GetAsync(item.ApplicationId), stage.Id, policy);
					if (evaluation.Requirements.Any(r => r.ConditionStatus == "undetermined"))
						throw new BusinessException(AdmissionDocumentErrorCodes.EvidenceRequired, "Complete the admission information needed to determine document requirements before transferring to Onboarding.");
					var applicable = evaluation.Requirements.Where(r => r.ConditionStatus != "notMatched")
						.Select(r => r.Requirement with { Requirement = r.IsRequired ? "required" : "optional", ConditionJson = null, StandardIds = null }).ToList();
					snapshots.Add(new(stage.Id, policy with { AllowWaivers = false, Requirements = applicable }));
				}
			}
		item.ExtraProperties["DocumentPoliciesSnapshot"] = JsonSerializer.Serialize(snapshots);
	}

	public static List<OnboardingDocumentPolicySnapshot> Read(StudentOnboardingCase item) =>
		item.ExtraProperties.TryGetValue("DocumentPoliciesSnapshot", out var json) && json is string text
			? JsonSerializer.Deserialize<List<OnboardingDocumentPolicySnapshot>>(text) ?? [] : [];

	public async Task<bool> DocumentsReadyAsync(StudentOnboardingCase item, AdmissionApplication application)
	{
		foreach (var snapshot in Read(item))
			if ((await evaluator.EvaluateAsync(application, snapshot.StageId, snapshot.Policy)).Blockers.Count != 0) return false;
		return true;
	}
}
