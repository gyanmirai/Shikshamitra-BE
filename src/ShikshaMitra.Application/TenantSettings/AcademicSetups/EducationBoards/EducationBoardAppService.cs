using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.EducationBoards;

[Authorize]
public class EducationBoardAppService(
	IEducationBoardRepository educationBoardRepository,
	EducationBoardManager educationBoardManager) : CrudAppService<
	EducationBoard,
	EducationBoardDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateEducationBoardDto>(educationBoardRepository), IEducationBoardAppService
{
	public override async Task<EducationBoardDto> CreateAsync(CreateUpdateEducationBoardDto input)
	{
		EducationBoard educationBoard = await educationBoardManager.CreateAsync(
			input.AcademicYearId,
			input.Type
		);

		await educationBoardRepository.InsertAsync(educationBoard);
		return ObjectMapper.Map<EducationBoard, EducationBoardDto>(educationBoard);
	}

	public override async Task<EducationBoardDto> UpdateAsync(Guid id, CreateUpdateEducationBoardDto input)
	{
		EducationBoard educationBoard = await educationBoardRepository.GetAsync(id);
		await educationBoardManager.UpdateAsync(educationBoard, input.Type);
		return ObjectMapper.Map<EducationBoard, EducationBoardDto>(educationBoard);
	}

	public async override Task<PagedResultDto<EducationBoardDto>>
		GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var result = await base.GetListAsync(input);

		result.Items = result.Items.OrderBy(x => x.Type).ToList();

		foreach (var board in result.Items)
		{
			board.EducationMediums = board.EducationMediums.OrderBy(x => x.Name).ToList();

			foreach (var medium in board.EducationMediums)
			{
				medium.EducationMediumStages = medium.EducationMediumStages.OrderBy(x => x.Name).ToList();
				foreach (var stage in medium.EducationMediumStages)
				{
					stage.Standards = stage.Standards.OrderBy(x => x.Name).ToList();
					foreach (var standard in stage.Standards)
					{
						standard.Divisions = standard.Divisions.OrderBy(x => x.Name).ToList();
					}
				}
			}
		}

		return result;
	}
}