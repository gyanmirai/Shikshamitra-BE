using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp;

namespace ShikshaMitra.Application.HR.Calendar.Holidays;

[RemoteService(false)]
public class HolidayReadService(
	IHolidayRepository holidayRepository,
	IHolidayTypeRepository holidayTypeRepository)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<HolidayDto>> GetListAsync(GetHolidayListRequestDto input)
	{
		var search = input.Search;
		var now = CurrentInstant();
		var query = (await holidayRepository.GetQueryableAsync())
			.WhereIf(!search.IsNullOrWhiteSpace(), x => x.HolidayName.Contains(search!))
			.WhereIf(input.AcademicYearId.HasValue, x => x.AcademicYearId == input.AcademicYearId)
			.WhereIf(input.TypeId.HasValue, x => x.HolidayTypeId == input.TypeId)
			.WhereIf(input.FromDate.HasValue, x => x.StartDate >= input.FromDate)
			.WhereIf(input.ToDate.HasValue, x => x.EndDate <= input.ToDate);

		List<Holiday> items;
		int totalCount;
		if (input.Status.HasValue || string.Equals(input.Sorting, "status", StringComparison.OrdinalIgnoreCase))
		{
			var filtered = await AsyncExecuter.ToListAsync(query);
			if (input.Status.HasValue)
			{
				filtered = filtered.Where(x => (int)x.GetStatus(now) == input.Status.Value).ToList();
			}

			totalCount = filtered.Count;
			items = ApplySorting(filtered, input.Sorting, now)
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount)
				.ToList();
		}
		else
		{
			totalCount = await AsyncExecuter.CountAsync(query);
			items = await AsyncExecuter.ToListAsync(
				ApplySorting(query, input.Sorting)
					.Skip(input.SkipCount)
					.Take(input.MaxResultCount));
		}

		var holidayDtos = ObjectMapper.Map<List<Holiday>, List<HolidayDto>>(items);
		ApplyStatuses(items, holidayDtos, now);
		await PopulateHolidayTypesAsync(holidayDtos);

		return new PagedResultDto<HolidayDto>(totalCount, holidayDtos);
	}

	public Task<PagedResultDto<HolidayDto>> GetListByNameAsync(GetHolidayListRequestDto input) =>
		GetListAsync(input);

	public async Task<List<HolidayDto>> GetAllHolidaysAsync()
	{
		var holidays = await holidayRepository.GetAllAsync();
		var holidayDtos = ObjectMapper.Map<List<Holiday>, List<HolidayDto>>(holidays);
		ApplyStatuses(holidays, holidayDtos);
		return holidayDtos;
	}

	public async Task<HolidayDto> GetAsync(Guid id)
	{
		var entity = await holidayRepository.GetAsync(id);
		var holidayDto = ObjectMapper.Map<Holiday, HolidayDto>(entity);
		holidayDto.Status = (int)entity.GetStatus(CurrentInstant());
		await PopulateHolidayTypeAsync(holidayDto);
		return holidayDto;
	}

	public async Task<List<HolidayDto>> GetByTypeAsync(Guid typeId)
	{
		var holidays = await holidayRepository.GetByTypeAsync(typeId);
		var holidayDtos = ObjectMapper.Map<List<Holiday>, List<HolidayDto>>(holidays);
		ApplyStatuses(holidays, holidayDtos);
		return holidayDtos;
	}

	public Task<PagedResultDto<HolidayDto>> GetByStatusAsync(GetHolidayListRequestDto input) =>
		GetListAsync(input);

	public async Task PopulateHolidayTypeAsync(HolidayDto holidayDto)
	{
		var holidayType = await holidayTypeRepository.GetAsync(holidayDto.HolidayTypeId);
		holidayDto.HolidayType = ObjectMapper.Map<HolidayType, HolidayTypeDto>(holidayType);
	}

	private async Task PopulateHolidayTypesAsync(List<HolidayDto> holidayDtos)
	{
		var typeIds = holidayDtos.Select(h => h.HolidayTypeId).Distinct().ToList();
		var holidayTypes = await AsyncExecuter.ToListAsync(
			(await holidayTypeRepository.GetQueryableAsync()).Where(ht => typeIds.Contains(ht.Id)));
		var typeMap = holidayTypes.ToDictionary(ht => ht.Id, ht => ObjectMapper.Map<HolidayType, HolidayTypeDto>(ht));

		foreach (var holidayDto in holidayDtos)
		{
			if (typeMap.TryGetValue(holidayDto.HolidayTypeId, out var htDto))
			{
				holidayDto.HolidayType = htDto;
			}
		}
	}

	private static IOrderedQueryable<Holiday> ApplySorting(
		IQueryable<Holiday> holidays,
		string? sorting)
	{
		var sortingProperty = sorting ?? nameof(Holiday.HolidayName);
		return sortingProperty switch
		{
			"startDate" => holidays.OrderBy(x => x.StartDate),
			"endDate" => holidays.OrderBy(x => x.EndDate),
			_ => holidays.OrderBy(x => x.HolidayName)
		};
	}

	private static IOrderedEnumerable<Holiday> ApplySorting(
		IEnumerable<Holiday> holidays,
		string? sorting,
		DateTimeOffset now)
	{
		var sortingProperty = sorting ?? nameof(Holiday.HolidayName);
		return sortingProperty switch
		{
			"startDate" => holidays.OrderBy(x => x.StartDate),
			"endDate" => holidays.OrderBy(x => x.EndDate),
			"status" => holidays.OrderBy(x => x.GetStatus(now)),
			_ => holidays.OrderBy(x => x.HolidayName)
		};
	}

	private void ApplyStatuses(IReadOnlyList<Holiday> holidays, IReadOnlyList<HolidayDto> holidayDtos, DateTimeOffset? now = null)
	{
		var effectiveNow = now ?? CurrentInstant();
		for (var i = 0; i < holidays.Count && i < holidayDtos.Count; i++)
		{
			holidayDtos[i].Status = (int)holidays[i].GetStatus(effectiveNow);
		}
	}

	private DateTimeOffset CurrentInstant() =>
		new(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
}
