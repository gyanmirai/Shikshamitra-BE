using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Calendar.EventTypes;

/// <summary>
/// Application service interface for managing event types
/// </summary>
public interface IEventTypeAppService : ICrudAppService<EventTypeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateEventTypeDto>
{
    /// <summary>
    /// Get paginated list of event types with optional search by name
    /// </summary>
    /// <param name="input">Paged request with optional search parameter</param>
    /// <returns>Paged list of event types</returns>
    Task<PagedResultDto<EventTypeDto>> GetListByNameAsync(GetEventTypeListRequestDto input);

    /// <summary>
    /// Get all event types (non-paginated)
    /// </summary>
    /// <returns>List of all event types</returns>
    Task<List<EventTypeDto>> GetAllEventTypesAsync();
}

/// <summary>
/// Request DTO for filtered event type list retrieval
/// </summary>
public class GetEventTypeListRequestDto : PagedAndSortedResultRequestDto
{
    /// <summary>
    /// Optional search term to filter by event type name
    /// </summary>
    [StringLength(256)]
    public string? Search { get; set; }

    public GetEventTypeListRequestDto()
    {
        MaxResultCount = 20; // Default page size
    }
}
