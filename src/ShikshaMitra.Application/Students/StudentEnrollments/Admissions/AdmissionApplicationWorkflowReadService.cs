using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationWorkflowReadService(
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflowRepository,
	IRepository<AdmissionApplicationStageInstance, Guid> stageRepository,
	IRepository<AdmissionWorkflowStageDefinition, Guid> stageDefinitionRepository,
	IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitionRepository,
	IRepository<AdmissionWorkflowTransitionHistory, Guid> historyRepository,
	AdmissionApplicationReadService applicationReadService)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionApplicationWorkflowDto> GetAsync(Guid applicationId)
	{
		var application = await applicationReadService.GetEntityAsync(applicationId);
		var workflow = await GetInstanceAsync(applicationId);
		var stages = await stageRepository.GetListAsync(stage => stage.WorkflowInstanceId == workflow.Id);
		var definitions = await stageDefinitionRepository.GetListAsync(stage => stage.WorkflowDefinitionId == workflow.WorkflowDefinitionId);
		var current = stages.SingleOrDefault(stage => stage.Id == application.CurrentStageInstanceId);
		var allTransitions = await transitionRepository.GetListAsync(item => item.WorkflowDefinitionId == workflow.WorkflowDefinitionId);
		var transitions = allTransitions.Where(transition => transition.FromStageId == current?.DefinitionStageId).ToList();
		var history = await historyRepository.GetListAsync(item => item.WorkflowInstanceId == workflow.Id);
		return new AdmissionApplicationWorkflowDto
		{
			WorkflowDefinitionId = application.WorkflowDefinitionId,
			Status = workflow.Status,
			StartedAt = workflow.StartedAt,
			CompletedAt = workflow.CompletedAt,
			CurrentStageInstanceId = application.CurrentStageInstanceId,
			NextRequiredAction = workflow.Status != AdmissionWorkflowInstanceStatus.Active || current == null
				? null : AdmissionWorkflowStageTypeRegistry.Find(current.StageType)?.NextRequiredAction,
			Stages = stages.OrderBy(stage => stage.DisplayOrder).Select(stage => new AdmissionApplicationStageDto
			{
				Id = stage.Id,
				DefinitionStageId = stage.DefinitionStageId,
				Code = stage.Code,
				Name = stage.Name,
				StageType = stage.StageType,
				DisplayOrder = stage.DisplayOrder,
				Status = stage.Status,
				AllowManualSkip = definitions.First(definition => definition.Id == stage.DefinitionStageId).AllowManualSkip,
				FormStageEnabled = stage.StageType == AdmissionWorkflowStageType.Form &&
					AdmissionFormStageAccessService.ReadPolicy(definitions.First(definition => definition.Id == stage.DefinitionStageId)) != null,
				PaymentStageEnabled = stage.StageType == AdmissionWorkflowStageType.Payment &&
					AdmissionPaymentAccessService.ReadPolicy(definitions.First(definition => definition.Id == stage.DefinitionStageId)) != null,
				OfferStageEnabled = stage.StageType == AdmissionWorkflowStageType.Offer &&
					AdmissionOfferAccessService.ReadPolicy(definitions.First(definition => definition.Id == stage.DefinitionStageId)) != null,
				SeatStageEnabled = stage.StageType == AdmissionWorkflowStageType.Seat &&
					AdmissionSeatAccessService.ReadPolicy(definitions.First(definition => definition.Id == stage.DefinitionStageId)) != null,
				AssessmentEnabled = stage.StageType == AdmissionWorkflowStageType.Assessment &&
					AdmissionAssessmentAccessService.ReadPolicy(definitions.First(definition => definition.Id == stage.DefinitionStageId)) != null,
				DocumentVerificationEnabled = stage.StageType == AdmissionWorkflowStageType.DocumentVerification &&
					AdmissionDocumentAccessService.ReadPolicy(definitions.First(definition => definition.Id == stage.DefinitionStageId)) != null,
				DocumentCollectionAvailable = workflow.Status == AdmissionWorkflowInstanceStatus.Active &&
					application.Status is AdmissionApplicationStatus.Draft or AdmissionApplicationStatus.Submitted &&
					current?.StageType == AdmissionWorkflowStageType.Form && current.Status == AdmissionRuntimeStageStatus.Active &&
					stage.StageType == AdmissionWorkflowStageType.DocumentVerification && stage.Status == AdmissionRuntimeStageStatus.Pending &&
					AdmissionDocumentAccessService.ReadPolicy(definitions.First(definition => definition.Id == stage.DefinitionStageId)) is { CollectionFormStageCode: not null } documentPolicy &&
					AdmissionDocumentCollectionRules.IsLinkedForm(definitions.First(item => item.Id == current.DefinitionStageId),
						definitions.First(item => item.Id == stage.DefinitionStageId), documentPolicy, allTransitions),
				EligibilityEnabled = stage.StageType == AdmissionWorkflowStageType.Eligibility &&
					AdmissionEligibilityAccessService.ReadPolicy(definitions.First(definition => definition.Id == stage.DefinitionStageId)) != null,
				ReviewEnabled = stage.StageType == AdmissionWorkflowStageType.Review &&
					AdmissionReviewAccessService.ReadPolicy(definitions.First(definition => definition.Id == stage.DefinitionStageId)) != null,
				EnteredAt = stage.EnteredAt,
				CompletedAt = stage.CompletedAt,
				CompletedBy = stage.CompletedBy,
				CompletionReason = stage.CompletionReason
			}).ToList(),
			AvailableTransitions = transitions.OrderBy(transition => transition.Priority).Select(transition =>
			{
				var target = stages.First(stage => stage.DefinitionStageId == transition.ToStageId);
				return new AdmissionWorkflowRuntimeTransitionDto
				{
					Id = transition.Id,
					ToStageDefinitionId = transition.ToStageId,
					ToStageInstanceId = target.Id,
					ToStageName = target.Name,
					Priority = transition.Priority,
					IsDefault = transition.IsDefault,
					ConditionRuleJson = transition.ConditionRuleJson
				};
			}).ToList(),
			History = history.OrderBy(item => item.OccurredAt).Select(item => new AdmissionWorkflowTransitionHistoryDto
			{
				Id = item.Id,
				FromStageInstanceId = item.FromStageInstanceId,
				ToStageInstanceId = item.ToStageInstanceId,
				TransitionDefinitionId = item.TransitionDefinitionId,
				Action = item.Action,
				Reason = item.Reason,
				ConditionEvaluationSnapshotJson = item.ConditionEvaluationSnapshotJson,
				OccurredAt = item.OccurredAt
			}).ToList()
		};
	}

	public async Task<AdmissionApplicationWorkflowInstance> GetInstanceAsync(Guid applicationId) =>
		await workflowRepository.FindAsync(instance => instance.ApplicationId == applicationId) ??
		throw new EntityNotFoundException(typeof(AdmissionApplicationWorkflowInstance), applicationId);
}
