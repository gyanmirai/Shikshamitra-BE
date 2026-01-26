using ShikshaMitra.HR.Calendar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Calendar.HolidayTypes;

/// <summary>
/// Application service for managing HolidayType
/// </summary>
[RemoteService(false)]
public class HolidayTypeAppService : CrudAppService<HolidayType, HolidayTypeDto, Guid, GetHolidayTypeListRequestDto, CreateUpdateHolidayTypeDto>,
    IHolidayTypeAppService
{
    private readonly IHolidayTypeRepository _repository;

    public HolidayTypeAppService(IHolidayTypeRepository repository)
        : base(repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Get paginated list of holiday types with optional filtering
    /// </summary>
    public override async Task<PagedResultDto<HolidayTypeDto>> GetListAsync(GetHolidayTypeListRequestDto input)
    {
        var query = (await Repository.GetQueryableAsync())
            .WhereIf(!input.Keyword.IsNullOrWhiteSpace(), x => x.Name.Contains(input.Keyword!));

        var totalCount = await AsyncExecuter.CountAsync(query);

        // Apply dynamic ordering
        var sortingProperty = input.Sorting ?? nameof(HolidayType.Name);
        var queryWithSort = sortingProperty switch
        {
            "creationTime" => query.OrderBy(x => x.CreationTime),
            _ => query.OrderBy(x => x.Name)
        };

        var items = await AsyncExecuter.ToListAsync(
            queryWithSort
                .Skip((input.PageNumber - 1) * input.PageSize)
                .Take(input.PageSize)
        );

        return new PagedResultDto<HolidayTypeDto>(
            totalCount,
            ObjectMapper.Map<List<HolidayType>, List<HolidayTypeDto>>(items)
        );
    }
}
