using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;

namespace ShikshaMitra.Academic.Curriculum;

public partial class LessonPlanAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.View)]
    public async Task<LessonPlanWorkspaceDto> GetWorkspaceAsync(GetLessonPlanWorkspaceInput input)
    {
        if (input.View is not ("lessonList" or "teacherDiary" or "reviewQueue"))
            throw new BusinessException("LessonPlan:InvalidWorkspaceView");
        var cohort = await GetWorkspaceCohortAsync(input);
        var keys = await ReadWorkspaceKeysAsync(cohort);
        var criteria = ParseWorkspaceJson<Dictionary<string, System.Text.Json.JsonElement>>(input.UiCriteria);
        var summary = WorkspaceSummary(keys, criteria);
        var filters = ParseWorkspaceJson<List<WorkspaceFilter>>(input.Filters);
        var sorts = ParseWorkspaceJson<List<WorkspaceSort>>(input.Sorts);
        if (WorkspaceNeedsInsights(input, filters, sorts))
        {
            var insights = await LoadLessonPlanInsightsAsync(keys.Select(key => key.InsightPlan()).ToList());
            foreach (var key in keys) key.InsightMessage = insights[key.Id].Message;
        }
        IEnumerable<WorkspaceKey> matches = keys.Where(key => MatchesWorkspaceUi(key, input, criteria));
        if (input.Status.HasValue) matches = matches.Where(key => key.Status == input.Status.Value);
        matches = ApplyWorkspaceTable(matches, input, filters, sorts);
        var selected = matches.ToList();
        var pageIds = selected.Skip(input.SkipCount).Take(input.MaxResultCount).Select(key => key.Id).ToArray();
        var rows = await AsyncExecuter.ToListAsync(cohort.Where(plan => pageIds.Contains(plan.Id)));
        var items = await MapLessonPlansAsync(rows);
        var positions = pageIds.Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => pair.index);
        items = items.OrderBy(item => positions[item.Id]).ToList();
        var diaries = await AsyncExecuter.ToListAsync((await teacherDiaryRepository.GetQueryableAsync())
            .Where(diary => pageIds.Contains(diary.LessonPlanId)));
        return new LessonPlanWorkspaceDto
        {
            TotalCount = selected.Count, Items = items, Diaries = await MapTeacherDiariesAsync(diaries), Summary = summary
        };
    }

    private async Task<IQueryable<LessonPlan>> GetWorkspaceCohortAsync(GetLessonPlanWorkspaceInput input)
    {
        var query = await ApplyLessonPlanFiltersAsync(await lessonPlanRepository.GetQueryableAsync(), new GetLessonPlansInput
        {
            AcademicYearId = input.AcademicYearId, AcademicMediumId = input.View == "reviewQueue" ? null : input.AcademicMediumId,
            TeacherId = input.TeacherId, StandardId = input.StandardId, DivisionId = input.DivisionId,
            SubjectId = input.SubjectId, ChapterId = input.ChapterId, FromDate = input.FromDate, ToDate = input.ToDate
        });
        var unscopedQuery = query;
        if (!IsAdminUser() && !await HasClassTeacherAccessAsync())
        {
            var teachers = await GetCurrentAccessibleTeacherIdsAsync();
            if (teachers.Count == 0) return query.Where(_ => false);
            var scoped = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
            var ownedIds = await GetTeacherAndReporteeSubjectOwnedPlanIdsAsync(unscopedQuery, teachers);
            query = scoped.Concat(unscopedQuery.Where(plan => teachers.Contains(plan.TeacherId)))
                .Concat(unscopedQuery.Where(plan => ownedIds.Contains(plan.Id))).Distinct()
                .Where(plan => teachers.Contains(plan.TeacherId));
        }
        return query;
    }

    private static LessonPlanWorkspaceSummaryDto WorkspaceSummary(List<WorkspaceKey> keys, Dictionary<string, System.Text.Json.JsonElement> criteria)
    {
        var today = WorkspaceText(criteria, "todayIso");
        var week = WorkspaceText(criteria, "weekStartIso");
        var weekEnd = DateTime.TryParse(week, out var firstDay) ? firstDay.AddDays(6).ToString("yyyy-MM-dd") : "";
        var reviews = keys.Where(key => key.Status is LessonPlanStatus.Submitted or LessonPlanStatus.Approved or LessonPlanStatus.Rejected or LessonPlanStatus.NeedsChanges).ToList();
        return new LessonPlanWorkspaceSummaryDto
        {
            ReviewStatusCounts = new() { ["all"] = reviews.Count, ["pending"] = reviews.Count(key => key.Status == LessonPlanStatus.Submitted),
                ["approved"] = reviews.Count(key => key.Status == LessonPlanStatus.Approved), ["rejected"] = reviews.Count(key => key.Status == LessonPlanStatus.Rejected),
                ["changes"] = reviews.Count(key => key.Status == LessonPlanStatus.NeedsChanges) },
            QuickCounts = new() { ["lessonsPlanned"] = keys.Count(key => string.CompareOrdinal(key.Date, week) >= 0 && string.CompareOrdinal(key.Date, weekEnd) <= 0),
                ["diariesSubmitted"] = keys.Sum(key => key.Diaries.Count(diary => diary.Date == today && diary.Status == TeacherDiaryStatus.Submitted)),
                ["pendingDiaries"] = keys.Count(key => key.Diaries.Count == 0), ["pendingApprovals"] = keys.Count(key => key.Status == LessonPlanStatus.Submitted),
                ["missedLessons"] = keys.Sum(key => key.Diaries.Count(diary => !diary.ClassConducted)),
                ["syllabusDelayed"] = keys.Count(key => key.PlannedPercentage - key.ActualPercentage > 10) },
            Options = new() { ["teachers"] = keys.Select(key => key.TeacherName).Distinct().ToArray(), ["classes"] = keys.Select(key => key.ClassName).Distinct().ToArray(),
                ["divisions"] = keys.Select(key => key.DivisionName).Distinct().ToArray(), ["subjects"] = keys.Select(key => key.SubjectName).Distinct().ToArray(),
                ["chapters"] = keys.Select(key => key.ChapterName).Distinct().ToArray(), ["reviewClasses"] = reviews.Select(key => key.ClassDivision).Distinct().ToArray(),
                ["reviewSubjects"] = reviews.Select(key => key.SubjectName).Distinct().ToArray(), ["reviewers"] = reviews.Select(key => key.ReviewerName).Where(name => !string.IsNullOrEmpty(name)).Distinct().ToArray() }
        };
    }
}
