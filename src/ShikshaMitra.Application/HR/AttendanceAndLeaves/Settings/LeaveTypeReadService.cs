using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeaveTypeReadService(
	IRepository<LeaveType, Guid> repository,
	ILeaveTypeRepository leaveTypeRepository,
	LeaveTypeCacheService cacheService,
	IAsyncQueryableExecuter asyncExecuter) : ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<LeaveTypeDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var queryable = await repository.GetQueryableAsync();
		queryable = ApplySorting(queryable, input.Sorting ?? "Type ASC");
		var totalCount = await asyncExecuter.CountAsync(queryable);
		var items = await asyncExecuter.ToListAsync(queryable.Skip(input.SkipCount).Take(input.MaxResultCount));

		return new PagedResultDto<LeaveTypeDto>(
			totalCount,
			ObjectMapper.Map<List<LeaveType>, List<LeaveTypeDto>>(items));
	}

	public async Task<PagedResultDto<LeaveTypeDto>> GetAllActiveAsync(PagedResultRequestDto input)
	{
		var cached = await cacheService.GetActiveAsync(input.SkipCount, input.MaxResultCount);
		if (cached != null)
		{
			return cached;
		}

		var queryable = await repository.GetQueryableAsync();
		var query = queryable.Where(x => x.IsActive);
		var totalCount = await asyncExecuter.CountAsync(query);
		var items = await asyncExecuter.ToListAsync(
			query.OrderBy(x => x.Type).Skip(input.SkipCount).Take(input.MaxResultCount));

		var result = new PagedResultDto<LeaveTypeDto>(
			totalCount,
			ObjectMapper.Map<List<LeaveType>, List<LeaveTypeDto>>(items));

		await cacheService.SetActiveAsync(input.SkipCount, input.MaxResultCount, result);

		return result;
	}

	public async Task<LeaveTypeListResponse> GetListByAcademicYearIdAsync(
		Guid academicYearId,
		int page,
		int pageSize,
		string? search = null,
		CancellationToken cancellationToken = default)
	{
		Check.NotNull(academicYearId, nameof(academicYearId));
		Check.Range(page, nameof(page), 1, int.MaxValue);
		Check.Range(pageSize, nameof(pageSize), 1, 100);

		var cached = await cacheService.GetContractAsync(academicYearId, page, pageSize, search, cancellationToken);
		if (cached != null)
		{
			return cached;
		}

		var skipCount = (page - 1) * pageSize;
		var items = await leaveTypeRepository.GetListByAcademicYearIdAsync(
			academicYearId,
			skipCount,
			pageSize,
			sorting: "Type ASC",
			search: search,
			includeInactive: false,
			cancellationToken: cancellationToken);

		var totalCount = await leaveTypeRepository.GetCountByAcademicYearIdAsync(
			academicYearId,
			search: search,
			includeInactive: false,
			cancellationToken: cancellationToken);

		var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
		var response = new LeaveTypeListResponse
		{
			TotalCount = (int)totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = Math.Max(1, totalPages),
			Items = items.Select(ToItem).ToList()
		};

		await cacheService.SetContractAsync(academicYearId, page, pageSize, search, response, cancellationToken);

		return response;
	}

	public static LeaveTypeResponse ToResponse(LeaveType leaveType) =>
		new()
		{
			Id = leaveType.Id,
			Name = leaveType.Type,
			Description = leaveType.Description,
			Color = leaveType.Color
		};

	public static LeaveTypeDeleteResponse ToDeleteResponse(Guid leaveTypeId) =>
		new()
		{
			Id = leaveTypeId,
			Message = "Leave type deleted successfully"
		};

	private static LeaveTypeItem ToItem(LeaveType leaveType) =>
		new()
		{
			Id = leaveType.Id,
			Name = leaveType.Type,
			Description = leaveType.Description,
			Color = leaveType.Color
		};

	private static IQueryable<LeaveType> ApplySorting(IQueryable<LeaveType> queryable, string sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting))
		{
			return queryable.OrderBy(x => x.Type);
		}

		var sortingParts = sorting.Split(',');
		IOrderedQueryable<LeaveType>? orderedQueryable = null;
		var isFirstSort = true;

		foreach (var sortPart in sortingParts)
		{
			var parts = sortPart.Trim().Split(' ');
			var property = parts[0];
			var direction = parts.Length > 1 ? parts[1].ToUpperInvariant() : "ASC";

			if (isFirstSort)
			{
				orderedQueryable = ApplyPrimarySort(queryable, property, direction);
				isFirstSort = false;
				continue;
			}

			orderedQueryable = ApplyThenSort(orderedQueryable!, property, direction);
		}

		return orderedQueryable ?? queryable.OrderBy(x => x.Type);
	}

	private static IOrderedQueryable<LeaveType> ApplyPrimarySort(
		IQueryable<LeaveType> queryable,
		string property,
		string direction) =>
		property.ToLowerInvariant() switch
		{
			"type" => direction == "DESC" ? queryable.OrderByDescending(x => x.Type) : queryable.OrderBy(x => x.Type),
			"color" => direction == "DESC" ? queryable.OrderByDescending(x => x.Color) : queryable.OrderBy(x => x.Color),
			"isactive" => direction == "DESC" ? queryable.OrderByDescending(x => x.IsActive) : queryable.OrderBy(x => x.IsActive),
			_ => queryable.OrderBy(x => x.Type)
		};

	private static IOrderedQueryable<LeaveType> ApplyThenSort(
		IOrderedQueryable<LeaveType> queryable,
		string property,
		string direction) =>
		property.ToLowerInvariant() switch
		{
			"type" => direction == "DESC" ? queryable.ThenByDescending(x => x.Type) : queryable.ThenBy(x => x.Type),
			"color" => direction == "DESC" ? queryable.ThenByDescending(x => x.Color) : queryable.ThenBy(x => x.Color),
			"isactive" => direction == "DESC" ? queryable.ThenByDescending(x => x.IsActive) : queryable.ThenBy(x => x.IsActive),
			_ => queryable.ThenBy(x => x.Type)
		};
}
