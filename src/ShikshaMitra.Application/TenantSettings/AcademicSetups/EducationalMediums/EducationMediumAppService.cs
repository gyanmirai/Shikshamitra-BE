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
	AcademicHierarchyQueryScope queryScope,
	AcademicHierarchyReader hierarchy) : CrudAppService<
	EducationMedium,
	EducationMediumDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateEducationMediumDto>(educationMediumRepository), IEducationMediumAppService

{
	public override async Task<EducationMediumDto> GetAsync(Guid id)
	{
		await CheckGetPolicyAsync();
		var query = await queryScope.MediumsAsync(await educationMediumRepository.GetQueryableAsync());
		return (await hierarchy.ReadMediumsAsync(query.Where(x => x.Id == id))).FirstOrDefault()
			?? throw new Volo.Abp.Authorization.AbpAuthorizationException();
	}

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
		await CheckGetListPolicyAsync();
		var query = await queryScope.MediumsAsync(await base.CreateFilteredQueryAsync(input));
		var total = await AsyncExecuter.CountAsync(query);
		var items = await hierarchy.ReadMediumsAsync(ApplyPaging(ApplySorting(query, input), input));
		return new(total, items);
	}
}
