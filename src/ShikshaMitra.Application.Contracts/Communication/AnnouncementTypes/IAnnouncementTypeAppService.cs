using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Communication.AnnouncementTypes.Dtos;

namespace ShikshaMitra.Communication.AnnouncementTypes
{
    public interface IAnnouncementTypeAppService : IApplicationService
    {
        Task<AnnouncementTypeListResultDto<AnnouncementTypeDto>> GetListAsync(GetAnnouncementTypesInput input);
        Task<AnnouncementTypeDto> GetAsync(Guid id, Guid academicMediumId);
        Task<AnnouncementTypeDto> CreateAsync(Guid academicMediumId, AnnouncementTypeCreateDto input);
        Task<AnnouncementTypeDto> UpdateAsync(Guid id,AnnouncementTypeUpdateDto input);
        Task<MessageDto> DeleteAsync(Guid id);
    }
}
