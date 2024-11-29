using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationalMediums;

[Authorize]
public class EducationMediumStageAppService(
	IEducationMediumStageRepository educationMediumStageRepository,
	EducationMediumStageManager educationMediumStageManager) : CrudAppService<
	EducationMediumStage,
	EducationMediumStageDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateEducationMediumStageDto>(educationMediumStageRepository), IEducationMediumStageAppService
{
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
}