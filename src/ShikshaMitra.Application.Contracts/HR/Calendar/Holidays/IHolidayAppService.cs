using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;

/// <summary>
/// Application service interface for Holiday management
/// </summary>
public interface IHolidayAppService : ICrudAppService<
    HolidayDto,
    Guid,
    GetHolidayListRequestDto,
    CreateUpdateHolidayDto>
{
    /// <summary>
    /// Search holidays by name with pagination
    /// </summary>
    Task<Volo.Abp.Application.Dtos.PagedResultDto<HolidayDto>> GetListByNameAsync(GetHolidayListRequestDto input);

    /// <summary>
    /// Get all holidays without pagination
    /// </summary>
    Task<System.Collections.Generic.List<HolidayDto>> GetAllHolidaysAsync();

    /// <summary>
    /// Get holidays by type (EventType)
    /// </summary>
    Task<System.Collections.Generic.List<HolidayDto>> GetByTypeAsync(Guid typeId);

    /// <summary>
    /// Get holidays by status
    /// </summary>
    Task<Volo.Abp.Application.Dtos.PagedResultDto<HolidayDto>> GetByStatusAsync(GetHolidayListRequestDto input);
}
