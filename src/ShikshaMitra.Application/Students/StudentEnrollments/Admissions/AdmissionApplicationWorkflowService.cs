using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionApplicationWorkflowService(
	IRepository<AdmissionApplication, Guid> applicationRepository,
	IRepository<AdmissionApplicationStageInstance, Guid> stageRepository,
	IRepository<AdmissionWorkflowStageDefinition, Guid> stageDefinitionRepository,
	IRepository<AdmissionWorkflowTransitionDefinition, Guid> transitionRepository,
	IRepository<AdmissionWorkflowTransitionHistory, Guid> historyRepository,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflowRepository,
	IRepository<AdmissionFormInstance, Guid> formRepository,
	IRepository<AdmissionFormFieldValue, Guid> valueRepository,
	IRepository<AdmissionActivity, Guid> activityRepository,
	AdmissionApplicationReadService applicationReadService,
	AdmissionApplicationWorkflowReadService workflowReadService,
	AdmissionWorkflowTransitionSelector transitionSelector,
	AdmissionWorkflowStageHandlerRegistry stageHandlerRegistry)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionApplicationWorkflowDto> AdvanceAsync(Guid id, AdvanceAdmissionWorkflowDto input)
	{
		var application = await applicationReadService.GetEntityAsync(id);
		if (string.IsNullOrWhiteSpace(input.IdempotencyKey))
			throw new BusinessException(AdmissionApplicationErrorCodes.IdempotencyKeyRequired);
		var workflow = await workflowReadService.GetInstanceAsync(id);
		var existing = await historyRepository.FindAsync(history =>
			history.WorkflowInstanceId == workflow.Id && history.IdempotencyKey == input.IdempotencyKey.Trim());
		if (existing != null) return await workflowReadService.GetAsync(id);
		if (!string.IsNullOrWhiteSpace(input.ConcurrencyStamp) &&
			!string.Equals(input.ConcurrencyStamp, application.ConcurrencyStamp, StringComparison.Ordinal))
			throw new BusinessException(AdmissionApplicationErrorCodes.ConcurrencyConflict);
		await AdvanceInternalAsync(application, workflow, input);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await workflowReadService.GetAsync(id);
	}

	public async Task AdvanceAfterSubmitAsync(AdmissionApplication application)
	{
		if (!application.CurrentStageInstanceId.HasValue) return;
		var workflow = await workflowRepository.FindAsync(instance => instance.ApplicationId == application.Id);
		if (workflow == null) return;
		await AdvanceInternalAsync(application, workflow, new AdvanceAdmissionWorkflowDto
		{
			ExpectedCurrentStageInstanceId = application.CurrentStageInstanceId.Value,
			IdempotencyKey = $"submit:{application.Id:N}"
		});
	}

	private async Task AdvanceInternalAsync(
		AdmissionApplication application,
		AdmissionApplicationWorkflowInstance workflow,
		AdvanceAdmissionWorkflowDto input)
	{
		if (workflow.Status != AdmissionWorkflowInstanceStatus.Active)
			throw new BusinessException(AdmissionApplicationErrorCodes.WorkflowAlreadyCompleted);
		if (application.CurrentStageInstanceId != input.ExpectedCurrentStageInstanceId)
			throw new BusinessException(AdmissionApplicationErrorCodes.InvalidCurrentStage);
		var stages = await stageRepository.GetListAsync(stage => stage.WorkflowInstanceId == workflow.Id);
		var current = stages.SingleOrDefault(stage => stage.Id == application.CurrentStageInstanceId) ??
			throw new BusinessException(AdmissionApplicationErrorCodes.InvalidCurrentStage);
		var definition = await stageDefinitionRepository.FindAsync(current.DefinitionStageId) ??
			throw new EntityNotFoundException(typeof(AdmissionWorkflowStageDefinition), current.DefinitionStageId);
		if (input.SkipCurrentStage && !input.AdminOverride && !definition.AllowManualSkip)
			throw new BusinessException(AdmissionApplicationErrorCodes.ManualSkipNotAllowed);
		var context = new AdmissionWorkflowStageRuntimeContext(
			application.Id, workflow.Id, current.Id, application.Status);
		if (!input.SkipCurrentStage && !input.AdminOverride)
			await stageHandlerRegistry.EnsureCanCompleteAsync(current.StageType, context);

		var transitions = await transitionRepository.GetListAsync(transition =>
			transition.WorkflowDefinitionId == application.WorkflowDefinitionId &&
			transition.FromStageId == current.DefinitionStageId);
		var selected = definition.IsTerminalStage ? null : transitionSelector.Select(
			transitions, await GetRuntimeValuesAsync(application), input.TransitionDefinitionId, input.AdminOverride);
		var next = selected == null ? null : stages.SingleOrDefault(stage =>
			stage.DefinitionStageId == selected.ToStageId && stage.Status == AdmissionRuntimeStageStatus.Pending) ??
			throw new BusinessException(AdmissionApplicationErrorCodes.NoEligibleTransition);
		await CompleteCurrentAsync(application, workflow, current, input, context);
		await EnterNextAsync(application, workflow, current, next, selected);
		await RecordHistoryAsync(application, workflow, current, next, selected, input);
	}

	private async Task CompleteCurrentAsync(
		AdmissionApplication application,
		AdmissionApplicationWorkflowInstance workflow,
		AdmissionApplicationStageInstance current,
		AdvanceAdmissionWorkflowDto input,
		AdmissionWorkflowStageRuntimeContext context)
	{
		if (input.SkipCurrentStage)
		{
			current.Skip(CurrentUser.Id, input.Reason!);
			application.RecordStageSkipped(workflow.Id, current.Id, input.Reason!);
		}
		else
		{
			current.Complete(CurrentUser.Id, input.Reason);
			if (!input.AdminOverride) await stageHandlerRegistry.OnCompleteAsync(current.StageType, context);
			application.RecordStageCompleted(workflow.Id, current.Id);
		}
		await stageRepository.UpdateAsync(current, false);
	}

	private async Task EnterNextAsync(
		AdmissionApplication application,
		AdmissionApplicationWorkflowInstance workflow,
		AdmissionApplicationStageInstance current,
		AdmissionApplicationStageInstance? next,
		AdmissionWorkflowTransitionDefinition? selected)
	{
		if (next == null)
		{
			workflow.Complete();
			application.CompleteWorkflow();
			application.RecordWorkflowCompleted(workflow.Id, current.Id);
			await workflowRepository.UpdateAsync(workflow, false);
			return;
		}
		next.Enter();
		await stageHandlerRegistry.OnEnterAsync(next.StageType,
			new AdmissionWorkflowStageRuntimeContext(application.Id, workflow.Id, next.Id, application.Status));
		application.SetCurrentStage(next.Id);
		application.RecordTransitioned(workflow.Id, current.Id, next.Id, selected?.Id);
		application.RecordStageEntered(workflow.Id, next.Id);
		await stageRepository.UpdateAsync(next, false);
	}

	private async Task RecordHistoryAsync(
		AdmissionApplication application,
		AdmissionApplicationWorkflowInstance workflow,
		AdmissionApplicationStageInstance current,
		AdmissionApplicationStageInstance? next,
		AdmissionWorkflowTransitionDefinition? selected,
		AdvanceAdmissionWorkflowDto input)
	{
		await historyRepository.InsertAsync(new AdmissionWorkflowTransitionHistory(
			GuidGenerator.Create(), CurrentTenant.Id, application.Id, workflow.Id, current.Id, next?.Id,
			selected?.Id, GetAction(input), input.IdempotencyKey.Trim(), input.Reason,
			JsonSerializer.Serialize(new { selected?.Id, selected?.IsDefault, selected?.ConditionRuleJson })), false);
		await applicationRepository.UpdateAsync(application, false);
		await AddActivitiesAsync(application, current, next, input);
	}

	private async Task<IReadOnlyDictionary<string, string>> GetRuntimeValuesAsync(AdmissionApplication application)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["application.status"] = application.Status.ToString()
		};
		var form = await formRepository.FindAsync(instance => instance.ApplicationId == application.Id);
		if (form == null) return result;
		foreach (var value in await valueRepository.GetListAsync(item => item.FormInstanceId == form.Id))
		{
			var unwrapped = AdmissionApplicationValueConverter.Unwrap(value.ValueJson);
			result[value.FieldCode] = unwrapped;
			result[$"application.{value.FieldCode}"] = unwrapped;
		}
		return result;
	}

	private async Task AddActivitiesAsync(
		AdmissionApplication application,
		AdmissionApplicationStageInstance current,
		AdmissionApplicationStageInstance? next,
		AdvanceAdmissionWorkflowDto input)
	{
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			input.SkipCurrentStage ? "WorkflowStageSkipped" : "WorkflowStageCompleted",
			input.SkipCurrentStage ? $"Stage skipped: {current.Name}" : $"Stage completed: {current.Name}",
			application.ApplicantId, application.Id,
			metadataJson: JsonSerializer.Serialize(new { current.Id, NextStageInstanceId = next?.Id, input.Reason })), false);
		var eventType = next == null ? "WorkflowCompleted" : "WorkflowTransitioned";
		var description = next == null ? $"Workflow completed at {current.Name}" : $"Workflow moved from {current.Name} to {next.Name}";
		await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
			eventType, description, application.ApplicantId, application.Id), false);
		if (next != null)
			await activityRepository.InsertAsync(new AdmissionActivity(GuidGenerator.Create(), CurrentTenant.Id,
				"WorkflowStageEntered", $"Stage entered: {next.Name}", application.ApplicantId, application.Id), false);
	}

	private static AdmissionWorkflowTransitionAction GetAction(AdvanceAdmissionWorkflowDto input) =>
		input.AdminOverride ? AdmissionWorkflowTransitionAction.AdminOverride :
		input.SkipCurrentStage ? AdmissionWorkflowTransitionAction.Skipped :
		AdmissionWorkflowTransitionAction.Completed;
}
