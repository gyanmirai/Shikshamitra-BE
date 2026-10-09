using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public sealed record AdmissionReviewContext(AdmissionApplication Application, AdmissionApplicationStageInstance Stage,
	AdmissionWorkflowStageDefinition Definition, AdmissionReviewPolicy? Policy, AdmissionWorkflowInstanceStatus WorkflowStatus)
{
	public bool IsActive => WorkflowStatus == AdmissionWorkflowInstanceStatus.Active && Application.CurrentStageInstanceId == Stage.Id && Stage.Status == AdmissionRuntimeStageStatus.Active;
	public bool CanMutate => Policy != null && Stage.Status is AdmissionRuntimeStageStatus.Active or AdmissionRuntimeStageStatus.Completed &&
		WorkflowStatus is AdmissionWorkflowInstanceStatus.Active or AdmissionWorkflowInstanceStatus.Completed &&
		Application.Status is AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.InReview or AdmissionApplicationStatus.Waitlisted or
			AdmissionApplicationStatus.Approved or AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted;
}

[RemoteService(false)]
public class AdmissionReviewAccessService(IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionApplicationStageInstance, Guid> stages, IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions, AdmissionApplicationScopeAccessService scope) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionReviewContext> GetContextAsync(Guid applicationId, Guid stageId, bool mutable = false)
	{
		var application = await applications.GetAsync(applicationId); await scope.EnsureCanAccessAsync(application);
		if (AdmissionWorkflowStageTypeRegistry.Find(AdmissionWorkflowStageType.Review)?.IsSelectable != true)
			throw new BusinessException(AdmissionReviewErrorCodes.InvalidState, "Review stages are retired. Record the admission decision instead.");
		var stage = await stages.GetAsync(stageId); var workflow = await workflows.GetAsync(stage.WorkflowInstanceId);
		if (workflow.ApplicationId != application.Id || workflow.WorkflowDefinitionId != application.WorkflowDefinitionId || stage.StageType != AdmissionWorkflowStageType.Review)
			throw new EntityNotFoundException(typeof(AdmissionApplicationStageInstance), stageId);
		var definition = await definitions.GetAsync(stage.DefinitionStageId);
		if (definition.WorkflowDefinitionId != application.WorkflowDefinitionId || definition.StageType != AdmissionWorkflowStageType.Review)
			throw new EntityNotFoundException(typeof(AdmissionWorkflowStageDefinition), definition.Id);
		var context = new AdmissionReviewContext(application, stage, definition, ReadPolicy(definition), workflow.Status);
		if (mutable && !context.CanMutate) throw new BusinessException(AdmissionReviewErrorCodes.InvalidState);
		return context;
	}
	public static AdmissionReviewPolicy? ReadPolicy(AdmissionWorkflowStageDefinition stage)
	{
		try { return AdmissionReviewPolicy.Read(stage.ConfigurationJson); }
		catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException or InvalidOperationException)
		{ throw new BusinessException(AdmissionReviewErrorCodes.InvalidPolicy, exception.Message); }
	}
	public static void EnsureConcurrency(AdmissionApplication application, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || application.ConcurrencyStamp != stamp) throw new BusinessException(AdmissionReviewErrorCodes.ConcurrencyConflict);
	}
}
