using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Communication.AnnouncementTypes;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Communication.AnnouncementTypes;

/// <summary>
/// Controller for managing announcement types
/// </summary>
[Area("app")]
[RemoteService(Name = "ShikshaMitra")]
[Route("api/announcement-types")]
public class AnnouncementTypeController : AbpControllerBase
{
    private readonly IAnnouncementTypeAppService _announcementTypeAppService;

    public AnnouncementTypeController(IAnnouncementTypeAppService announcementTypeAppService)
    {
        _announcementTypeAppService = announcementTypeAppService;
    }

    /// <summary>
    /// Get all announcement types (paginated)
    /// </summary>
    /// <param name="input">Pagination and filter parameters</param>
    /// <returns>Paginated list of announcement types</returns>
    [HttpGet]
    public async Task<PagedResultDto<AnnouncementTypeDto>> GetAllAsync([FromQuery] GetAnnouncementTypeListRequestDto input)
    {
        return await _announcementTypeAppService.GetListAsync(input);
    }

    /// <summary>
    /// Get announcement type by ID
    /// </summary>
    /// <param name="id">Announcement type ID</param>
    /// <returns>Announcement type details</returns>
    [HttpGet("{id}")]
    public async Task<AnnouncementTypeDto> GetAsync(Guid id)
    {
        return await _announcementTypeAppService.GetAsync(id);
    }

    /// <summary>
    /// Create a new announcement type
    /// </summary>
    /// <param name="academicMediumId">Academic medium ID (query parameter)</param>
    /// <param name="input">Announcement type data</param>
    /// <returns>Created announcement type</returns>
    [HttpPost]
    public async Task<AnnouncementTypeDto> CreateAsync(
        [FromQuery] Guid? academicMediumId,
        [FromBody] CreateUpdateAnnouncementTypeDto input)
    {
        return await _announcementTypeAppService.CreateAsync(academicMediumId, input);
    }

    /// <summary>
    /// Update an existing announcement type
    /// </summary>
    /// <param name="id">Announcement type ID</param>
    /// <param name="input">Updated announcement type data</param>
    /// <returns>Updated announcement type</returns>
    [HttpPut("{id}")]
    public async Task<AnnouncementTypeDto> UpdateAsync(
        Guid id,
        [FromBody] CreateUpdateAnnouncementTypeDto input)
    {
        return await _announcementTypeAppService.UpdateAsync(id, input);
    }

    /// <summary>
    /// Delete an announcement type
    /// </summary>
    /// <param name="id">Announcement type ID</param>
    /// <returns>Success message</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _announcementTypeAppService.DeleteAsync(id);
        return Ok(new { message = "Announcement type deleted successfully" });
    }
}
