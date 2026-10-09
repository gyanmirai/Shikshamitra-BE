using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowCommandService(
	IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository,
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionFormDefinition, Guid> formRepository,
	AdmissionWorkflowDefinitionStore definitionStore,
	AdmissionWorkflowReadService readService,
	AdmissionWorkflowValidationService validationService,
	AdmissionWorkflowGovernanceService governanceService)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionWorkflowDto> CreateAsync(CreateUpdateAdmissionWorkflowDto input)
	{
		var id = GuidGenerator.Create();
		var code = AdmissionSettingsCode.ForCreate(input.Code, "WF", id);
		await EnsureCodeVersionAvailableAsync(code, 1);
		await EnsurePublishedInquiryFormAsync(input.DefaultInquiryFormDefinitionId);
		var workflow = new AdmissionWorkflowDefinition(
			id, CurrentTenant.Id, input.Name, code, description: input.Description,
			ownerUserId: CurrentUser.Id,
			defaultInquiryFormDefinitionId: input.DefaultInquiryFormDefinitionId);
		// Keep the value explicit at the application boundary as a safeguard for
		// databases where the repair migration has not been applied yet.
		workflow.IsDeleted = false;
		await workflowRepository.InsertAsync(workflow, autoSave: true);
		await definitionStore.ReplaceChildrenAsync(workflow.Id, input);
		await governanceService.RecordAsync(workflow, AdmissionWorkflowGovernanceAction.Created,
			null, CurrentUser.Id);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await definitionStore.GetDtoAsync(workflow);
	}

	public async Task<AdmissionWorkflowDto> UpdateAsync(Guid id, CreateUpdateAdmissionWorkflowDto input)
	{
		var workflow = await definitionStore.GetAsync(id);
		workflow.PrepareForEdit();
		EnsureConcurrency(workflow, input.ConcurrencyStamp);
		var code = AdmissionSettingsCode.ForUpdate(input.Code, workflow.Code);
		if (workflow.ParentVersionId.HasValue &&
			!string.Equals(workflow.Code, code, StringComparison.Ordinal))
		{
			throw new BusinessException(AdmissionWorkflowErrorCodes.CodeAlreadyExists,
				"Workflow code cannot change inside a version history.");
		}
		await EnsureCodeVersionAvailableAsync(code, workflow.Version, workflow.Id);
		await EnsurePublishedInquiryFormAsync(input.DefaultInquiryFormDefinitionId);
		workflow.UpdateDetails(input.Name, code, input.Description);
		workflow.SetDefaultInquiryForm(input.DefaultInquiryFormDefinitionId);
		await definitionStore.ReplaceChildrenAsync(id, input);
		await workflowRepository.UpdateAsync(workflow, autoSave: true);
		return await definitionStore.GetDtoAsync(workflow);
	}

	public async Task DeleteAsync(Guid id, string? concurrencyStamp = null)
	{
		var workflow = await definitionStore.GetAsync(id);
		workflow.EnsureDraft();
		EnsureConcurrency(workflow, concurrencyStamp);
		if (!(await readService.GetUsageAsync(id)).CanDelete)
		{
			throw new BusinessException(AdmissionWorkflowErrorCodes.WorkflowInUse,
				"A workflow used by an offering or application cannot be deleted.");
		}
		await definitionStore.DeleteChildrenAsync(id);
		await workflowRepository.DeleteAsync(workflow, autoSave: true);
	}

	public async Task<AdmissionWorkflowDto> CloneAsync(Guid id, CloneAdmissionWorkflowDto input)
	{
		var source = await definitionStore.GetAsync(id);
		var cloneId = GuidGenerator.Create();
		var code = AdmissionSettingsCode.ForCreate(input.Code, "WF", cloneId);
		await EnsureCodeVersionAvailableAsync(code, 1);
		var clone = new AdmissionWorkflowDefinition(
			cloneId, CurrentTenant.Id, input.Name, code, description: input.Description,
			ownerUserId: CurrentUser.Id,
			defaultInquiryFormDefinitionId: source.DefaultInquiryFormDefinitionId);
		clone.IsDeleted = false;
		await workflowRepository.InsertAsync(clone, autoSave: true);
		await definitionStore.CopyChildrenAsync(source.Id, clone.Id);
		await governanceService.RecordAsync(clone, AdmissionWorkflowGovernanceAction.Created,
			null, CurrentUser.Id, "Cloned from an existing workflow.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await definitionStore.GetDtoAsync(clone);
	}

	public async Task<AdmissionWorkflowDto> CreateNewVersionAsync(Guid id)
	{
		var source = await definitionStore.GetAsync(id);
		if (IsPending(source.Status))
			throw new BusinessException(AdmissionWorkflowErrorCodes.DraftRequired,
				"Publish the current draft before creating another version.");
		var query = await workflowRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(query.Where(workflow => workflow.Code == source.Code));
		if (versions.Any(workflow => IsPending(workflow.Status)))
			throw new BusinessException(AdmissionWorkflowErrorCodes.DraftRequired,
				"This workflow already has a draft version.");
		var version = new AdmissionWorkflowDefinition(
			GuidGenerator.Create(), CurrentTenant.Id, source.Name, source.Code,
			versions.Max(workflow => workflow.Version) + 1, source.Id, source.Description,
			CurrentUser.Id, source.DefaultInquiryFormDefinitionId);
		version.IsDeleted = false;
		version.RecordVersionCreated(source.Id);
		await workflowRepository.InsertAsync(version, autoSave: true);
		await definitionStore.CopyChildrenAsync(source.Id, version.Id);
		await governanceService.RecordAsync(version, AdmissionWorkflowGovernanceAction.Created,
			null, CurrentUser.Id, $"Created from version {source.Version}.");
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await definitionStore.GetDtoAsync(version);
	}

	public async Task<AdmissionWorkflowValidationResultDto> ValidateAsync(Guid id)
	{
		var workflow = await definitionStore.GetAsync(id);
		var validation = await validationService.ValidateAsync(id);
		if (!validation.IsValid || workflow.Status != AdmissionWorkflowStatus.Draft) return validation;
		var fromStatus = workflow.Status;
		workflow.MarkValidated(CurrentUser.Id, Clock.Now);
		await governanceService.RecordAsync(workflow, AdmissionWorkflowGovernanceAction.Validated,
			fromStatus, CurrentUser.Id);
		await workflowRepository.UpdateAsync(workflow, autoSave: true);
		return validation;
	}

	public async Task<AdmissionWorkflowDto> PublishAsync(Guid id, string? concurrencyStamp)
	{
		var workflow = await definitionStore.GetAsync(id);
		if (workflow.Status == AdmissionWorkflowStatus.Published)
			return await definitionStore.GetDtoAsync(workflow);
		EnsureConcurrency(workflow, concurrencyStamp);
		if (workflow.Status is not (AdmissionWorkflowStatus.Draft or AdmissionWorkflowStatus.Validated or
			AdmissionWorkflowStatus.Approved or AdmissionWorkflowStatus.Scheduled))
			throw new BusinessException(AdmissionWorkflowErrorCodes.InvalidGovernanceTransition);
		await EnsureValidAsync(workflow);
		if (workflow.Status == AdmissionWorkflowStatus.Draft)
		{
			var fromStatus = workflow.Status;
			workflow.MarkValidated(CurrentUser.Id, Clock.Now);
			await governanceService.RecordAsync(workflow, AdmissionWorkflowGovernanceAction.Validated,
				fromStatus, CurrentUser.Id);
		}
		await PublishNowAsync(workflow, CurrentUser.Id, "Direct publication");
		return await definitionStore.GetDtoAsync(workflow);
	}

	public async Task<AdmissionWorkflowDto> SubmitForApprovalAsync(Guid id, SubmitAdmissionWorkflowDto input)
	{
		if (!(await governanceService.GetSettingsAsync()).MakerCheckerEnabled)
			throw new BusinessException(AdmissionWorkflowErrorCodes.MakerCheckerDisabled,
				"Maker-checker governance is not enabled for this school.");
		var actorId = CurrentUser.Id ?? throw new BusinessException(AdmissionWorkflowErrorCodes.ApproverUnavailable);
		var workflow = await definitionStore.GetAsync(id);
		EnsureConcurrency(workflow, input.ConcurrencyStamp);
		await governanceService.EnsureApproverAsync(input.ApproverUserId);
		await EnsureValidAsync(workflow);
		if (workflow.Status == AdmissionWorkflowStatus.Draft)
		{
			var validationFrom = workflow.Status;
			workflow.MarkValidated(actorId, Clock.Now);
			await governanceService.RecordAsync(workflow, AdmissionWorkflowGovernanceAction.Validated,
				validationFrom, actorId);
		}
		var fromStatus = workflow.Status;
		workflow.SubmitForApproval(actorId, input.ApproverUserId, input.ChangeReason, Clock.Now);
		await governanceService.RecordAsync(workflow, AdmissionWorkflowGovernanceAction.Submitted,
			fromStatus, actorId, input.ChangeReason, input.ApproverUserId);
		await workflowRepository.UpdateAsync(workflow, autoSave: false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await definitionStore.GetDtoAsync(workflow);
	}

	public async Task<AdmissionWorkflowDto> ApproveAsync(Guid id, DecideAdmissionWorkflowDto input)
	{
		var actorId = CurrentUser.Id ?? throw new BusinessException(AdmissionWorkflowErrorCodes.ApproverUnavailable);
		var workflow = await definitionStore.GetAsync(id);
		EnsureConcurrency(workflow, input.ConcurrencyStamp);
		var fromStatus = workflow.Status;
		workflow.Approve(actorId, Clock.Now);
		await governanceService.RecordAsync(workflow, AdmissionWorkflowGovernanceAction.Approved,
			fromStatus, actorId, input.Reason);
		await workflowRepository.UpdateAsync(workflow, autoSave: false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await definitionStore.GetDtoAsync(workflow);
	}

	public async Task<AdmissionWorkflowDto> RejectAsync(Guid id, DecideAdmissionWorkflowDto input)
	{
		var actorId = CurrentUser.Id ?? throw new BusinessException(AdmissionWorkflowErrorCodes.ApproverUnavailable);
		var workflow = await definitionStore.GetAsync(id);
		EnsureConcurrency(workflow, input.ConcurrencyStamp);
		var fromStatus = workflow.Status;
		workflow.Reject(actorId, RequireReason(input.Reason));
		await governanceService.RecordAsync(workflow, AdmissionWorkflowGovernanceAction.Rejected,
			fromStatus, actorId, input.Reason);
		await workflowRepository.UpdateAsync(workflow, autoSave: false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await definitionStore.GetDtoAsync(workflow);
	}

	// Keep the existing endpoint contract for older clients; publication is always immediate.
	public Task<AdmissionWorkflowDto> PublishWithGovernanceAsync(Guid id, PublishAdmissionWorkflowDto input) =>
		PublishAsync(id, input.ConcurrencyStamp);

	public async Task<AdmissionWorkflowDto> CreateRollbackDraftAsync(Guid sourceId, RollbackAdmissionWorkflowDto input)
	{
		var source = await definitionStore.GetAsync(sourceId);
		if (IsPending(source.Status))
			throw new BusinessException(AdmissionWorkflowErrorCodes.InvalidGovernanceTransition,
				"Rollback can only start from a historical published, superseded, or archived version.");
		var reason = RequireReason(input.ChangeReason);
		var query = await workflowRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(query.Where(workflow => workflow.Code == source.Code));
		if (versions.Any(workflow => IsPending(workflow.Status)))
			throw new BusinessException(AdmissionWorkflowErrorCodes.DraftRequired,
				"Complete or remove the existing pending version before creating a rollback draft.");
		var rollback = new AdmissionWorkflowDefinition(GuidGenerator.Create(), CurrentTenant.Id, source.Name, source.Code,
			versions.Max(workflow => workflow.Version) + 1, source.Id, source.Description, CurrentUser.Id,
			source.DefaultInquiryFormDefinitionId);
		rollback.IsDeleted = false;
		rollback.RecordVersionCreated(source.Id);
		rollback.RecordRollbackSource(source.Id, reason);
		await workflowRepository.InsertAsync(rollback, autoSave: true);
		await definitionStore.CopyChildrenAsync(source.Id, rollback.Id);
		await governanceService.RecordAsync(rollback, AdmissionWorkflowGovernanceAction.RollbackDraftCreated,
			null, CurrentUser.Id, reason);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await definitionStore.GetDtoAsync(rollback);
	}

	// Historical schedules remain stored but no longer trigger unattended publication.
	public Task PublishScheduledAsync(Guid id) => Task.CompletedTask;

	public async Task<AdmissionWorkflowDto> ArchiveAsync(Guid id, string? concurrencyStamp = null)
	{
		var workflow = await definitionStore.GetAsync(id);
		EnsureConcurrency(workflow, concurrencyStamp);
		var query = await offeringRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(query.Where(offering => offering.WorkflowDefinitionId == id && offering.IsActive)))
		{
			throw new BusinessException(AdmissionWorkflowErrorCodes.WorkflowInUse,
				"Deactivate offerings that use this workflow before archiving it.");
		}
		var fromStatus = workflow.Status;
		workflow.Archive();
		await governanceService.RecordAsync(workflow, AdmissionWorkflowGovernanceAction.Archived,
			fromStatus, CurrentUser.Id);
		await workflowRepository.UpdateAsync(workflow, autoSave: true);
		return await definitionStore.GetDtoAsync(workflow);
	}

	private async Task PublishNowAsync(AdmissionWorkflowDefinition workflow, Guid? actorId, string reason)
	{
		var fromStatus = workflow.Status;
		var query = await workflowRepository.GetQueryableAsync();
		var previous = await AsyncExecuter.ToListAsync(query.Where(item => item.Code == workflow.Code &&
			item.Id != workflow.Id && item.Status == AdmissionWorkflowStatus.Published));
		foreach (var published in previous)
		{
			published.Supersede();
			await governanceService.RecordAsync(published, AdmissionWorkflowGovernanceAction.Superseded,
				AdmissionWorkflowStatus.Published, actorId, $"Superseded by version {workflow.Version}.");
			await workflowRepository.UpdateAsync(published, autoSave: false);
		}
		workflow.Publish(actorId, Clock.Now, reason);
		await governanceService.RecordAsync(workflow, AdmissionWorkflowGovernanceAction.Published,
			fromStatus, actorId, reason);
		await workflowRepository.UpdateAsync(workflow, autoSave: false);
		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	private async Task EnsureValidAsync(AdmissionWorkflowDefinition workflow)
	{
		var validation = await validationService.ValidateAsync(workflow.Id);
		if (!validation.IsValid)
			throw new BusinessException(AdmissionWorkflowErrorCodes.InvalidTransition, "Workflow validation failed.")
				.WithData("ValidationIssues", validation.Issues.Select(issue => issue.Code).ToArray());
	}

	private async Task EnsurePublishedInquiryFormAsync(Guid? formDefinitionId)
	{
		if (!formDefinitionId.HasValue) return;
		var form = await formRepository.FindAsync(formDefinitionId.Value);
		if (form is null || form.Status != AdmissionFormStatus.Published ||
			form.Purpose != AdmissionFormPurpose.Inquiry)
		{
			throw new BusinessException(AdmissionWorkflowErrorCodes.InvalidInquiryForm,
				"Select a published Inquiry form for this workflow.");
		}
	}

	private async Task EnsureCodeVersionAvailableAsync(string code, int version, Guid? excludingId = null)
	{
		var normalizedCode = NormalizeCode(code);
		var query = await workflowRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(query.Where(workflow =>
			workflow.Code == normalizedCode && workflow.Version == version &&
			(!excludingId.HasValue || workflow.Id != excludingId.Value))))
		{
			throw new BusinessException(AdmissionWorkflowErrorCodes.CodeAlreadyExists,
				"A workflow with this code and version already exists.");
		}
	}

	private static void EnsureConcurrency(AdmissionWorkflowDefinition workflow, string? stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) ||
			!string.Equals(workflow.ConcurrencyStamp, stamp, StringComparison.Ordinal))
		{
			throw new BusinessException(AdmissionWorkflowErrorCodes.ConcurrencyConflict,
				"The workflow changed after it was opened. Reload and try again.");
		}
	}

	private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

	private static bool IsPending(AdmissionWorkflowStatus status) => status is AdmissionWorkflowStatus.Draft or
		AdmissionWorkflowStatus.Validated or AdmissionWorkflowStatus.SubmittedForApproval or
		AdmissionWorkflowStatus.Approved or AdmissionWorkflowStatus.Scheduled;

	private static string RequireReason(string? reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
			throw new BusinessException(AdmissionWorkflowErrorCodes.ChangeReasonRequired,
				"Enter a change reason before continuing.");
		return Check.Length(reason.Trim(), nameof(reason), AdmissionWorkflowConsts.MaxReasonLength)!;
	}
}
