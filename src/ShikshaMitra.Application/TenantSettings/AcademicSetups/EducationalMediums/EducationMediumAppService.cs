using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
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
	IUserScopeService userScopeService) : CrudAppService<
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

		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id!.Value);
		if (!scope.IsAdmin && !scope.HasTenantWideScope())
		{
			result.Items = result.Items
				.Where(m =>
					scope.WildcardMediumBoardIds.Contains(m.EducationBoardId)
					|| scope.MediumIds.Contains(m.Id)
					|| scope.WildcardMediumStageMediumIds.Contains(m.Id))
				.ToList();
			result.TotalCount = result.Items.Count;
		}

		return result;
	}
}