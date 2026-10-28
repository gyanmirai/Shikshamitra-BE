using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Curriculum;

public partial class LessonPlanAppService
{
    private async Task<List<WorkspaceKey>> ReadWorkspaceKeysAsync(IQueryable<LessonPlan> query)
    {
        var keys = await AsyncExecuter.ToListAsync(query.OrderByDescending(plan => plan.CreationTime).Select(plan => new WorkspaceKey
        {
            Id = plan.Id, TenantId = plan.TenantId, YearId = plan.AcademicYearId, TeacherId = plan.TeacherId, StandardId = plan.StandardId,
            DivisionId = plan.DivisionId, SubjectId = plan.SubjectId, ChapterId = plan.ChapterId, PlannedDate = plan.PlannedDate,
            SlotId = plan.TimetableSlotId, Topic = plan.Topic, Status = plan.Status, ReviewerId = plan.ReviewedBy,
            PlannedPercentage = plan.PlannedCompletionPercentage
        }));
        if (keys.Count == 0) return keys;
        var teacherIds = keys.Select(key => key.TeacherId).Distinct().ToArray();
        var reviewerIds = keys.Where(key => key.ReviewerId.HasValue).Select(key => key.ReviewerId!.Value).Distinct().ToArray();
        var teachers = await AsyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
            .Where(employee => teacherIds.Contains(employee.Id) || (employee.UserId.HasValue && reviewerIds.Contains(employee.UserId.Value)))
            .Select(employee => new LessonEmployeeName(employee.Id, employee.UserId, employee.FirstName, employee.LastName)));
        var users = await AsyncExecuter.ToListAsync((await identityUserRepository.GetQueryableAsync()).Where(user => reviewerIds.Contains(user.Id))
            .Select(user => new LessonIdentityName(user.Id, user.Name, user.Surname, user.UserName)));
        var standards = await AsyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync()).Where(row => query.Any(plan => plan.StandardId == row.Id)).Select(row => new { row.Id, row.Name }));
        var divisions = await AsyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync()).Where(row => query.Any(plan => plan.DivisionId == row.Id)).Select(row => new { row.Id, row.Name }));
        var subjects = await AsyncExecuter.ToListAsync((await subjectRepository.GetQueryableAsync()).Where(row => query.Any(plan => plan.SubjectId == row.Id)).Select(row => new { row.Id, row.Name }));
        var chapters = await AsyncExecuter.ToListAsync((await chapterRepository.GetQueryableAsync()).Where(row => query.Any(plan => plan.ChapterId == row.Id)).Select(row => new { row.Id, row.ChapterName }));
        var diaries = await AsyncExecuter.ToListAsync((await teacherDiaryRepository.GetQueryableAsync()).Where(row => query.Any(plan => plan.Id == row.LessonPlanId))
            .Select(row => new WorkspaceDiaryKey { Id = row.Id, PlanId = row.LessonPlanId, ActualDate = row.ActualDate, Status = row.Status,
                Percentage = row.ActualCompletionPercentage, IsBackdated = row.IsBackdated, ClassConducted = row.ClassConducted }));
        var slots = await AsyncExecuter.ToListAsync((await timetableSlotRepository.GetQueryableAsync()).Where(row => query.Any(plan => plan.TimetableSlotId == row.Id))
            .Select(row => new LessonSlotLookup(row.Id, row.Sequence, row.StartTime, row.EndTime)));
        var teacherLookup = teachers.ToDictionary(row => row.Id);
        var reviewerLookup = teachers.Where(row => row.UserId.HasValue).GroupBy(row => row.UserId!.Value).ToDictionary(group => group.Key, group => group.First());
        var userLookup = users.ToDictionary(row => row.Id);
        var standardLookup = standards.ToDictionary(row => row.Id, row => row.Name);
        var divisionLookup = divisions.ToDictionary(row => row.Id, row => row.Name);
        var subjectLookup = subjects.ToDictionary(row => row.Id, row => row.Name);
        var chapterLookup = chapters.ToDictionary(row => row.Id, row => row.ChapterName);
        var diaryLookup = diaries.ToLookup(row => row.PlanId);
        var slotLookup = slots.ToDictionary(row => row.Id);
        foreach (var key in keys)
        {
            key.TeacherName = teacherLookup.GetValueOrDefault(key.TeacherId)?.EmployeeName ?? "";
            key.ClassName = standardLookup.GetValueOrDefault(key.StandardId) ?? "";
            key.DivisionName = divisionLookup.GetValueOrDefault(key.DivisionId) ?? "";
            key.SubjectName = subjectLookup.GetValueOrDefault(key.SubjectId) ?? "";
            key.ChapterName = chapterLookup.GetValueOrDefault(key.ChapterId) ?? "";
            key.ReviewerName = key.ReviewerId.HasValue ? reviewerLookup.GetValueOrDefault(key.ReviewerId.Value)?.EmployeeName ?? GetIdentityUserDisplayName(userLookup.GetValueOrDefault(key.ReviewerId.Value)) ?? "" : "";
            key.Diaries = diaryLookup[key.Id].ToList();
            var slot = key.SlotId.HasValue ? slotLookup.GetValueOrDefault(key.SlotId.Value) : null;
            key.Period = slot != null ? $"Period {slot.Sequence}" : key.SlotId.HasValue ? "Scheduled" : "Manual";
            key.PlannedTime = slot == null ? "" : $"{slot.StartTime:hh:mm tt} - {slot.EndTime:hh:mm tt}";
        }
        return keys;
    }

    private sealed class WorkspaceKey
    {
        public Guid Id { get; set; }
        public Guid? TenantId { get; set; }
        public Guid YearId { get; set; }
        public Guid TeacherId { get; set; }
        public Guid StandardId { get; set; }
        public Guid DivisionId { get; set; }
        public Guid SubjectId { get; set; }
        public Guid ChapterId { get; set; }
        public Guid? SlotId { get; set; }
        public Guid? ReviewerId { get; set; }
        public DateTime PlannedDate { get; set; }
        public string Topic { get; set; } = "";
        public LessonPlanStatus Status { get; set; }
        public int PlannedPercentage { get; set; }
        public string TeacherName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string DivisionName { get; set; } = "";
        public string SubjectName { get; set; } = "";
        public string ChapterName { get; set; } = "";
        public string ReviewerName { get; set; } = "";
        public string Period { get; set; } = "";
        public string PlannedTime { get; set; } = "";
        public string? InsightMessage { get; set; }
        public List<WorkspaceDiaryKey> Diaries { get; set; } = new();
        public string Date => LessonPlanCalendarDate.FromStoredInstant(PlannedDate).Date.ToString("yyyy-MM-dd");
        public string ClassDivision => $"{ClassName}-{DivisionName}";
        public int ActualPercentage => Diaries.FirstOrDefault()?.Percentage ?? 0;
        public string DiaryStatus => Diaries.FirstOrDefault()?.Status.ToString() ?? "NotSubmitted";
        public string ReviewStatus => Status == LessonPlanStatus.Approved ? "approved" : Status == LessonPlanStatus.Rejected ? "rejected" : Status == LessonPlanStatus.NeedsChanges ? "changes" : "pending";
        public LessonPlan InsightPlan() => new(Id, TenantId, YearId, TeacherId, StandardId, DivisionId, SubjectId, ChapterId, PlannedDate, Topic, PlannedPercentage, SlotId);
    }

    private sealed class WorkspaceDiaryKey
    {
        public Guid Id { get; set; }
        public Guid PlanId { get; set; }
        public DateTime ActualDate { get; set; }
        public TeacherDiaryStatus Status { get; set; }
        public int Percentage { get; set; }
        public bool IsBackdated { get; set; }
        public bool ClassConducted { get; set; }
        public string Date => LessonPlanCalendarDate.FromStoredInstant(ActualDate).Date.ToString("yyyy-MM-dd");
    }
}
