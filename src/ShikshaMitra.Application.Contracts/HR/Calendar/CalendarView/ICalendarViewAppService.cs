using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.CalendarView;

/// <summary>
/// Application service interface for Calendar View operations
/// </summary>
public interface ICalendarViewAppService : IApplicationService
{
    /// <summary>
    /// Gets paginated calendar view data (unified events and holidays)
    /// with visibility filtering based on user's group membership
    /// </summary>
    /// <param name="input">Request parameters including filters and pagination</param>
    /// <returns>Paginated list of calendar items</returns>
    Task<PagedResultDto<CalendarViewItemDto>> GetViewAsync(GetCalendarViewRequestDto input);

    /// <summary>
    /// Gets detailed information about a specific calendar item (event or holiday)
    /// for popup display
    /// </summary>
    /// <param name="itemId">The ID of the event or holiday</param>
    /// <returns>Detailed calendar item information</returns>
    Task<CalendarItemDetailsDto> GetItemDetailsAsync(Guid itemId);
}
