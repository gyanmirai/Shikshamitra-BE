using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormStageRuntimeService(
	IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	IRepository<AdmissionFormSubmission, Guid> submissions,
	AdmissionFormStageAccessService access,
	AdmissionFormStageReadService reader) : ApplicationService, ITransientDependency
{
	public async Task EnsureCanSubmitAsync(Guid applicationId)
	{
		var context = await GetCurrentManagedContextAsync(applicationId);
		if (context?.Policy == null)
		{
			await EnsureFormIsNotPendingAsync(applicationId);
			return;
		}
		var attempts = await submissions.CountAsync(item => item.FormInstanceId == context.Form.Id);
		if (attempts >= context.Policy.MaxSubmissionAttempts)
			throw new BusinessException(AdmissionFormStageErrorCodes.AttemptLimitReached,
				$"The limit of {context.Policy.MaxSubmissionAttempts} submission attempts has been reached.");
		if (!context.Policy.AllowLateSubmission && context.DueAt is DateTime dueAt && dueAt < Clock.Now)
			throw new BusinessException(AdmissionFormStageErrorCodes.DeadlinePassed,
				"The form submission deadline has passed.");
	}

	private async Task EnsureFormIsNotPendingAsync(Guid applicationId)
	{
		var application = await applications.FindAsync(applicationId);
		if (application?.CurrentStageInstanceId is not Guid currentId) return;
		var current = await stages.FindAsync(currentId);
		// Older Inquiry entry flows submit the form and then enter its Form stage in the same handoff.
		if (current == null || current.StageType == AdmissionWorkflowStageType.Inquiry) return;
		foreach (var pending in await stages.GetListAsync(item => item.WorkflowInstanceId == current.WorkflowInstanceId &&
			item.StageType == AdmissionWorkflowStageType.Form && item.Status == AdmissionRuntimeStageStatus.Pending))
		{
			var definition = await definitions.GetAsync(pending.DefinitionStageId);
			if (AdmissionFormStageAccessService.ReadPolicy(definition) != null)
				throw new BusinessException(AdmissionFormStageErrorCodes.InvalidState,
					"Save the form as a draft until its Form stage becomes the current journey stage.");
		}
	}

	public async Task<bool> ShouldAdvanceAfterSubmitAsync(Guid applicationId)
	{
		var context = await GetCurrentManagedContextAsync(applicationId);
		return context?.Policy == null ||
			(context.Policy.AutoAdvanceOnSubmit && !context.Policy.RequireSectionVerification);
	}

	public async Task EnsureDirectReopenAllowedAsync(Guid applicationId)
	{
		if (await GetCurrentManagedContextAsync(applicationId) is not null)
			throw new BusinessException(AdmissionFormStageErrorCodes.CorrectionNotAllowed,
				"Use the managed correction action and record a reason for this Form stage.");
	}

	public async Task EnsureCompleteAsync(Guid applicationId, Guid stageId)
	{
		var status = await reader.GetAsync(applicationId, stageId);
		if (!status.FormStageEnabled) return;
		if (!status.CanComplete)
			throw new BusinessException(AdmissionFormStageErrorCodes.VerificationRequired,
				status.Blockers.Count > 0 ? status.Blockers[0] : "Complete the managed Form stage requirements.");
	}

	private async Task<AdmissionFormStageContext?> GetCurrentManagedContextAsync(Guid applicationId)
	{
		var application = await applications.FindAsync(applicationId);
		if (application?.CurrentStageInstanceId is not Guid stageId) return null;
		var stage = await stages.FindAsync(stageId);
		if (stage?.StageType != AdmissionWorkflowStageType.Form) return null;
		var definition = await definitions.GetAsync(stage.DefinitionStageId);
		if (AdmissionFormStageAccessService.ReadPolicy(definition) == null) return null;
		return await access.GetContextAsync(applicationId, stageId, true);
	}
}
