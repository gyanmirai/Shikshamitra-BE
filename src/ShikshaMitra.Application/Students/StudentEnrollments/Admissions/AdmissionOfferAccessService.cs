using System;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionOfferContext(
	AdmissionApplication Application,
	AdmissionApplicationWorkflowInstance Workflow,
	AdmissionApplicationStageInstance Stage,
	AdmissionWorkflowStageDefinition Definition,
	AdmissionOfferStagePolicy? Policy)
{
	public bool IsActive => Workflow.Status == AdmissionWorkflowInstanceStatus.Active &&
		Application.CurrentStageInstanceId == Stage.Id && Stage.Status == AdmissionRuntimeStageStatus.Active;
}

[RemoteService(false)]
public class AdmissionOfferAccessService(
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	AdmissionApplicationReadService applications) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionOfferContext> GetContextAsync(Guid applicationId, Guid stageId)
	{
		var application = await applications.GetEntityAsync(applicationId);
		if (AdmissionWorkflowStageTypeRegistry.Find(AdmissionWorkflowStageType.Offer)?.IsSelectable != true)
			throw new BusinessException(AdmissionOfferStageErrorCodes.InvalidState, "Offer stages are retired. Finalize the approved admission after completing its journey.");
		var stage = await stages.FindAsync(stageId) ??
			throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var workflow = await workflows.FindAsync(stage.WorkflowInstanceId) ??
			throw new EntityNotFoundException(typeof(AdmissionApplicationWorkflowInstance), stage.WorkflowInstanceId);
		if (workflow.ApplicationId != application.Id || stage.StageType != AdmissionWorkflowStageType.Offer)
			throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var definition = await definitions.GetAsync(stage.DefinitionStageId);
		if (definition.WorkflowDefinitionId != application.WorkflowDefinitionId ||
			definition.StageType != AdmissionWorkflowStageType.Offer)
			throw new EntityNotFoundException(typeof(AdmissionWorkflowStageDefinition), definition.Id);
		return new AdmissionOfferContext(application, workflow, stage, definition, ReadPolicy(definition));
	}

	public async Task<AdmissionOfferContext?> FindCurrentManagedAsync(Guid applicationId)
	{
		var application = await applications.GetEntityAsync(applicationId);
		if (!application.CurrentStageInstanceId.HasValue) return null;
		var stage = await stages.FindAsync(application.CurrentStageInstanceId.Value);
		if (stage?.StageType != AdmissionWorkflowStageType.Offer) return null;
		var context = await GetContextAsync(applicationId, application.CurrentStageInstanceId.Value);
		return context.Policy != null && context.IsActive ? context : null;
	}

	public static AdmissionOfferStagePolicy? ReadPolicy(AdmissionWorkflowStageDefinition definition)
	{
		try { return AdmissionOfferStagePolicy.Read(definition.ConfigurationJson); }
		catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
		{ throw new BusinessException(AdmissionOfferStageErrorCodes.InvalidPolicy, exception.Message); }
	}

	public static void EnsureConcurrency(IHasConcurrencyStamp entity, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || entity.ConcurrencyStamp != stamp)
			throw new BusinessException(AdmissionOfferStageErrorCodes.ConcurrencyConflict,
				"This offer changed. Refresh it before continuing.");
	}
}
