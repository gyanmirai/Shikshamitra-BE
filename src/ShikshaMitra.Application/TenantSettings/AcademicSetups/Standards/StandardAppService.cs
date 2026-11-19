using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Divisions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Uow;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.Standards;

[Authorize]
public class StandardAppService(
	IStandardRepository standardRepository,
	IStudentRepository studentRepository,
	IEducationMediumStageRepository educationMediumRepository,
	StandardManager standardManager,
	AcademicOrganizationScopeAccessService scopeAccessService,
	AcademicReorderAccessService reorderAccessService,
	AcademicHierarchyQueryScope queryScope,
	AcademicHierarchyReader hierarchy,
	ShiftReferenceValidator shiftReferenceValidator) : CrudAppService<
	Standard,
	StandardDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateStandardDto>(standardRepository), IStandardAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit)]
	[UnitOfWork(true)]
	public async Task ReorderAsync(Guid educationMediumStageId, ReorderAcademicChildrenDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit);
		var siblings = await standardRepository.GetListAsync(x => x.EducationMediumStageId == educationMediumStageId
			&& x.TenantId == CurrentTenant.Id);
		await reorderAccessService.CheckStandardsAsync(educationMediumStageId, siblings);
		standardManager.Reorder(siblings, input.Ids);
		await standardRepository.UpdateManyAsync(siblings, autoSave: true);
	}

	protected override IQueryable<Standard> ApplySorting(IQueryable<Standard> query, PagedAndSortedResultRequestDto input) =>
		string.IsNullOrWhiteSpace(input.Sorting)
			? query.OrderBy(x => x.EducationMediumStage.EducationMediumId).ThenBy(x => x.EducationMediumStage.SortOrder)
				.ThenBy(x => x.EducationMediumStage.Name).ThenBy(x => x.EducationMediumStageId)
				.ThenBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id)
			: base.ApplySorting(query, input);

	public override async Task<PagedResultDto<StandardDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		await CheckGetListPolicyAsync();
		var query = await queryScope.StandardsAsync(await base.CreateFilteredQueryAsync(input));
		var total = await AsyncExecuter.CountAsync(query);
		return new(total, await hierarchy.ReadStandardsAsync(ApplyPaging(ApplySorting(query, input), input)));
	}
	public async Task<ListResultDto<StandardLookupDto>> GetLookupAsync()
	{
		await CheckGetListPolicyAsync();
		var query = await queryScope.StandardsAsync(await base.CreateFilteredQueryAsync(new PagedAndSortedResultRequestDto()));
		var result = await AsyncExecuter.ToListAsync(ApplySorting(query, new PagedAndSortedResultRequestDto())
			.Select(standard => new StandardLookupDto { Id = standard.Id, Name = standard.Name }));
		if (result.Count == 0) return new(result);
		var standardIds = result.Select(standard => standard.Id).ToArray();
		var divisionRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<Division, Guid>>();
		var divisionQuery = await queryScope.DivisionsAsync(await divisionRepository.GetQueryableAsync());
		var children = await AsyncExecuter.ToListAsync(divisionQuery.Where(division => Enumerable.Contains(standardIds, division.StandardId))
			.OrderBy(division => division.SortOrder).ThenBy(division => division.Name).ThenBy(division => division.Id)
			.Select(division => new { division.StandardId, division.Id, division.Name }));
		var byStandard = children.ToLookup(division => division.StandardId);
		foreach (var standard in result)
			standard.Divisions = byStandard[standard.Id].Select(division => new StandardDivisionLookupDto { Id = division.Id, Name = division.Name }).ToList();
		return new(result);
	}
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
		// Project each level instead of materializing the circular academic auto-includes.
		var stages = await queryScope.StagesAsync(await educationMediumRepository.GetQueryableAsync());
		var stageIds = await AsyncExecuter.ToListAsync(stages
			.Where(stage => stage.EducationMediumId == educationMediumId
				&& stage.EducationMedium.EducationBoard.AcademicYear.TenantId == CurrentTenant.Id
				&& !stage.EducationMedium.EducationBoard.AcademicYear.IsDeleted).Select(stage => stage.Id));
		if (stageIds.Count == 0) return [];

		var standards = await queryScope.StandardsAsync(await standardRepository.GetQueryableAsync());
		var standardDtos = await AsyncExecuter.ToListAsync(standards
			.Where(standard => Enumerable.Contains(stageIds, standard.EducationMediumStageId))
			.OrderBy(standard => standard.EducationMediumStage.SortOrder)
			.ThenBy(standard => standard.EducationMediumStage.Name).ThenBy(standard => standard.EducationMediumStageId)
			.ThenBy(standard => standard.SortOrder).ThenBy(standard => standard.Name).ThenBy(standard => standard.Id)
			.Select(standard => new StandardDto {
				Id = standard.Id, Name = standard.Name, SortOrder = standard.SortOrder,
				AgeLowerLimit = standard.AgeLowerLimit, EducationMediumStageId = standard.EducationMediumStageId,
				ShiftId = standard.ShiftId, CreationTime = standard.CreationTime, CreatorId = standard.CreatorId,
				LastModificationTime = standard.LastModificationTime, LastModifierId = standard.LastModifierId,
				// Shift has no audit columns; preserve the legacy DTO's default audit values.
				Shift = standard.Shift == null ? null! : new ShiftManagementDto {
					Id = standard.Shift.Id, ShiftName = standard.Shift.ShiftName,
					StartTime = standard.Shift.StartTime, EndTime = standard.Shift.EndTime },
				Divisions = new List<DivisionDto>() }));
		if (standardDtos.Count == 0) return standardDtos;

		var standardIds = standardDtos.Select(standard => standard.Id).ToArray();
		var divisions = await queryScope.DivisionsAsync(await LazyServiceProvider
			.LazyGetRequiredService<IRepository<Division, Guid>>().GetQueryableAsync());
		var divisionDtos = await AsyncExecuter.ToListAsync(divisions
			.Where(division => Enumerable.Contains(standardIds, division.StandardId))
			.OrderBy(division => division.SortOrder).ThenBy(division => division.Name).ThenBy(division => division.Id)
			.Select(AcademicHierarchyProjection.Division));
		var divisionsByStandard = divisionDtos.ToLookup(division => division.StandardId);
		foreach (var standard in standardDtos) standard.Divisions = divisionsByStandard[standard.Id].ToList();
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
		var occupancy = await AsyncExecuter.ToListAsync(
			(await studentRepository.GetQueryableWithoutAutoIncludesAsync())
				.Where(s => s.DivisionId.HasValue
					&& Enumerable.Contains(divisionIds, s.DivisionId.Value)
					&& s.Status == StudentLifeCycleStatusEnum.Enrolled)
				.GroupBy(s => s.DivisionId!.Value)
				.Select(group => new
				{
					DivisionId = group.Key,
					Current = group.Count(),
					Male = group.Count(s => s.AdditionalDetail != null && s.AdditionalDetail.Gender == GenderEnum.Male),
					Female = group.Count(s => s.AdditionalDetail != null && s.AdditionalDetail.Gender == GenderEnum.Female)
				}));
		var countsByDivision = occupancy.ToDictionary(row => row.DivisionId);

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
