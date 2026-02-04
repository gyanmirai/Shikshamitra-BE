using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.Communication.AnnouncementTypes;

/// <summary>
/// Application service interface for managing announcement types
/// </summary>
public interface IAnnouncementTypeAppService : IApplicationService
{
    /// <summary>
    /// Get paginated list of announcement types
    /// </summary>
    Task<PagedResultDto<AnnouncementTypeDto>> GetListAsync(GetAnnouncementTypeListRequestDto input);

    /// <summary>
    /// Get announcement type by ID
    /// </summary>
    Task<AnnouncementTypeDto> GetAsync(Guid id);

    /// <summary>
    /// Create a new announcement type
    /// </summary>
    Task<AnnouncementTypeDto> CreateAsync(Guid? academicMediumId, CreateUpdateAnnouncementTypeDto input);

    /// <summary>
    /// Update an existing announcement type
    /// </summary>
    Task<AnnouncementTypeDto> UpdateAsync(Guid id, CreateUpdateAnnouncementTypeDto input);

    /// <summary>
    /// Delete an announcement type
    /// </summary>
    Task DeleteAsync(Guid id);
}
