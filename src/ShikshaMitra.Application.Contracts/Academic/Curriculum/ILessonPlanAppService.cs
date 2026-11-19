using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.Curriculum;

public interface ILessonPlanAppService : IApplicationService
{
    Task<PagedResultDto<LessonPlanDto>> GetListAsync(GetLessonPlansInput input);
    Task<LessonPlanWorkspaceDto> GetWorkspaceAsync(GetLessonPlanWorkspaceInput input);
    Task<LessonPlanTimelineDto> GetTimelineAsync(DateTimeOffset date, Guid? teacherId = null, Guid? academicMediumId = null);
    Task<LessonPlanChapterProgressDto> GetChapterProgressAsync(GetLessonPlanChapterProgressInput input);
    Task<LessonPlanDto> GetAsync(Guid id);
    Task<LessonPlanDto> CreateAsync(CreateLessonPlanDto input);
    Task<LessonPlanDto> UpdateAsync(Guid id, UpdateLessonPlanDto input);
    Task<LessonPlanDto> SubmitAsync(Guid id);
    Task BulkSubmitAsync(BulkSubmitLessonPlansDto input);
    Task<LessonPlanDto> ApproveAsync(Guid id, ReviewRemarksDto input);
    Task<LessonPlanDto> RejectAsync(Guid id, ReviewRemarksDto input);
    Task<LessonPlanDto> RequestChangesAsync(Guid id, ReviewRemarksDto input);
    Task<HomeworkCreateResponseDto> CreateHomeworkFromLessonPlanAsync(Guid id, CreateHomeworkFromLessonPlanDto input);
    Task<PagedResultDto<TeacherDiaryDto>> GetTeacherDiariesAsync(GetTeacherDiariesInput input);
    Task<TeacherDiaryDto> GetTeacherDiaryAsync(Guid id);
    Task<TeacherDiaryDto> CreateTeacherDiaryAsync(CreateUpdateTeacherDiaryDto input);
    Task<TeacherDiaryDto> UpdateTeacherDiaryAsync(Guid id, CreateUpdateTeacherDiaryDto input);
    Task<TeacherDiaryDto> SubmitTeacherDiaryAsync(Guid id);
}
