using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings.Standards;

[Authorize]
public class StandardAppService(
	IStandardRepository standardRepository,
	IStudentRepository studentRepository,
	IEducationMediumStageRepository educationMediumRepository,
	StandardManager standardManager,
	AcademicOrganizationScopeAccessService scopeAccessService,
	ShiftReferenceValidator shiftReferenceValidator) : CrudAppService<
	Standard,
	StandardDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateStandardDto>(standardRepository), IStandardAppService
{
	public override async Task<StandardDto> CreateAsync(CreateUpdateStandardDto input)
	{
		await shiftReferenceValidator.EnsureExistsAsync(input.ShiftId);

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
		await shiftReferenceValidator.EnsureExistsAsync(input.ShiftId);

		Standard standard = await standardRepository.GetAsync(id);
		await standardManager.UpdateAsync(standard, input.Name, input.AgeLowerLimit, input.ShiftId);
		return ObjectMapper.Map<Standard, StandardDto>(standard);
	}

	public override async Task<StandardDto> GetAsync(Guid id)
	{
		var standard = await standardRepository.GetAsync(id, includeDetails: true);
		var visibleStandards = await scopeAccessService.FilterStandardsAsync([standard]);
		if (visibleStandards.Count == 0)
		{
			throw new AbpAuthorizationException("You are not authorized to access this standard.");
		}

		var dto = ObjectMapper.Map<Standard, StandardDto>(visibleStandards[0]);
		await EnrichDivisionOccupancyAsync(dto.Divisions);
		await scopeAccessService.ResolveRoomIdsAsync(dto.Divisions);
		return dto;
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

		standards = await scopeAccessService.FilterStandardsAsync(standards);
		standards = StandardAcademicOrder.Sort(standards, x => x.Name, x => x.AgeLowerLimit, x => x.Id);

		var standardDtos = ObjectMapper.Map<List<Standard>, List<StandardDto>>(standards);
		await EnrichDivisionOccupancyAsync(standardDtos.SelectMany(s => s.Divisions ?? []).ToList());
		return standardDtos;
	}

	private async Task EnrichDivisionOccupancyAsync(IReadOnlyList<DivisionDto>? divisions)
	{
		if (divisions == null || divisions.Count == 0)
		{
			return;
		}

		var divisionIds = divisions.Select(d => d.Id).Distinct().ToList();
		var studentRows = await AsyncExecuter.ToListAsync(
			(await studentRepository.GetQueryableWithoutAutoIncludesAsync())
				.Where(s => s.DivisionId.HasValue
					&& divisionIds.Contains(s.DivisionId.Value)
					&& s.Status == StudentLifeCycleStatusEnum.Enrolled)
				.Select(s => new
				{
					DivisionId = s.DivisionId!.Value,
					Gender = s.AdditionalDetail != null ? s.AdditionalDetail.Gender : null
				}));

		var countsByDivision = studentRows
			.GroupBy(s => s.DivisionId)
			.ToDictionary(
				g => g.Key,
				g => new
				{
					Current = g.Count(),
					Male = g.Count(x => x.Gender == GenderEnum.Male),
					Female = g.Count(x => x.Gender == GenderEnum.Female)
				});

		foreach (var division in divisions)
		{
			countsByDivision.TryGetValue(division.Id, out var counts);
			division.CurrentStudentCount = counts?.Current ?? 0;
			division.HeadCount = division.CurrentStudentCount;
			division.MaleCount = counts?.Male ?? 0;
			division.FemaleCount = counts?.Female ?? 0;
			division.AvailableSeats = Math.Max(division.Strength - division.CurrentStudentCount, 0);
		}
	}
}
