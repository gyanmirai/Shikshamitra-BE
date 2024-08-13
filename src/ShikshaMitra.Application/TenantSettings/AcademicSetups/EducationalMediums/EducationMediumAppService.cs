using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Onboarding.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.EducationMediums;

[Authorize]
public class EducationMediumAppService(
	IEducationMediumRepository educationMediumRepository,
	EducationMediumManager educationMediumManager) : CrudAppService<
	EducationMedium,
	EducationMediumDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateEducationMediumDto>(educationMediumRepository), IEducationMediumAppService

{
	public override async Task<EducationMediumDto> CreateAsync(CreateUpdateEducationMediumDto input)
	{
		EducationMedium educationMedium = await educationMediumManager.CreateAsync(
			input.EducationBoardId,
			input.Name
		);

		await educationMediumRepository.InsertAsync(educationMedium);
		return ObjectMapper.Map<EducationMedium, EducationMediumDto>(educationMedium);
	}

	public override async Task<EducationMediumDto> UpdateAsync(Guid id, CreateUpdateEducationMediumDto input)
	{
		EducationMedium educationMedium = await educationMediumRepository.GetAsync(id);
		await educationMediumManager.UpdateAsync(educationMedium, input.Name);
		return ObjectMapper.Map<EducationMedium, EducationMediumDto>(educationMedium);
	}
}