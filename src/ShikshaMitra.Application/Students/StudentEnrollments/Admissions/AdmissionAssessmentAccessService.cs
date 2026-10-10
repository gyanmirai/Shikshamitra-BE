using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionAssessmentContext(AdmissionApplication Application,
	AdmissionApplicationStageInstance Stage, AdmissionWorkflowStageDefinition Definition, AdmissionAssessmentPolicy? Policy)
{
	public AdmissionWorkflowInstanceStatus WorkflowStatus { get; init; } = AdmissionWorkflowInstanceStatus.Active;
	public bool CanPrepare => AdmissionStagePreparationRules.CanPrepare(Application, WorkflowStatus, Stage.Status);
	public bool IsActive => WorkflowStatus == AdmissionWorkflowInstanceStatus.Active && Application.CurrentStageInstanceId == Stage.Id && Stage.Status == AdmissionRuntimeStageStatus.Active &&
		Application.Status is AdmissionApplicationStatus.Draft or AdmissionApplicationStatus.Submitted or
		AdmissionApplicationStatus.InReview or AdmissionApplicationStatus.Approved or AdmissionApplicationStatus.Waitlisted or
		AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted;
}

[RemoteService(false)]
public class AdmissionAssessmentAccessService(
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> instances,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	IRepository<AdmissionWorkflowDefinition, Guid> workflows,
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionCycle, Guid> cycles,
	IRepository<Division, Guid> divisions,
	IUserScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	public async Task<IQueryable<AdmissionOffering>> GetScopedOfferingsAsync()
	{
		var query = await offerings.GetQueryableAsync();
		var scope = await scopeAccess.GetCurrentUserScopeAsync();
		if (scope.IsAdmin || scope.HasTenantWideScope()) return query;
		var cycleQuery = await cycles.GetQueryableAsync();
		var divisionQuery = await divisions.GetQueryableAsync();
		return AdmissionOfferingScopeQuery.Apply(query, cycleQuery, divisionQuery, scope);
	}

	public async Task<AdmissionOffering> GetOfferingAsync(Guid id) =>
		await AsyncExecuter.FirstOrDefaultAsync((await GetScopedOfferingsAsync()).Where(item => item.Id == id)) ??
		throw new EntityNotFoundException(typeof(AdmissionOffering), id);

	public async Task<AdmissionAssessmentContext> GetContextAsync(Guid applicationId, Guid stageId, bool requireActive = true, bool requirePreparation = false)
	{
		var application = await applications.FindAsync(applicationId) ?? throw new EntityNotFoundException(typeof(AdmissionApplication), applicationId);
		await GetOfferingAsync(application.AdmissionOfferingId);
		var stage = await stages.FindAsync(stageId) ?? throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var workflow = await instances.FindAsync(stage.WorkflowInstanceId);
		if (workflow?.ApplicationId != applicationId || workflow.WorkflowDefinitionId != application.WorkflowDefinitionId ||
			stage.StageType != AdmissionWorkflowStageType.Assessment)
			throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var definition = await definitions.GetAsync(stage.DefinitionStageId);
		if (definition.WorkflowDefinitionId != application.WorkflowDefinitionId)
			throw new EntityNotFoundException(typeof(AdmissionWorkflowStageDefinition), definition.Id);
		var context = new AdmissionAssessmentContext(application, stage, definition, ReadPolicy(definition)) { WorkflowStatus = workflow.Status };
		if (requireActive && (!context.IsActive || context.Policy == null || workflow.Status != AdmissionWorkflowInstanceStatus.Active))
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "The managed assessment stage must be active.");
		if (requirePreparation && (!context.CanPrepare || context.Policy == null))
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Assessment changes require an open admission journey and an unfinished assessment stage.");
		return context;
	}

	public async Task<AdmissionWorkflowStageDefinition> GetSessionDefinitionAsync(Guid offeringId, Guid definitionId)
	{
		var definition = await GetManagedDefinitionAsync(offeringId, definitionId);
		if (ReadPolicy(definition)?.SchedulingEnabled != true)
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Choose a scheduled assessment stage belonging to this offering.");
		return definition;
	}

	public async Task<AdmissionWorkflowStageDefinition> GetManagedDefinitionAsync(Guid offeringId, Guid definitionId)
	{
		var offering = await GetOfferingAsync(offeringId);
		var definition = await definitions.FindAsync(definitionId) ?? throw new EntityNotFoundException(typeof(AdmissionWorkflowStageDefinition), definitionId);
		var workflow = await workflows.FindAsync(definition.WorkflowDefinitionId);
		if (workflow == null || !workflow.Status.IsRuntimeVersion())
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Assessment sessions require a published workflow version.");
		if (workflow.Status == AdmissionWorkflowStatus.Archived && !await applications.AnyAsync(application =>
			application.AdmissionOfferingId == offeringId && application.WorkflowDefinitionId == workflow.Id))
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Archived workflows only support their existing applications.");
		if (definition.StageType != AdmissionWorkflowStageType.Assessment || ReadPolicy(definition) == null ||
			(offering.WorkflowDefinitionId != definition.WorkflowDefinitionId && !await applications.AnyAsync(application =>
				application.AdmissionOfferingId == offeringId && application.WorkflowDefinitionId == definition.WorkflowDefinitionId)))
			throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidState, "Choose a managed assessment stage belonging to this offering.");
		return definition;
	}

	public static AdmissionAssessmentPolicy? ReadPolicy(AdmissionWorkflowStageDefinition definition)
	{
		try { return AdmissionAssessmentPolicy.Read(definition.ConfigurationJson) is { } policy ? policy with { ReviewRequired = false } : null; }
		catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
		{ throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidPolicy, exception.Message); }
	}

	public static void EnsureConcurrency(IHasConcurrencyStamp entity, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || entity.ConcurrencyStamp != stamp)
			throw new BusinessException(AdmissionAssessmentErrorCodes.ConcurrencyConflict, "This record changed. Refresh it before saving.");
	}
}
