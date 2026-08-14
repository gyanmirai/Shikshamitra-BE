using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize]
public class AdmissionOfferingAppService(
	IRepository<AdmissionOffering, Guid> offeringRepository,
	IRepository<AdmissionCycle, Guid> cycleRepository,
	IRepository<AdmissionWorkflowDefinition, Guid> workflowRepository,
	IRepository<AdmissionFormDefinition, Guid> formRepository,
	IEducationBoardRepository boardRepository,
	IEducationMediumRepository mediumRepository,
	IEducationMediumStageRepository stageRepository,
	IStandardRepository standardRepository) : ApplicationService, IAdmissionOfferingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<PagedResultDto<AdmissionOfferingDto>> GetListAsync(AdmissionOfferingListInput input)
	{
		var queryable = await offeringRepository.GetQueryableAsync();
		queryable = queryable.Where(x =>
			(!input.AdmissionCycleId.HasValue || x.AdmissionCycleId == input.AdmissionCycleId.Value) &&
			(!input.BoardId.HasValue || x.BoardId == input.BoardId.Value) &&
			(!input.MediumId.HasValue || x.MediumId == input.MediumId.Value) &&
			(!input.StageId.HasValue || x.StageId == input.StageId.Value) &&
			(!input.StandardId.HasValue || x.StandardId == input.StandardId.Value) &&
			(!input.IsActive.HasValue || x.IsActive == input.IsActive.Value) &&
			(string.IsNullOrWhiteSpace(input.Search) || x.Code.Contains(input.Search)));

		var totalCount = await AsyncExecuter.CountAsync(queryable);
		var rows = await AsyncExecuter.ToListAsync(queryable
			.OrderByDescending(x => x.CreationTime)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionOfferingDto>(totalCount, rows.Select(Map).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.View)]
	public async Task<AdmissionOfferingDto> GetAsync(Guid id) => Map(await GetOfferingAsync(id));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Manage)]
	public async Task<AdmissionOfferingDto> CreateAsync(Guid admissionCycleId, CreateUpdateAdmissionOfferingDto input)
	{
		var cycle = await GetCycleAsync(admissionCycleId);
		ValidateWithinCycle(cycle, input.OpenDate, input.CloseDate);
		await ValidateHierarchyAsync(input);
		await ValidateReferencesAsync(input);
		await EnsureUniqueAsync(admissionCycleId, input, null);

		var offering = new AdmissionOffering(GuidGenerator.Create(), CurrentTenant.Id, admissionCycleId,
			input.CampusId, input.BoardId, input.MediumId, input.StageId, input.StandardId,
			input.Code, input.OpenDate, input.CloseDate);
		offering.ConfigureReferences(input.WorkflowDefinitionId, input.InquiryFormDefinitionId, input.ApplicationFormDefinitionId);
		offering.SetActive(input.IsActive);
		await offeringRepository.InsertAsync(offering, autoSave: true);
		return Map(offering);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Manage)]
	public async Task<AdmissionOfferingDto> UpdateAsync(Guid id, CreateUpdateAdmissionOfferingDto input)
	{
		var offering = await GetOfferingAsync(id);
		var cycle = await GetCycleAsync(offering.AdmissionCycleId);
		ValidateWithinCycle(cycle, input.OpenDate, input.CloseDate);
		await ValidateHierarchyAsync(input);
		await ValidateReferencesAsync(input);
		await EnsureUniqueAsync(offering.AdmissionCycleId, input, id);
		offering.UpdateDetails(input.CampusId, input.BoardId, input.MediumId, input.StageId, input.StandardId,
			input.Code, input.OpenDate, input.CloseDate);
		offering.ConfigureReferences(input.WorkflowDefinitionId, input.InquiryFormDefinitionId, input.ApplicationFormDefinitionId);
		offering.SetActive(input.IsActive);
		await offeringRepository.UpdateAsync(offering, autoSave: true);
		return Map(offering);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionOfferings.Manage)]
	public async Task<AdmissionOfferingDto> SetActiveAsync(Guid id, bool isActive)
	{
		var offering = await GetOfferingAsync(id);
		offering.SetActive(isActive);
		await offeringRepository.UpdateAsync(offering, autoSave: true);
		return Map(offering);
	}

	private async Task<AdmissionOffering> GetOfferingAsync(Guid id) =>
		await offeringRepository.FindAsync(id) ?? throw new EntityNotFoundException(typeof(AdmissionOffering), id);

	private async Task<AdmissionCycle> GetCycleAsync(Guid id) =>
		await cycleRepository.FindAsync(id) ?? throw new BusinessException(AdmissionErrorCodes.OfferingCycleNotFound);

	private async Task EnsureUniqueAsync(Guid cycleId, CreateUpdateAdmissionOfferingDto input, Guid? excludingId)
	{
		var queryable = await offeringRepository.GetQueryableAsync();
		var exists = await AsyncExecuter.AnyAsync(queryable.Where(x =>
			x.AdmissionCycleId == cycleId && x.CampusId == input.CampusId && x.BoardId == input.BoardId &&
			x.MediumId == input.MediumId && x.StageId == input.StageId && x.StandardId == input.StandardId &&
			(!excludingId.HasValue || x.Id != excludingId.Value)));
		if (exists)
		{
			throw new BusinessException(AdmissionErrorCodes.OfferingDuplicate);
		}
	}

	private async Task ValidateHierarchyAsync(CreateUpdateAdmissionOfferingDto input)
	{
		if (input.BoardId == Guid.Empty || input.MediumId == Guid.Empty || input.StageId == Guid.Empty || input.StandardId == Guid.Empty)
		{
			throw new BusinessException(AdmissionErrorCodes.OfferingAcademicHierarchyInvalid);
		}

		var board = await boardRepository.FindAsync(input.BoardId);
		var medium = await mediumRepository.FindAsync(input.MediumId);
		var stage = await stageRepository.FindAsync(input.StageId);
		var standard = await standardRepository.FindAsync(input.StandardId);
		if (board == null || medium == null || stage == null || standard == null ||
			medium.EducationBoardId != input.BoardId || stage.EducationMediumId != input.MediumId ||
			standard.EducationMediumStageId != input.StageId)
		{
			throw new BusinessException(AdmissionErrorCodes.OfferingAcademicHierarchyInvalid);
		}
	}

	private async Task ValidateReferencesAsync(CreateUpdateAdmissionOfferingDto input)
	{
		if (input.WorkflowDefinitionId.HasValue)
		{
			var workflow = await workflowRepository.FindAsync(input.WorkflowDefinitionId.Value);
			if (workflow == null || workflow.Status != AdmissionWorkflowStatus.Published) throw new BusinessException(AdmissionErrorCodes.OfferingWorkflowNotPublished);
		}
		foreach (var formId in new[] { input.InquiryFormDefinitionId, input.ApplicationFormDefinitionId }.Where(x => x.HasValue).Select(x => x!.Value))
		{
			var form = await formRepository.FindAsync(formId);
			if (form == null || form.Status != AdmissionFormStatus.Published) throw new BusinessException(AdmissionErrorCodes.OfferingFormNotPublished);
		}
	}

	private static void ValidateWithinCycle(AdmissionCycle cycle, DateTime openDate, DateTime closeDate)
	{
		if (openDate.Date < cycle.StartDate.Date || closeDate.Date > cycle.EndDate.Date)
		{
			throw new BusinessException(AdmissionErrorCodes.OfferingDateRangeInvalid);
		}
	}

	private static AdmissionOfferingDto Map(AdmissionOffering offering) => new()
	{
		Id = offering.Id,
		AdmissionCycleId = offering.AdmissionCycleId,
		CampusId = offering.CampusId,
		BoardId = offering.BoardId,
		MediumId = offering.MediumId,
		StageId = offering.StageId,
		StandardId = offering.StandardId,
		Code = offering.Code,
		OpenDate = offering.OpenDate,
		CloseDate = offering.CloseDate,
		IsActive = offering.IsActive,
		WorkflowDefinitionId = offering.WorkflowDefinitionId,
		InquiryFormDefinitionId = offering.InquiryFormDefinitionId,
		ApplicationFormDefinitionId = offering.ApplicationFormDefinitionId,
		CreationTime = offering.CreationTime,
		CreatorId = offering.CreatorId,
		LastModificationTime = offering.LastModificationTime,
		LastModifierId = offering.LastModifierId
	};
}
