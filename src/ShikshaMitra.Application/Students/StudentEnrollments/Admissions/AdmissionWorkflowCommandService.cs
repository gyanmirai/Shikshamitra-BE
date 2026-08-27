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
	AdmissionWorkflowDefinitionStore definitionStore,
	AdmissionWorkflowReadService readService,
	AdmissionWorkflowValidationService validationService)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionWorkflowDto> CreateAsync(CreateUpdateAdmissionWorkflowDto input)
	{
		await EnsureCodeVersionAvailableAsync(input.Code, 1);
		var workflow = new AdmissionWorkflowDefinition(
			GuidGenerator.Create(), CurrentTenant.Id, input.Name, input.Code, description: input.Description);
		// Keep the value explicit at the application boundary as a safeguard for
		// databases where the repair migration has not been applied yet.
		workflow.IsDeleted = false;
		await workflowRepository.InsertAsync(workflow, autoSave: true);
		await definitionStore.ReplaceChildrenAsync(workflow.Id, input);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await definitionStore.GetDtoAsync(workflow);
	}

	public async Task<AdmissionWorkflowDto> UpdateAsync(Guid id, CreateUpdateAdmissionWorkflowDto input)
	{
		var workflow = await definitionStore.GetAsync(id);
		workflow.EnsureDraft();
		EnsureConcurrency(workflow, input.ConcurrencyStamp);
		if (workflow.ParentVersionId.HasValue &&
			!string.Equals(workflow.Code, NormalizeCode(input.Code), StringComparison.Ordinal))
		{
			throw new BusinessException(AdmissionWorkflowErrorCodes.CodeAlreadyExists,
				"Workflow code cannot change inside a version history.");
		}
		await EnsureCodeVersionAvailableAsync(input.Code, workflow.Version, workflow.Id);
		workflow.UpdateDetails(input.Name, input.Code, input.Description);
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
		await EnsureCodeVersionAvailableAsync(input.Code, 1);
		var clone = new AdmissionWorkflowDefinition(
			GuidGenerator.Create(), CurrentTenant.Id, input.Name, input.Code, description: input.Description);
		clone.IsDeleted = false;
		await workflowRepository.InsertAsync(clone, autoSave: true);
		await definitionStore.CopyChildrenAsync(source.Id, clone.Id);
		return await definitionStore.GetDtoAsync(clone);
	}

	public async Task<AdmissionWorkflowDto> CreateNewVersionAsync(Guid id)
	{
		var source = await definitionStore.GetAsync(id);
		if (source.Status == AdmissionWorkflowStatus.Draft)
			throw new BusinessException(AdmissionWorkflowErrorCodes.DraftRequired,
				"Publish the current draft before creating another version.");
		var query = await workflowRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(query.Where(workflow => workflow.Code == source.Code));
		if (versions.Any(workflow => workflow.Status == AdmissionWorkflowStatus.Draft))
			throw new BusinessException(AdmissionWorkflowErrorCodes.DraftRequired,
				"This workflow already has a draft version.");
		var version = new AdmissionWorkflowDefinition(
			GuidGenerator.Create(), CurrentTenant.Id, source.Name, source.Code,
			versions.Max(workflow => workflow.Version) + 1, source.Id, source.Description);
		version.IsDeleted = false;
		version.RecordVersionCreated(source.Id);
		await workflowRepository.InsertAsync(version, autoSave: true);
		await definitionStore.CopyChildrenAsync(source.Id, version.Id);
		return await definitionStore.GetDtoAsync(version);
	}

	public Task<AdmissionWorkflowValidationResultDto> ValidateAsync(Guid id) =>
		validationService.ValidateAsync(id);

	public async Task<AdmissionWorkflowDto> PublishAsync(Guid id, string? concurrencyStamp)
	{
		var workflow = await definitionStore.GetAsync(id);
		if (workflow.Status == AdmissionWorkflowStatus.Published)
			return await definitionStore.GetDtoAsync(workflow);
		workflow.EnsureDraft();
		EnsureConcurrency(workflow, concurrencyStamp);
		var validation = await validationService.ValidateAsync(id);
		if (!validation.IsValid)
		{
			throw new BusinessException(AdmissionWorkflowErrorCodes.InvalidTransition,
				"Workflow validation failed.")
				.WithData("ValidationIssues", validation.Issues.Select(issue => issue.Code).ToArray());
		}
		workflow.Publish(CurrentUser.Id);
		await workflowRepository.UpdateAsync(workflow, autoSave: true);
		return await definitionStore.GetDtoAsync(workflow);
	}

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
		workflow.Archive();
		await workflowRepository.UpdateAsync(workflow, autoSave: true);
		return await definitionStore.GetDtoAsync(workflow);
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
}
