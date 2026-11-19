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
	IRepository<AdmissionApplication, Guid> applications,
	AdmissionDocumentEvaluationService evaluator) : ITransientDependency
{
	public Task<bool> CanDeferAsync(AdmissionApplication application) => Task.FromResult(true);

	public async Task<bool> CaptureAsync(StudentOnboardingCase item)
	{
		if (!AutomaticOnboardingReadinessRules.CanAdopt(item)) return false;
		return await CaptureAsync(item, await configuration.GetAsync(item.AcademicYearId));
	}

	public async Task<bool> CaptureAsync(StudentOnboardingCase item, OnboardingRuleConfiguration? settings)
	{
		if (!AutomaticOnboardingReadinessRules.CanAdopt(item)) return false;
		var rules = AutomaticOnboardingReadinessRules.Resolve(settings).GetRawText();
		var changed = !item.ExtraProperties.TryGetValue("ReadinessRulesSnapshot", out var current) ||
			current is not string saved || saved != rules ||
			!item.ExtraProperties.TryGetValue(AutomaticOnboardingReadinessRules.SnapshotVersionKey, out var version) ||
			version?.ToString() != AutomaticOnboardingReadinessRules.SnapshotVersion.ToString();
		// The application pins a published workflow version. Keep its document policy snapshot
		// and evidence intact when the school changes account preparation settings later.
		if (item.ExtraProperties.ContainsKey("DocumentPoliciesSnapshot"))
		{
			if (changed) ApplyReadiness(item, rules);
			return changed;
		}
		item.ExtraProperties["DocumentPoliciesSnapshot"] =
			await PrepareDocumentPoliciesAsync(await applications.GetAsync(item.ApplicationId));
		ApplyReadiness(item, rules);
		return true;
	}

	public async Task<string> PrepareDocumentPoliciesAsync(AdmissionApplication application)
	{
		var workflow = await workflows.FindAsync(w => w.ApplicationId == application.Id);
		var snapshots = new List<OnboardingDocumentPolicySnapshot>();
		if (workflow != null)
		{
			var visited = await stages.GetListAsync(stage => stage.WorkflowInstanceId == workflow.Id && stage.StageType == AdmissionWorkflowStageType.DocumentVerification && stage.Status != AdmissionRuntimeStageStatus.Pending);
			var definitionIds = visited.Select(stage => stage.DefinitionStageId).Distinct().ToArray();
			var definitionRows = definitionIds.Length == 0 ? new Dictionary<Guid, AdmissionWorkflowStageDefinition>() :
				(await definitions.GetListAsync(definition => Enumerable.Contains(definitionIds, definition.Id))).ToDictionary(definition => definition.Id);
			// Freeze the stored published policy, including legacy collection modes, instead of editor normalization.
			var plan = AdmissionDocumentEvaluationService.ReadPolicies(visited, definitionRows,
				definition => AdmissionDocumentPolicy.Read(definition.ConfigurationJson));
			using var evaluations = (await evaluator.EnumerateManyAsync(application, plan.Requests)).GetEnumerator();
			foreach (var request in plan.Requests)
			{
				evaluations.MoveNext();
				var evaluation = evaluations.Current;
				if (evaluation.Requirements.Any(requirement => requirement.ConditionStatus == "undetermined"))
					throw new BusinessException(AdmissionDocumentErrorCodes.EvidenceRequired, "Complete the admission information needed to determine document requirements before transferring to Onboarding.");
				var applicable = evaluation.Requirements.Where(requirement => requirement.ConditionStatus != "notMatched")
					.Select(requirement => requirement.Requirement with { Requirement = requirement.IsRequired ? "required" : "optional", ConditionJson = null, StandardIds = null }).ToList();
				snapshots.Add(new(request.StageId, request.Policy with { AllowWaivers = false, Requirements = applicable }));
			}
			plan.Error?.Throw();
		}
		return JsonSerializer.Serialize(snapshots);
	}

	private static void ApplyReadiness(StudentOnboardingCase item, string rules)
	{
		item.ExtraProperties["ReadinessRulesSnapshot"] = rules;
		item.ExtraProperties[AutomaticOnboardingReadinessRules.SnapshotVersionKey] = AutomaticOnboardingReadinessRules.SnapshotVersion;
	}

	public static List<OnboardingDocumentPolicySnapshot> Read(StudentOnboardingCase item) =>
		item.ExtraProperties.TryGetValue("DocumentPoliciesSnapshot", out var json) && json is string text
			? JsonSerializer.Deserialize<List<OnboardingDocumentPolicySnapshot>>(text) ?? [] : [];

	public async Task<bool> DocumentsReadyAsync(StudentOnboardingCase item, AdmissionApplication application)
	{
		var requests = new List<(Guid StageId, AdmissionDocumentPolicy Policy)>();
		System.Runtime.ExceptionServices.ExceptionDispatchInfo? rowError = null;
		foreach (var snapshot in Read(item))
		{
			try { requests.Add((snapshot.StageId, snapshot.Policy)); }
			catch (NullReferenceException exception)
			{
				// A later malformed row must not precede an earlier blocked requirement.
				rowError = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
				break;
			}
		}
		foreach (var evaluation in await evaluator.EnumerateManyAsync(application, requests))
			if (evaluation.Blockers.Count != 0) return false;
		rowError?.Throw();
		return true;
	}
}
