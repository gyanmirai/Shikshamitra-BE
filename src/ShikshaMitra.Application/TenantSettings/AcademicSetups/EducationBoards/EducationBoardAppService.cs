using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicSetups;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

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
}