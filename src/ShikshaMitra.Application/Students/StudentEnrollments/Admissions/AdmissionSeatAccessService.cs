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

public sealed record AdmissionSeatContext(
	AdmissionApplication Application,
	AdmissionApplicationWorkflowInstance Workflow,
	AdmissionApplicationStageInstance Stage,
	AdmissionWorkflowStageDefinition Definition,
	AdmissionSeatStagePolicy? Policy)
{
	public bool IsActive => Workflow.Status == AdmissionWorkflowInstanceStatus.Active &&
		Application.CurrentStageInstanceId == Stage.Id && Stage.Status == AdmissionRuntimeStageStatus.Active;
}

[RemoteService(false)]
public class AdmissionSeatAccessService(
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	AdmissionApplicationReadService applications) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionSeatContext> GetContextAsync(Guid applicationId, Guid stageId)
	{
		var application = await applications.GetEntityAsync(applicationId);
		var stage = await stages.FindAsync(stageId) ??
			throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var workflow = await workflows.FindAsync(stage.WorkflowInstanceId) ??
			throw new EntityNotFoundException(typeof(AdmissionApplicationWorkflowInstance), stage.WorkflowInstanceId);
		if (workflow.ApplicationId != application.Id || stage.StageType != AdmissionWorkflowStageType.Seat)
			throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var definition = await definitions.GetAsync(stage.DefinitionStageId);
		if (definition.WorkflowDefinitionId != application.WorkflowDefinitionId ||
			definition.StageType != AdmissionWorkflowStageType.Seat)
			throw new EntityNotFoundException(typeof(AdmissionWorkflowStageDefinition), definition.Id);
		return new AdmissionSeatContext(application, workflow, stage, definition, ReadPolicy(definition));
	}

	public static AdmissionSeatStagePolicy? ReadPolicy(AdmissionWorkflowStageDefinition definition)
	{
		try { return AdmissionSeatStagePolicy.Read(definition.ConfigurationJson); }
		catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
		{ throw new BusinessException(AdmissionSeatStageErrorCodes.InvalidPolicy, exception.Message); }
	}

	public static void EnsureConcurrency(IHasConcurrencyStamp entity, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || entity.ConcurrencyStamp != stamp)
			throw new BusinessException(AdmissionSeatStageErrorCodes.ConcurrencyConflict,
				"The seat allocation changed. Refresh it before continuing.");
	}
}
