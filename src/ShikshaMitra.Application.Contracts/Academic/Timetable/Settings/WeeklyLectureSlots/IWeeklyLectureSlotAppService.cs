using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;

public interface IWeeklyLectureSlotAppService
{
    Task<PagedResultDto<WeeklyLectureSlotListDto>> GetListAsync(GetWeeklyLectureSlotsInput input);

    Task<WeeklyLectureSlotDto> GetAsync(Guid id);

    Task<WeeklyLectureSlotDto> CreateAsync(Guid academicMediumId, CreateWeeklyLectureSlotDto input);

    Task<WeeklyLectureSlotDto> UpdateAsync(Guid id, UpdateWeeklyLectureSlotDto input);
}
