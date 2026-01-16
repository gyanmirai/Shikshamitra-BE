using System;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Announcements.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Application.Dtos; // <-- Add this using directive

namespace ShikshaMitra.Communication.Announcements
{
    public interface IAnnouncementAppService : IApplicationService
    {
        Task<AnnouncementDto> CreateAsync(AnnouncementCreateDto input);
        Task<AnnouncementDto> GetAsync(Guid id);
        Task<PagedResultDto<AnnouncementDto>> GetListAsync(GetAnnouncementListInput input);
        Task<AnnouncementDto> UpdateAsync(Guid id, AnnouncementUpdateDto input);
        Task DeleteAsync(Guid id);
        Task PublishAsync(Guid id);
        Task UpdateVisibilityAsync(Guid id, AnnouncementVisibilityInput input);
    }
}
