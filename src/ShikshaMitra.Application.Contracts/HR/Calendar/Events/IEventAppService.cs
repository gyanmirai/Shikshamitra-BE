using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.HR.Calendar.Events;

/// <summary>
/// Application service interface for Event CRUD operations
/// </summary>
public interface IEventAppService : ICrudAppService<
    EventDto,
    Guid,
    GetEventListRequestDto,
    CreateUpdateEventDto>
{
    /// <summary>
    /// Get all events without pagination
    /// </summary>
    Task<List<EventDto>> GetAllEventsAsync();

    /// <summary>
    /// Get events by type (EventType)
    /// </summary>
    Task<List<EventDto>> GetByTypeAsync(Guid typeId);

    /// <summary>
    /// Get events filtered by status
    /// </summary>
    Task<PagedResultDto<EventDto>> GetByStatusAsync(GetEventListRequestDto input);
}
