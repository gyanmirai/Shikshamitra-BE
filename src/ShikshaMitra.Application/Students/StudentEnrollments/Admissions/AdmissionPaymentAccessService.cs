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

public sealed record AdmissionPaymentContext(
	AdmissionApplication Application,
	AdmissionApplicationWorkflowInstance Workflow,
	AdmissionApplicationStageInstance Stage,
	AdmissionWorkflowStageDefinition Definition,
	AdmissionPaymentStagePolicy? Policy)
{
	public bool IsActive => Workflow.Status == AdmissionWorkflowInstanceStatus.Active &&
		Application.CurrentStageInstanceId == Stage.Id && Stage.Status == AdmissionRuntimeStageStatus.Active;
	public DateTime? DueAt => Policy?.DueInDays is int days && Stage.EnteredAt.HasValue
		? Stage.EnteredAt.Value.AddDays(days) : null;
}

[RemoteService(false)]
public class AdmissionPaymentAccessService(
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	AdmissionApplicationReadService applications) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionPaymentContext> GetContextAsync(Guid applicationId, Guid stageId, bool requireManaged = false)
	{
		var application = await applications.GetEntityAsync(applicationId);
		var stage = await stages.FindAsync(stageId) ??
			throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var workflow = await workflows.FindAsync(stage.WorkflowInstanceId) ??
			throw new EntityNotFoundException(typeof(AdmissionApplicationWorkflowInstance), stage.WorkflowInstanceId);
		if (workflow.ApplicationId != application.Id || stage.StageType != AdmissionWorkflowStageType.Payment)
			throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var definition = await definitions.GetAsync(stage.DefinitionStageId);
		if (definition.WorkflowDefinitionId != application.WorkflowDefinitionId ||
			definition.StageType != AdmissionWorkflowStageType.Payment)
			throw new EntityNotFoundException(typeof(AdmissionWorkflowStageDefinition), definition.Id);
		var context = new AdmissionPaymentContext(application, workflow, stage, definition, ReadPolicy(definition));
		if (requireManaged && (context.Policy == null || !context.IsActive))
			throw new BusinessException(AdmissionPaymentStageErrorCodes.InvalidState,
				"The managed Payment stage must be active.");
		return context;
	}

	public static AdmissionPaymentStagePolicy? ReadPolicy(AdmissionWorkflowStageDefinition definition)
	{
		try { return AdmissionPaymentStagePolicy.Read(definition.ConfigurationJson); }
		catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
		{ throw new BusinessException(AdmissionPaymentStageErrorCodes.InvalidPolicy, exception.Message); }
	}

	public static void EnsureConcurrency(IHasConcurrencyStamp entity, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || entity.ConcurrencyStamp != stamp)
			throw new BusinessException(AdmissionPaymentStageErrorCodes.ConcurrencyConflict,
				"This payment changed. Refresh it before continuing.");
	}
}
