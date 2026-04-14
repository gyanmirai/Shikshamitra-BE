using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public interface ILectureSlotAppService
{
    Task<PagedResultDto<LectureSlotListDto>> GetByMediumAsync(GetLectureSlotsByMediumInput input);

    Task<LectureSlotDto> GetAsync(Guid id);

    Task<LectureSlotDto> CreateAsync(CreateLectureSlotDto input);

    Task<LectureSlotDto> UpdateAsync(Guid id, UpdateLectureSlotDto input);

    Task AssignAsync(Guid id, AssignLectureSlotDto input);
}
