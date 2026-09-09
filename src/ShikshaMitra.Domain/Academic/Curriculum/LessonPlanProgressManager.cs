using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Curriculum;

public class LessonPlanProgressManager(
    IRepository<LessonPlan, Guid> lessonPlans,
    IRepository<TeacherDiary, Guid> diaries,
    IAsyncQueryableExecuter asyncExecuter) : DomainService
{
    public async Task<int> GetMinimumAsync(
        Guid academicYearId, Guid standardId, Guid divisionId, Guid subjectId, Guid chapterId)
    {
        var plansQuery = await lessonPlans.GetQueryableAsync();
        var diariesQuery = await diaries.GetQueryableAsync();
        // Achievement belongs to the chapter/division, regardless of teacher or
        // topic. Draft diary values and planned targets are not completed work.
        var progress = from plan in plansQuery
                       join diary in diariesQuery on plan.Id equals diary.LessonPlanId
                       where plan.TenantId == CurrentTenant.Id && diary.TenantId == CurrentTenant.Id &&
                             plan.AcademicYearId == academicYearId && plan.StandardId == standardId &&
                             plan.DivisionId == divisionId && plan.SubjectId == subjectId &&
                             plan.ChapterId == chapterId && diary.ClassConducted &&
                             diary.Status == TeacherDiaryStatus.Submitted
                       select (int?)diary.ActualCompletionPercentage;
        return await asyncExecuter.MaxAsync(progress) ?? 0;
    }

    public async Task EnsureTargetAsync(LessonPlan plan, Guid chapterId, int target)
    {
        var minimum = await GetMinimumAsync(
            plan.AcademicYearId, plan.StandardId, plan.DivisionId, plan.SubjectId, chapterId);
        if (target < minimum)
        {
            throw new UserFriendlyException(
                $"Target chapter completion cannot be below the recorded progress of {minimum}%.");
        }
    }
}
