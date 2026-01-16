using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Communication.Announcements.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Communication.Announcements
{
    [RemoteService(Name = "Communication")]
    [Route("api/communication/announcements")]
    public class AnnouncementController : AbpController
    {
        private readonly IAnnouncementAppService _announcementAppService;

        public AnnouncementController(IAnnouncementAppService announcementAppService)
        {
            _announcementAppService = announcementAppService;
        }

        [HttpPost]
        public async Task<ActionResult<AnnouncementSuccessResponse>> CreateAsync(AnnouncementCreateDto input)
        {
            var result = await _announcementAppService.CreateAsync(input);
            return CreatedAtAction(nameof(GetAsync), new { id = result.Id }, new AnnouncementSuccessResponse
            {
                Success = true,
                Message = "Announcement created successfully",
                Data = new { id = result.Id }
            });
        }

        [HttpGet]
        public async Task<ActionResult<AnnouncementListResponse>> GetListAsync([FromQuery] GetAnnouncementListInput input)
        {
            var pagedResult = await _announcementAppService.GetListAsync(input);
            
            var totalPages = (int)Math.Ceiling((double)pagedResult.TotalCount / (input.MaxResultCount > 0 ? input.MaxResultCount : 10));

            return new AnnouncementListResponse
            {
                CurrentPage = (input.SkipCount / (input.MaxResultCount > 0 ? input.MaxResultCount : 10)) + 1,
                PageSize = input.MaxResultCount,
                TotalCount = (int)pagedResult.TotalCount,
                Items = pagedResult.Items,
                TotalPages = totalPages
            };
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AnnouncementDetailResponse>> GetAsync(Guid id)
        {
            var announcement = await _announcementAppService.GetAsync(id);
            return new AnnouncementDetailResponse { Item = announcement };
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AnnouncementSuccessResponse>> UpdateAsync(Guid id, AnnouncementUpdateDto input)
        {
            var result = await _announcementAppService.UpdateAsync(id, input);
            return new AnnouncementSuccessResponse
            {
                Success = true,
                Message = "Announcement updated successfully",
                Data = new { id = result.Id }
            };
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ActionSuccessResponse>> DeleteAsync(Guid id)
        {
            await _announcementAppService.DeleteAsync(id);
            return new ActionSuccessResponse
            {
                Success = true,
                Message = "Announcement deleted successfully"
            };
        }

        [HttpPatch("{id}/publish")]
        public async Task<ActionResult<ActionSuccessResponse>> PublishAsync(Guid id)
        {
            await _announcementAppService.PublishAsync(id);
            return new ActionSuccessResponse
            {
                Success = true,
                Message = "Announcement published successfully"
            };
        }

        [HttpPatch("{id}/visibility")]
        public async Task<ActionResult<ActionSuccessResponse>> UpdateVisibilityAsync(Guid id, AnnouncementVisibilityInput input)
        {
            await _announcementAppService.UpdateVisibilityAsync(id, input);
            return new ActionSuccessResponse
            {
                Success = true,
                Message = "Announcement visibility updated successfully"
            };
        }
    }

    // Response DTOs defined here for cleanliness as they are specific to API contract wrapping
    public class AnnouncementSuccessResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    public class AnnouncementListResponse
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public System.Collections.Generic.IReadOnlyList<AnnouncementDto> Items { get; set; }
        public int TotalPages { get; set; }
    }

    public class AnnouncementDetailResponse
    {
        public AnnouncementDto Item { get; set; }
    }

    public class ActionSuccessResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
