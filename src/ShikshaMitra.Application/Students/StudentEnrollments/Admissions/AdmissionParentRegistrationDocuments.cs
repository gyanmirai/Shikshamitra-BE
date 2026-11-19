using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionParentRegistrationDocuments(AdmissionParentRegistrationData data, AdmissionDocumentEvaluationService evaluator,
	AdmissionWorkflowRuntimeValueService runtimeValues, AdmissionDocumentAccessService documentAccess)
	: ApplicationService, ITransientDependency
{
	internal static bool HasParentEvidence(AdmissionDocumentRequirementEvaluation item) => item.Status == "waived" ||
		item.Status != "rejected" && item.Latest != null && (item.Latest.BlobName != null || item.Status == "accepted" &&
		AdmissionDocumentRules.HasCollectionEvidence(item.Latest, item.Requirement.CollectionMode));
	internal sealed record Item(AdmissionDocumentContext Context, AdmissionDocumentRequirementEvaluation Evaluation, AdmissionParentDocumentDto Dto);
	internal async Task<List<Item>> ReadAsync(AdmissionApplication application, AdmissionParentRegistration? grant = null)
	{
		var result = new List<Item>();
		if (!AdmissionParentRegistrationData.IsOpen(application) || !application.CurrentStageInstanceId.HasValue) return result;
		var current = await data.Repo<AdmissionApplicationStageInstance>().FindAsync(application.CurrentStageInstanceId.Value);
		if (current == null) return result;
		var workflow = await data.Repo<AdmissionApplicationWorkflowInstance>().GetAsync(current.WorkflowInstanceId);
		if (workflow.ApplicationId != application.Id || workflow.Status != AdmissionWorkflowInstanceStatus.Active) return result;
		var stages = await data.Repo<AdmissionApplicationStageInstance>().GetListAsync(row => row.WorkflowInstanceId == workflow.Id);
		var definitions = (await data.Repo<AdmissionWorkflowStageDefinition>().GetListAsync(row => row.WorkflowDefinitionId == application.WorkflowDefinitionId)).ToDictionary(row => row.Id);
		var transitions = await data.Repo<AdmissionWorkflowTransitionDefinition>().GetListAsync(row => row.WorkflowDefinitionId == application.WorkflowDefinitionId);
		var facts = await runtimeValues.ReadAsync(application, protectManagedFacts: true);
		var route = DefiniteRoute(current.DefinitionStageId, transitions, facts);
		var selections = grant == null ? [] : JsonSerializer.Deserialize<List<AdmissionParentDocumentSelection>>(grant.DocumentsJson)!;
		foreach (var stage in stages.Where(stage => route.Contains(stage.DefinitionStageId) && stage.StageType == AdmissionWorkflowStageType.DocumentVerification &&
			stage.Status is AdmissionRuntimeStageStatus.Pending or AdmissionRuntimeStageStatus.Active))
		{
			var definition = definitions[stage.DefinitionStageId];
			var policy = AdmissionDocumentAccessService.ReadPolicy(definition);
			if (policy == null) continue;
			var context = new AdmissionDocumentContext(application, stage, definition, policy, workflow.Status);
			foreach (var item in (await evaluator.EvaluateAsync(application, stage.Id, policy)).Requirements.Where(item =>
				item.ConditionStatus is not ("notMatched" or "undetermined") && item.Requirement.CollectionMode != "original"))
			{
				var selection = selections.SingleOrDefault(selection => selection.StageInstanceId == stage.Id && selection.RequirementCode == item.Requirement.Code);
				if (grant != null && selection == null) continue;
				var explicitlyRequested = grant?.Purpose == "correction" && selection != null;
				var awaitingReplacement = explicitlyRequested && (item.Latest?.VersionNumber ?? 0) <= selection!.BaselineVersionNumber;
				var mapped = await documentAccess.ValidateMappingAsync(item.Requirement);
				var extensions = item.Requirement.AllowedExtensions.Where(extension => mapped == null || mapped.GetAcceptedFormats()
					.Any(format => format.ToString().Equals(extension, StringComparison.OrdinalIgnoreCase))).ToArray();
				var dto = new AdmissionParentDocumentDto { StageInstanceId = stage.Id, Code = item.Requirement.Code, Name = item.Requirement.Name,
					Description = item.Requirement.Description, IsRequired = item.IsRequired || explicitlyRequested, Status = awaitingReplacement ? "missing" : item.Status, AllowedExtensions = extensions,
					MaxFileSizeMb = Math.Min(item.Requirement.MaxFileSizeMb, mapped?.MaxSizeMB ?? item.Requirement.MaxFileSizeMb),
					CanUpload = grant == null || grant.AllowsDocuments && grant.CanWrite(Clock.Now) && (item.Status is not ("accepted" or "waived") || explicitlyRequested),
					Evidence = item.Evidence.Where(row => row.BlobName != null).Select(row => new AdmissionParentFileDto { Id = row.Id,
						VersionNumber = row.VersionNumber, FileName = row.FileName, ContentType = row.ContentType, Size = row.Size,
						SubmittedAt = row.SubmittedAt, CanDownload = true, Status = AdmissionDocumentRules.Status(row, item.Reviews) }).ToList() };
				result.Add(new(context, item, dto));
			}
		}
		return result;
	}
	internal static HashSet<Guid> DefiniteRoute(Guid current, List<AdmissionWorkflowTransitionDefinition> transitions, IReadOnlyDictionary<string, string> facts)
	{
		var result = new HashSet<Guid>();
		var evaluator = new AdmissionWorkflowConditionEvaluator();
		while (result.Add(current))
		{
			var outgoing = transitions.Where(row => row.FromStageId == current).OrderBy(row => row.Priority).ToList();
			AdmissionWorkflowTransitionDefinition? selected = null;
			foreach (var link in outgoing.Where(row => !row.IsDefault))
			{
				if (!string.IsNullOrWhiteSpace(link.ConditionRuleJson))
				{
					using var condition = JsonDocument.Parse(link.ConditionRuleJson);
					if (!Known(condition.RootElement, facts)) return result;
					if (!evaluator.Evaluate(link.ConditionRuleJson, facts)) continue;
				}
				selected = link; break;
			}
			selected ??= outgoing.FirstOrDefault(row => row.IsDefault);
			if (selected == null) return result;
			current = selected.ToStageId;
		}
		return result;
	}
	private static bool IsUnsettledFact(string field) => field.StartsWith("assessment.", StringComparison.OrdinalIgnoreCase) || field.StartsWith("documents.", StringComparison.OrdinalIgnoreCase) || field == "application.status";
	private static bool Known(JsonElement node, IReadOnlyDictionary<string, string> facts)
	{
		if (node.ValueKind != JsonValueKind.Object) return false;
		if (node.TryGetProperty("conditions", out var children)) return children.EnumerateArray().All(child => Known(child, facts));
		return node.TryGetProperty("field", out var field) && facts.ContainsKey(field.GetString() ?? "") && !IsUnsettledFact(field.GetString() ?? "");
	}
}
