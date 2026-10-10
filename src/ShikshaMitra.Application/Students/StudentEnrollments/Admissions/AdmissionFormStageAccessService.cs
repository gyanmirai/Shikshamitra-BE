using System;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionFormStageContext(
	AdmissionApplication Application,
	AdmissionApplicationWorkflowInstance Workflow,
	AdmissionApplicationStageInstance Stage,
	AdmissionWorkflowStageDefinition Definition,
	AdmissionFormInstance Form,
	AdmissionFormStagePolicy? Policy)
{
	public bool CanPrepare => AdmissionStagePreparationRules.CanPrepare(Application, Workflow.Status, Stage.Status);
	public bool IsActive => Workflow.Status == AdmissionWorkflowInstanceStatus.Active &&
		Application.CurrentStageInstanceId == Stage.Id && Stage.Status == AdmissionRuntimeStageStatus.Active;
	public DateTime? DueAt => Policy?.DueInDays is int days && Stage.EnteredAt.HasValue
		? Stage.EnteredAt.Value.AddDays(days) : null;
}

[RemoteService(false)]
public class AdmissionFormStageAccessService(
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	IRepository<AdmissionFormInstance, Guid> forms,
	AdmissionApplicationReadService applications) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionFormStageContext> GetContextAsync(Guid applicationId, Guid stageId, bool requireManaged = false, bool requirePreparation = false)
	{
		var application = await applications.GetEntityAsync(applicationId);
		var stage = await stages.FindAsync(stageId) ??
			throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var workflow = await workflows.FindAsync(stage.WorkflowInstanceId) ??
			throw new EntityNotFoundException(typeof(AdmissionApplicationWorkflowInstance), stage.WorkflowInstanceId);
		if (workflow.ApplicationId != application.Id || workflow.WorkflowDefinitionId != application.WorkflowDefinitionId ||
			stage.StageType != AdmissionWorkflowStageType.Form)
			throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var definition = await definitions.GetAsync(stage.DefinitionStageId);
		if (definition.WorkflowDefinitionId != application.WorkflowDefinitionId || definition.StageType != AdmissionWorkflowStageType.Form)
			throw new EntityNotFoundException(typeof(AdmissionWorkflowStageDefinition), definition.Id);
		var form = await forms.FindAsync(item => item.ApplicationId == application.Id) ??
			throw new EntityNotFoundException(typeof(AdmissionFormInstance), application.Id);
		var configuredFormId = ReadFormDefinitionId(definition.ConfigurationJson);
		if (configuredFormId.HasValue && configuredFormId != form.FormDefinitionId)
			throw new BusinessException(AdmissionFormStageErrorCodes.OfferingFormMismatch,
				"The workflow Form stage does not match the form frozen for this application.");
		var context = new AdmissionFormStageContext(application, workflow, stage, definition, form, ReadPolicy(definition));
		if (requireManaged && (context.Policy == null || !context.IsActive))
			throw new BusinessException(AdmissionFormStageErrorCodes.InvalidState, "The managed Form stage must be active.");
		if (requirePreparation && (context.Policy == null || !context.CanPrepare))
			throw new BusinessException(AdmissionFormStageErrorCodes.InvalidState, "Form changes require an open admission journey and an unfinished Form stage.");
		return context;
	}

	public static AdmissionFormStagePolicy? ReadPolicy(AdmissionWorkflowStageDefinition definition)
	{
		try { return AdmissionFormStagePolicy.Read(definition.ConfigurationJson); }
		catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
		{ throw new BusinessException(AdmissionFormStageErrorCodes.InvalidPolicy, exception.Message); }
	}

	public static Guid? ReadFormDefinitionId(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		try
		{
			using var document = JsonDocument.Parse(json);
			return document.RootElement.TryGetProperty("formDefinitionId", out var value) &&
				Guid.TryParse(value.GetString(), out var id) ? id : null;
		}
		catch (JsonException) { return null; }
	}

	public static void EnsureConcurrency(AdmissionApplication application, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || application.ConcurrencyStamp != stamp)
			throw new BusinessException(AdmissionFormStageErrorCodes.ConcurrencyConflict,
				"This application changed. Refresh it before continuing.");
	}
}
