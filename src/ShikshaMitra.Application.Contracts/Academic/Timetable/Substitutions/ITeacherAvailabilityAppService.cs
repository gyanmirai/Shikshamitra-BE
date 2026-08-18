using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public interface ITeacherAvailabilityAppService : IApplicationService
{
    Task<PagedResultDto<TeacherAvailabilityDto>> GetListAsync(GetTeacherAvailabilityInput input);
    Task<TeacherAvailabilityDto> CreateAsync(CreateTeacherAvailabilityDto input);
    Task<AffectedLectureResultDto> GetAffectedLecturesAsync(CreateTeacherAvailabilityDto input);
    Task CancelAsync(Guid id);
}
