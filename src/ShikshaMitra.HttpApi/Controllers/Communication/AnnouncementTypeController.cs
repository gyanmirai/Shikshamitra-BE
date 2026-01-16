using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Controllers;
using ShikshaMitra.Communication.AnnouncementTypes.Dtos;
using Volo.Abp;

namespace ShikshaMitra.Communication.AnnouncementTypes
{
    [Route("communication/announcement-types")]
    public class AnnouncementTypeController : ShikshaMitraController, IAnnouncementTypeAppService
    {
        private readonly IAnnouncementTypeAppService _announcementTypeAppService;

        public AnnouncementTypeController(IAnnouncementTypeAppService announcementTypeAppService)
        {
            _announcementTypeAppService = announcementTypeAppService;
        }

        [HttpGet]
        public Task<AnnouncementTypeListResultDto<AnnouncementTypeDto>> GetListAsync(GetAnnouncementTypesInput input)
        {
            return _announcementTypeAppService.GetListAsync(input);
        }

        [HttpGet("{id}")]
        public Task<AnnouncementTypeDto> GetAsync(Guid id, [FromQuery] Guid academicMediumId)
        {
            return _announcementTypeAppService.GetAsync(id, academicMediumId);
        }

        [HttpPost]
        public Task<AnnouncementTypeDto> CreateAsync([FromQuery] Guid academicMediumId, [FromBody] AnnouncementTypeCreateDto input)
        {
            return _announcementTypeAppService.CreateAsync(academicMediumId, input);
        }

        [HttpPut("{id}")]
        public Task<AnnouncementTypeDto> UpdateAsync(Guid id, [FromBody] AnnouncementTypeUpdateDto input)
        {
            return _announcementTypeAppService.UpdateAsync(id, input);
        }

        [HttpDelete("{id}")]
        public Task<MessageDto> DeleteAsync(Guid id)
        {
            return _announcementTypeAppService.DeleteAsync(id);
        }
    }
}
