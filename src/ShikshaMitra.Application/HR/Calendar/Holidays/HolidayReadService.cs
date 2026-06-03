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
		var query = (await holidayRepository.GetQueryableAsync())
			.WhereIf(!search.IsNullOrWhiteSpace(), x => x.HolidayName.Contains(search!))
			.WhereIf(input.AcademicYearId.HasValue, x => x.AcademicYearId == input.AcademicYearId)
			.WhereIf(input.TypeId.HasValue, x => x.HolidayTypeId == input.TypeId)
			.WhereIf(input.FromDate.HasValue, x => x.StartDate >= input.FromDate)
			.WhereIf(input.ToDate.HasValue, x => x.EndDate <= input.ToDate);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var queryWithSort = ApplySorting(query, input.Sorting);
		var items = await AsyncExecuter.ToListAsync(
			queryWithSort
				.Skip(input.SkipCount)
				.Take(input.MaxResultCount));

		var holidayDtos = ObjectMapper.Map<List<Holiday>, List<HolidayDto>>(items);
		ApplyStatuses(items, holidayDtos);
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

	public async Task<PagedResultDto<HolidayDto>> GetByStatusAsync(GetHolidayListRequestDto input)
	{
		if (!input.Status.HasValue)
		{
			return new PagedResultDto<HolidayDto>(0, new List<HolidayDto>());
		}

		var search = input.Search;
		var query = (await holidayRepository.GetQueryableAsync())
			.WhereIf(!search.IsNullOrWhiteSpace(), x => x.HolidayName.Contains(search!));

		var allHolidays = await AsyncExecuter.ToListAsync(query);
		var statusValue = (HolidayStatus)input.Status.Value;
		var now = CurrentInstant();
		var filtered = allHolidays.Where(x => x.GetStatus(now) == statusValue).ToList();
		var items = filtered
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount)
			.ToList();

		var holidayDtos = ObjectMapper.Map<List<Holiday>, List<HolidayDto>>(items);
		ApplyStatuses(items, holidayDtos, now);
		return new PagedResultDto<HolidayDto>(
			filtered.Count,
			holidayDtos);
	}

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

	private IQueryable<Holiday> ApplySorting(IQueryable<Holiday> query, string? sorting)
	{
		var sortingProperty = sorting ?? nameof(Holiday.HolidayName);
		var now = CurrentInstant();
		return sortingProperty switch
		{
			"startDate" => query.OrderBy(x => x.StartDate),
			"endDate" => query.OrderBy(x => x.EndDate),
			"status" => query.AsEnumerable().OrderBy(x => x.GetStatus(now)).AsQueryable(),
			_ => query.OrderBy(x => x.HolidayName)
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
