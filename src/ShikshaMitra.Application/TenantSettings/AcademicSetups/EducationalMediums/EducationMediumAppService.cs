using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationalMediums;

[Authorize]
public class EducationMediumAppService(
	IEducationMediumRepository educationMediumRepository,
	EducationMediumManager educationMediumManager,
	AcademicOrganizationScopeAccessService scopeAccessService) : CrudAppService<
	EducationMedium,
	EducationMediumDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateEducationMediumDto>(educationMediumRepository), IEducationMediumAppService

{
	public override async Task<EducationMediumDto> CreateAsync(CreateUpdateEducationMediumDto input)
	{
		var educationMedium = await educationMediumManager.CreateAsync(
			input.EducationBoardId,
			input.Name
		);

		await educationMediumRepository.InsertAsync(educationMedium);
		return ObjectMapper.Map<EducationMedium, EducationMediumDto>(educationMedium);
	}

	public override async Task<EducationMediumDto> UpdateAsync(Guid id, CreateUpdateEducationMediumDto input)
	{
		var educationMedium = await educationMediumRepository.GetAsync(id);
		await educationMediumManager.UpdateAsync(educationMedium, input.Name);
		return ObjectMapper.Map<EducationMedium, EducationMediumDto>(educationMedium);
	}

	public override async Task<PagedResultDto<EducationMediumDto>> GetListAsync(
		PagedAndSortedResultRequestDto input)
	{
		var result = await base.GetListAsync(input);

		result.Items = (await scopeAccessService.FilterMediumsAsync(result.Items)).ToList();
		result.TotalCount = result.Items.Count;

		return result;
	}
}
