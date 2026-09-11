using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionEligibilityContext(AdmissionApplication Application, AdmissionApplicationStageInstance Stage,
	AdmissionWorkflowStageDefinition Definition, AdmissionEligibilityPolicy? Policy, AdmissionWorkflowInstanceStatus WorkflowStatus)
{
	public bool IsActive => WorkflowStatus == AdmissionWorkflowInstanceStatus.Active && Application.CurrentStageInstanceId == Stage.Id && Stage.Status == AdmissionRuntimeStageStatus.Active;
	public bool CanMutate => Policy != null && WorkflowStatus is AdmissionWorkflowInstanceStatus.Active or AdmissionWorkflowInstanceStatus.Completed &&
		Stage.Status is AdmissionRuntimeStageStatus.Active or AdmissionRuntimeStageStatus.Completed or AdmissionRuntimeStageStatus.Skipped &&
		Application.Status is AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.InReview or AdmissionApplicationStatus.Approved or
		AdmissionApplicationStatus.Waitlisted or AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted;
}

[RemoteService(false)]
public class AdmissionEligibilityAccessService(IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionApplicationStageInstance, Guid> stages, IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions, AdmissionApplicationScopeAccessService scope) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionEligibilityContext> GetContextAsync(Guid applicationId, Guid stageId, bool requireMutable = false)
	{
		var application = await applications.GetAsync(applicationId);
		await scope.EnsureCanAccessAsync(application);
		var stage = await stages.GetAsync(stageId);
		var workflow = await workflows.FindAsync(stage.WorkflowInstanceId);
		if (workflow?.ApplicationId != applicationId || workflow.WorkflowDefinitionId != application.WorkflowDefinitionId || stage.StageType != AdmissionWorkflowStageType.Eligibility)
			throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var definition = await definitions.GetAsync(stage.DefinitionStageId);
		if (definition.WorkflowDefinitionId != application.WorkflowDefinitionId || definition.StageType != AdmissionWorkflowStageType.Eligibility)
			throw new EntityNotFoundException(typeof(AdmissionWorkflowStageDefinition), definition.Id);
		var context = new AdmissionEligibilityContext(application, stage, definition, ReadPolicy(definition), workflow.Status);
		if (requireMutable && !context.CanMutate) throw new BusinessException(AdmissionEligibilityErrorCodes.InvalidState);
		return context;
	}
	public static AdmissionEligibilityPolicy? ReadPolicy(AdmissionWorkflowStageDefinition definition)
	{
		try { return AdmissionEligibilityPolicy.Read(definition.ConfigurationJson); }
		catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
		{ throw new BusinessException(AdmissionEligibilityErrorCodes.InvalidPolicy, exception.Message); }
	}
	public static void EnsureConcurrency(AdmissionApplication application, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || application.ConcurrencyStamp != stamp)
			throw new BusinessException(AdmissionEligibilityErrorCodes.ConcurrencyConflict);
	}
}
