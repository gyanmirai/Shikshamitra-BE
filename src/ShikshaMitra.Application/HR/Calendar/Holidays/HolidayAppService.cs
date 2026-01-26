using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Application.HR.Calendar.Holidays;

/// <summary>
/// Application service for Holiday CRUD operations with filtering
/// </summary>
public class HolidayAppService : CrudAppService<
    Holiday,
    HolidayDto,
    Guid,
    GetHolidayListRequestDto,
    CreateUpdateHolidayDto>,
    IHolidayAppService
{
    private readonly IHolidayRepository _holidayRepository;

    public HolidayAppService(IRepository<Holiday, Guid> repository, IHolidayRepository holidayRepository)
        : base(repository)
    {
        _holidayRepository = holidayRepository;
        GetPolicyName = "Holiday.View";
        GetListPolicyName = "Holiday.List";
        CreatePolicyName = "Holiday.Create";
        UpdatePolicyName = "Holiday.Edit";
        DeletePolicyName = "Holiday.Delete";
    }

    /// <summary>
    /// Override GetListAsync to implement custom filtering
    /// </summary>
    public override async Task<PagedResultDto<HolidayDto>> GetListAsync(GetHolidayListRequestDto input)
    {
        var query = (await _holidayRepository.GetQueryableAsync()).WhereIf(
            !input.Search.IsNullOrWhiteSpace(),
            x => x.HolidayName.Contains(input.Search)
        ).WhereIf(
            input.TypeId.HasValue,
            x => x.HolidayTypeId == input.TypeId
        ).WhereIf(
            input.FromDate.HasValue,
            x => x.StartDate >= input.FromDate
        ).WhereIf(
            input.ToDate.HasValue,
            x => x.EndDate <= input.ToDate
        );

        var totalCount = await AsyncExecuter.CountAsync(query);

        // Apply dynamic ordering
        var sortingProperty = input.Sorting ?? nameof(Holiday.HolidayName);
        var queryWithSort = sortingProperty switch
        {
            "startDate" => query.OrderBy(x => x.StartDate),
            "endDate" => query.OrderBy(x => x.EndDate),
            "status" => query.AsEnumerable().OrderBy(x => x.Status).AsQueryable(),
            _ => query.OrderBy(x => x.HolidayName)
        };

        var items = await AsyncExecuter.ToListAsync(
            queryWithSort
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
        );

        return new PagedResultDto<HolidayDto>(
            totalCount,
            ObjectMapper.Map<List<Holiday>, List<HolidayDto>>(items)
        );
    }

    /// <summary>
    /// Search holidays by name with pagination
    /// </summary>
    public async Task<PagedResultDto<HolidayDto>> GetListByNameAsync(GetHolidayListRequestDto input)
    {
        return await GetListAsync(input);
    }

    /// <summary>
    /// Get all holidays without pagination
    /// </summary>
    public async Task<List<HolidayDto>> GetAllHolidaysAsync()
    {
        var holidays = await _holidayRepository.GetAllAsync();
        return ObjectMapper.Map<List<Holiday>, List<HolidayDto>>(holidays);
    }

    /// <summary>
    /// Get holidays by type (EventType)
    /// </summary>
    public async Task<List<HolidayDto>> GetByTypeAsync(Guid typeId)
    {
        var holidays = await _holidayRepository.GetByTypeAsync(typeId);
        return ObjectMapper.Map<List<Holiday>, List<HolidayDto>>(holidays);
    }

    /// <summary>
    /// Get holidays filtered by status
    /// </summary>
    public async Task<PagedResultDto<HolidayDto>> GetByStatusAsync(GetHolidayListRequestDto input)
    {
        if (!input.Status.HasValue)
        {
            return new PagedResultDto<HolidayDto>(0, new List<HolidayDto>());
        }

        var query = (await _holidayRepository.GetQueryableAsync())
            .WhereIf(!input.Search.IsNullOrWhiteSpace(), x => x.HolidayName.Contains(input.Search));

        var allHolidays = await AsyncExecuter.ToListAsync(query);

        // Filter by status (computed property requires in-memory evaluation)
        var statusValue = (HolidayStatus)input.Status.Value;
        var filtered = allHolidays.Where(x => x.Status == statusValue).ToList();

        var totalCount = filtered.Count;
        var items = filtered
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<HolidayDto>(
            totalCount,
            ObjectMapper.Map<List<Holiday>, List<HolidayDto>>(items)
        );
    }

    /// <summary>
    /// Override CreateAsync to use factory pattern
    /// </summary>
    public override async Task<HolidayDto> CreateAsync(CreateUpdateHolidayDto input)
    {
        var holiday = Holiday.Create(
            CurrentTenant.Id,
            input.HolidayName,
            input.HolidayTypeId,
            input.StartDate,
            input.EndDate,
            input.Description,
            input.AudienceIds,
            input.Attachments
        );

        await _holidayRepository.InsertAsync(holiday);
        return ObjectMapper.Map<Holiday, HolidayDto>(holiday);
    }

    /// <summary>
    /// Override UpdateAsync to use entity Update method
    /// </summary>
    public override async Task<HolidayDto> UpdateAsync(Guid id, CreateUpdateHolidayDto input)
    {
        var holiday = await _holidayRepository.GetAsync(id);

        holiday.Update(
            input.HolidayName,
            input.StartDate,
            input.EndDate,
            input.Description,
            input.AudienceIds,
            input.Attachments
        );

        await _holidayRepository.UpdateAsync(holiday);
        return ObjectMapper.Map<Holiday, HolidayDto>(holiday);
    }
}
