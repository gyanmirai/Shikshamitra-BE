using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicOrganizationOptions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Validation;

namespace ShikshaMitra.TenantSettings.AcademicSetups.AcademicOrganizationOptions;

[Authorize]
public class AcademicOrganizationOptionsAppService(
	IRepository<EducationBoard, Guid> boardRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationMediumStage, Guid> stageRepository,
	AcademicOrganizationScopeAccessService scopeAccessService)
	: ApplicationService, IAcademicOrganizationOptionsAppService
{
	public async Task<ListResultDto<AcademicOrganizationOptionDto>> GetListAsync(
		AcademicOrganizationOptionsInput input)
	{
		if (input.AcademicYearId == Guid.Empty)
		{
			throw new AbpValidationException(
				"AcademicYearId is required.",
				[
					new System.ComponentModel.DataAnnotations.ValidationResult(
						"AcademicYearId is required.",
						[nameof(input.AcademicYearId)])
				]);
		}

		var boardQuery = await boardRepository.GetQueryableAsync();
		var mediumQuery = await mediumRepository.GetQueryableAsync();
		var stageQuery = await stageRepository.GetQueryableAsync();

		var rows = await AsyncExecuter.ToListAsync(
			from stage in stageQuery
			join medium in mediumQuery on stage.EducationMediumId equals medium.Id
			join board in boardQuery on medium.EducationBoardId equals board.Id
			where board.AcademicYearId == input.AcademicYearId
			select new AcademicOrganizationOptionRow(
				stage.Id,
				stage.Name,
				medium.Id,
				medium.Name,
				board.Id,
				board.Type.ToString(),
				board.AcademicYearId));

		var scopedRows = new List<AcademicOrganizationOptionRow>();
		var boardNames = (await boardRepository.GetListAsync(board => board.AcademicYearId == input.AcademicYearId))
			.ToDictionary(board => board.Id, board => board.Name);
		foreach (var row in rows)
		{
			if (await scopeAccessService.CanAccessMediumStageAsync(
				row.StageId,
				row.MediumId,
				row.BoardId,
				row.AcademicYearId))
			{
				scopedRows.Add(row with { BoardName = boardNames.GetValueOrDefault(row.BoardId, row.BoardName) });
			}
		}

		var items = scopedRows
			.OrderBy(x => x.BoardName)
			.ThenBy(x => x.MediumName)
			.ThenBy(x => x.StageName)
			.Select(x => new AcademicOrganizationOptionDto
			{
				Id = x.StageId,
				Label = $"{x.BoardName} - {x.MediumName} - {x.StageName}",
				StageName = x.StageName,
				MediumId = x.MediumId,
				MediumName = x.MediumName,
				BoardId = x.BoardId,
				BoardName = x.BoardName,
				AcademicYearId = x.AcademicYearId
			})
			.ToList();

		return new ListResultDto<AcademicOrganizationOptionDto>(items);
	}

	private sealed record AcademicOrganizationOptionRow(
		Guid StageId,
		string StageName,
		Guid MediumId,
		string MediumName,
		Guid BoardId,
		string BoardName,
		Guid AcademicYearId);
}
