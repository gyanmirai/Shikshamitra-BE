using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.EducationBoards;

[Authorize]
public class EducationBoardAppService(
	IEducationBoardRepository educationBoardRepository,
	EducationBoardManager educationBoardManager,
	AcademicHierarchyQueryScope queryScope,
	AcademicHierarchyReader hierarchy) : CrudAppService<
	EducationBoard,
	EducationBoardDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateEducationBoardDto>(educationBoardRepository), IEducationBoardAppService
{
	[Volo.Abp.RemoteService(false)]
	public async Task<ListResultDto<EducationBoardDto>> GetHierarchyAsync(Guid academicYearId)
	{
		await CheckGetListPolicyAsync();
		if (academicYearId == Guid.Empty) throw new Volo.Abp.Validation.AbpValidationException("AcademicYearId is required.");
		return new(await hierarchy.ReadAsync(academicYearId));
	}
	public override async Task<EducationBoardDto> GetAsync(Guid id)
	{
		await CheckGetPolicyAsync();
		return (await hierarchy.ReadAsync(null, id)).FirstOrDefault()
			?? throw new Volo.Abp.Authorization.AbpAuthorizationException();
	}
	protected override async Task<IQueryable<EducationBoard>> CreateFilteredQueryAsync(PagedAndSortedResultRequestDto input) =>
		await queryScope.BoardsAsync(await base.CreateFilteredQueryAsync(input));
	protected override IQueryable<EducationBoard> ApplySorting(IQueryable<EducationBoard> query, PagedAndSortedResultRequestDto input) =>
		string.IsNullOrWhiteSpace(input.Sorting) ? query.OrderBy(x => x.Type).ThenBy(x => x.Id) : base.ApplySorting(query, input);
	public Task<System.Collections.Generic.List<EducationBoardOptionDto>> GetOptionsAsync() => Task.FromResult(
		Enum.GetValues<EducationBoardType>().Select(type => new EducationBoardOptionDto
		{
			Value = type, Label = EducationBoardNames.DisplayName(type)
		}).ToList());

	public override async Task<EducationBoardDto> CreateAsync(CreateUpdateEducationBoardDto input)
	{
		EducationBoard educationBoard = await educationBoardManager.CreateAsync(
			input.AcademicYearId,
			input.Type,
			input.StartDate,
			input.EndDate,
			input.CustomName
		);

		await educationBoardRepository.InsertAsync(educationBoard);
		return ObjectMapper.Map<EducationBoard, EducationBoardDto>(educationBoard);
	}

	public override async Task<EducationBoardDto> UpdateAsync(Guid id, CreateUpdateEducationBoardDto input)
	{
		EducationBoard educationBoard = await educationBoardRepository.GetAsync(id);
		await educationBoardManager.UpdateAsync(educationBoard, input.Type, input.StartDate, input.EndDate, input.CustomName);
		return ObjectMapper.Map<EducationBoard, EducationBoardDto>(educationBoard);
	}

	public async override Task<PagedResultDto<EducationBoardDto>>
		GetListAsync(PagedAndSortedResultRequestDto input)
	{
		await CheckGetListPolicyAsync();
		var query = await CreateFilteredQueryAsync(input);
		var total = await AsyncExecuter.CountAsync(query);
		var items = await hierarchy.ReadBoardsAsync(ApplyPaging(ApplySorting(query, input), input));
		return new(total, items);
	}
}
