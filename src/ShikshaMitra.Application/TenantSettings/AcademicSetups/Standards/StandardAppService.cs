using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.Standards;

[Authorize]
public class StandardAppService(
	IStandardRepository standardRepository,
	IEducationMediumStageRepository educationMediumRepository,
	StandardManager standardManager) : CrudAppService<
	Standard,
	StandardDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateStandardDto>(standardRepository), IStandardAppService
{
	public override async Task<StandardDto> CreateAsync(CreateUpdateStandardDto input)
	{
		Standard standard = await standardManager.CreateAsync(
			input.EducationMediumStageId,
			input.Name,
			input.AgeLowerLimit,
			input.ShiftId
		);

		await standardRepository.InsertAsync(standard);
		return ObjectMapper.Map<Standard, StandardDto>(standard);
	}

	public override async Task<StandardDto> UpdateAsync(Guid id, CreateUpdateStandardDto input)
	{
		Standard standard = await standardRepository.GetAsync(id);
		await standardManager.UpdateAsync(standard, input.Name, input.AgeLowerLimit, input.ShiftId);
		return ObjectMapper.Map<Standard, StandardDto>(standard);
	}

	[RemoteService(false)]
	public async Task<List<StandardDto>> GetStandardsByEducationMediumId(Guid educationMediumId)
	{
		List<EducationMediumStage> educationMediumsStages =
			await educationMediumRepository.GetEducationMediumStagesByEducationMediumIdAsync(educationMediumId);

		educationMediumsStages = educationMediumsStages.OrderBy(x => x.Name).ToList();

		if (educationMediumsStages.Count == 0)
		{
			return [];
		}

		// Find out all standards for the given education medium stage

		List<Standard> standards = [];

		foreach (EducationMediumStage educationMediumStage in educationMediumsStages)
		{
			List<Standard> standardsForEducationMedium =
				await standardRepository.GetStandardsByEducationMediumStageIdAsync(
					educationMediumStage.Id
				);

			// sort divisions by it's name for each standard
			foreach (Standard standard in standardsForEducationMedium)
			{
				// sort divisions in standard using orderby
				standard.Divisions = standard.Divisions.OrderBy(x => x.Name).ToList();
				standards.Add(standard);
			}
		}

		standards = standards.OrderBy(x => x.CreationTime).ToList();

		return ObjectMapper.Map<List<Standard>, List<StandardDto>>(standards);
	}
}