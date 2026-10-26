using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationalMediums;

[Authorize]
public class EducationMediumStageAppService(
	IEducationMediumStageRepository educationMediumStageRepository,
	EducationMediumStageManager educationMediumStageManager,
	AcademicOrganizationScopeAccessService scopeAccessService,
	AcademicReorderAccessService reorderAccessService) : CrudAppService<
	EducationMediumStage,
	EducationMediumStageDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateEducationMediumStageDto>(educationMediumStageRepository), IEducationMediumStageAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit)]
	[UnitOfWork(true)]
	public async Task ReorderAsync(Guid educationMediumId, ReorderAcademicChildrenDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit);
		var siblings = await educationMediumStageRepository.GetListAsync(x => x.EducationMediumId == educationMediumId
			&& x.TenantId == CurrentTenant.Id);
		await reorderAccessService.CheckStagesAsync(educationMediumId, siblings);
		educationMediumStageManager.Reorder(siblings, input.Ids);
		await educationMediumStageRepository.UpdateManyAsync(siblings, autoSave: true);
	}

	protected override IQueryable<EducationMediumStage> ApplySorting(IQueryable<EducationMediumStage> query, PagedAndSortedResultRequestDto input) =>
		string.IsNullOrWhiteSpace(input.Sorting)
			? query.OrderBy(x => x.EducationMediumId).ThenBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id)
			: base.ApplySorting(query, input);

	public override async Task<EducationMediumStageDto> CreateAsync(CreateUpdateEducationMediumStageDto input)
	{
		EducationMediumStage? educationMediumStage = await educationMediumStageManager.CreateAsync(
			input.EducationMediumId,
			input.Name
		);

		await educationMediumStageRepository.InsertAsync(educationMediumStage);
		return ObjectMapper.Map<EducationMediumStage, EducationMediumStageDto>(educationMediumStage);
	}

	public override async Task<EducationMediumStageDto> UpdateAsync(Guid id, CreateUpdateEducationMediumStageDto input)
	{
		EducationMediumStage? educationMediumStage = await educationMediumStageRepository.GetAsync(id);
		await educationMediumStageManager.UpdateAsync(educationMediumStage, input.Name);
		return ObjectMapper.Map<EducationMediumStage, EducationMediumStageDto>(educationMediumStage);
	}

	public override async Task<PagedResultDto<EducationMediumStageDto>> GetListAsync(
		PagedAndSortedResultRequestDto input)
	{
		var result = await base.GetListAsync(input);
		result.Items = (await scopeAccessService.FilterStagesAsync(result.Items)).ToList();
		result.TotalCount = result.Items.Count;
		return result;
	}
}
