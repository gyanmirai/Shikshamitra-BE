using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Curriculum;

[Authorize]
public class LessonPlanAppService(
    IRepository<LessonPlan, Guid> lessonPlanRepository,
    IRepository<TeacherDiary, Guid> teacherDiaryRepository,
    ITimetableRepository timetableRepository,
    IRepository<TimetableSlot, Guid> timetableSlotRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<AssignedSubjectChapter, Guid> chapterRepository,
    IRepository<Employee, Guid> employeeRepository,
    IRepository<AcademicCalendarDay, Guid> calendarDayRepository,
    IRepository<EmployeeLeave, Guid> employeeLeaveRepository,
    IHomeworkAppService homeworkAppService
) : ApplicationService, ILessonPlanAppService
{
    public async Task<PagedResultDto<LessonPlanDto>> GetListAsync(GetLessonPlansInput input)
    {
        var query = await lessonPlanRepository.GetQueryableAsync();
        query = ApplyLessonPlanFilters(query, input);

        var count = await AsyncExecuter.CountAsync(query);
        var list = await AsyncExecuter.ToListAsync(
            query
                .OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
        );

        var items = await MapLessonPlansAsync(list);
        return new PagedResultDto<LessonPlanDto>(count, items);
    }

    public async Task<LessonPlanTimelineDto> GetTimelineAsync(DateTimeOffset date, Guid? teacherId = null)
    {
        var targetTeacherId = teacherId ?? CurrentUser.GetId();
        var timetableDay = ToTimetableDay(date.DateTime.DayOfWeek);
        if (timetableDay == null)
        {
            return new LessonPlanTimelineDto { Date = date.DateTime, TeacherId = targetTeacherId };
        }

        var timetableQuery = await timetableRepository.WithDetailsAsync();
        var timetables = await AsyncExecuter.ToListAsync(
            timetableQuery.Where(x => x.Status == TimetableStatus.Published)
        );

        var lessonPlans = await lessonPlanRepository.GetListAsync(x =>
            x.TeacherId == targetTeacherId && x.PlannedDate.Date == date.DateTime.Date
        );
        var mappedPlans = await MapLessonPlansAsync(lessonPlans);
        var mappedById = mappedPlans.ToDictionary(x => x.Id);

        var standards = await standardRepository.GetListAsync();
        var divisions = await divisionRepository.GetListAsync();
        var subjects = await subjectRepository.GetListAsync();

        var slots = new List<TimelineSlotDto>();

        foreach (var timetable in timetables)
        {
            var daySlots = timetable.Slots.Where(s => s.Day == timetableDay.Value);
            foreach (var slot in daySlots)
            {
                var teacherSubjects = slot.Subjects.Where(s => s.TeacherId == targetTeacherId);
                foreach (var slotSubject in teacherSubjects)
                {
                    var standard = standards.FirstOrDefault(s => s.Id == timetable.StandardId);
                    var division = divisions.FirstOrDefault(d => d.Id == timetable.DivisionId);
                    var subject = subjects.FirstOrDefault(sub => sub.Id == slotSubject.SubjectId);
                    var plan = lessonPlans.FirstOrDefault(lp => lp.TimetableSlotId == slot.Id);
                    var insight = await ResolveLessonPlanInsightAsync(
                        targetTeacherId,
                        date.DateTime.Date,
                        timetable.StandardId,
                        timetable.DivisionId
                    );

                    slots.Add(new TimelineSlotDto
                    {
                        TimetableSlotId = slot.Id,
                        PeriodName = $"Period {slot.Sequence}",
                        StartTime = slot.StartTime.ToString("hh:mm tt"),
                        EndTime = slot.EndTime.ToString("hh:mm tt"),
                        StandardId = timetable.StandardId,
                        StandardName = standard?.Name ?? "Class",
                        DivisionId = timetable.DivisionId,
                        DivisionName = division?.Name ?? "Division",
                        SubjectId = slotSubject.SubjectId,
                        SubjectName = subject?.Name ?? "Subject",
                        IsTimetableSlotOnly = plan == null,
                        LessonPlan = plan != null && mappedById.TryGetValue(plan.Id, out var dto) ? dto : null,
                        InsightType = insight.Type,
                        InsightSeverity = insight.Severity,
                        InsightMessage = insight.Message,
                        IsPlanCreationBlocked = insight.BlocksPlanCreation
                    });
                }
            }
        }

        return new LessonPlanTimelineDto
        {
            Date = date.DateTime,
            TeacherId = targetTeacherId,
            Slots = slots.OrderBy(s => s.StartTime).ToList()
        };
    }

    public async Task<LessonPlanDto> GetAsync(Guid id)
    {
        var plan = await lessonPlanRepository.GetAsync(id);
        return (await MapLessonPlansAsync(new[] { plan })).Single();
    }

    public async Task<LessonPlanDto> CreateAsync(CreateLessonPlanDto input)
    {
        var chapterExists = await chapterRepository.AnyAsync(x => x.Id == input.ChapterId);
        if (!chapterExists)
        {
            throw new UserFriendlyException("Selected chapter is invalid.");
        }

        var lessonPlan = new LessonPlan(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.AcademicYearId,
            input.TeacherId,
            input.StandardId,
            input.DivisionId,
            input.SubjectId,
            input.ChapterId,
            input.PlannedDate,
            input.Topic,
            input.PlannedCompletionPercentage,
            input.TimetableSlotId,
            input.IsAiGenerated
        );

        lessonPlan.UpdatePlan(
            input.ChapterId,
            input.Topic,
            input.LearningObjective,
            input.TeachingMethod,
            input.ResourcesRequired,
            input.PlannedCompletionPercentage
        );

        var inserted = await lessonPlanRepository.InsertAsync(lessonPlan, autoSave: true);
        return (await MapLessonPlansAsync(new[] { inserted })).Single();
    }

    public async Task<LessonPlanDto> UpdateAsync(Guid id, UpdateLessonPlanDto input)
    {
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        EnsureOwnTeacherRecord(lessonPlan.TeacherId, "modify");

        lessonPlan.UpdatePlan(
            input.ChapterId,
            input.Topic,
            input.LearningObjective,
            input.TeachingMethod,
            input.ResourcesRequired,
            input.PlannedCompletionPercentage
        );

        await lessonPlanRepository.UpdateAsync(lessonPlan);
        return await GetAsync(id);
    }

    public async Task<LessonPlanDto> SubmitAsync(Guid id)
    {
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        EnsureOwnTeacherRecord(lessonPlan.TeacherId, "submit");

        lessonPlan.Submit();
        await lessonPlanRepository.UpdateAsync(lessonPlan);
        return await GetAsync(id);
    }

    public async Task BulkSubmitAsync(BulkSubmitLessonPlansDto input)
    {
        foreach (var id in input.LessonPlanIds)
        {
            var plan = await lessonPlanRepository.GetAsync(id);
            EnsureOwnTeacherRecord(plan.TeacherId, "submit");
            plan.Submit();
            await lessonPlanRepository.UpdateAsync(plan);
        }
    }

    public async Task<LessonPlanDto> ApproveAsync(Guid id, ReviewRemarksDto input)
    {
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        lessonPlan.Approve(CurrentUser.GetId(), input.Comment);
        await lessonPlanRepository.UpdateAsync(lessonPlan);
        return await GetAsync(id);
    }

    public async Task<LessonPlanDto> RejectAsync(Guid id, ReviewRemarksDto input)
    {
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        lessonPlan.Reject(CurrentUser.GetId(), input.Comment);
        await lessonPlanRepository.UpdateAsync(lessonPlan);
        return await GetAsync(id);
    }

    public async Task<LessonPlanDto> RequestChangesAsync(Guid id, ReviewRemarksDto input)
    {
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        lessonPlan.RequestChanges(CurrentUser.GetId(), input.Comment);
        await lessonPlanRepository.UpdateAsync(lessonPlan);
        return await GetAsync(id);
    }

    public async Task<HomeworkCreateResponseDto> CreateHomeworkFromLessonPlanAsync(Guid id, CreateHomeworkFromLessonPlanDto input)
    {
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        EnsureOwnTeacherRecord(lessonPlan.TeacherId, "create homework from");

        if (lessonPlan.Status != LessonPlanStatus.Approved)
        {
            throw new UserFriendlyException("Homework can be created only from an approved lesson plan.");
        }

        var title = string.IsNullOrWhiteSpace(input.Title)
            ? lessonPlan.Topic
            : input.Title.Trim();

        var description = string.IsNullOrWhiteSpace(input.Description)
            ? BuildDefaultHomeworkDescription(lessonPlan)
            : input.Description.Trim();

        var homework = new CreateHomeworkDto
        {
            StandardId = lessonPlan.StandardId,
            SubjectId = lessonPlan.SubjectId,
            ChapterId = lessonPlan.ChapterId,
            ChapterIds = new List<Guid> { lessonPlan.ChapterId },
            Title = title,
            Description = description,
            HomeworkTypeId = input.HomeworkTypeId,
            SubmissionType = input.SubmissionType,
            DivisionSchedules = new List<CreateHomeworkDivisionScheduleDto>
            {
                new()
                {
                    DivisionId = lessonPlan.DivisionId,
                    PublishAt = input.PublishAt,
                    DueAt = input.DueAt
                }
            }
        };

        return await homeworkAppService.CreateAsync(input.AcademicMediumId, homework, attachmentBlobName: null);
    }

    public async Task<PagedResultDto<TeacherDiaryDto>> GetTeacherDiariesAsync(GetTeacherDiariesInput input)
    {
        var diaryQuery = await teacherDiaryRepository.GetQueryableAsync();
        var planQuery = await lessonPlanRepository.GetQueryableAsync();

        var query =
            from diary in diaryQuery
            join plan in planQuery on diary.LessonPlanId equals plan.Id
            select new { diary, plan };

        if (input.LessonPlanId.HasValue) query = query.Where(x => x.diary.LessonPlanId == input.LessonPlanId.Value);
        if (input.AcademicYearId.HasValue) query = query.Where(x => x.plan.AcademicYearId == input.AcademicYearId.Value);
        if (input.TeacherId.HasValue) query = query.Where(x => x.diary.TeacherId == input.TeacherId.Value);
        if (input.StandardId.HasValue) query = query.Where(x => x.plan.StandardId == input.StandardId.Value);
        if (input.DivisionId.HasValue) query = query.Where(x => x.plan.DivisionId == input.DivisionId.Value);
        if (input.SubjectId.HasValue) query = query.Where(x => x.plan.SubjectId == input.SubjectId.Value);
        if (input.ChapterId.HasValue) query = query.Where(x => x.plan.ChapterId == input.ChapterId.Value);
        if (input.Status.HasValue) query = query.Where(x => x.diary.Status == input.Status.Value);
        if (input.FromDate.HasValue) query = query.Where(x => x.diary.ActualDate.Date >= input.FromDate.Value.Date);
        if (input.ToDate.HasValue) query = query.Where(x => x.diary.ActualDate.Date <= input.ToDate.Value.Date);

        var count = await AsyncExecuter.CountAsync(query);
        var rows = await AsyncExecuter.ToListAsync(
            query
                .OrderByDescending(x => x.diary.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
        );

        var items = await MapTeacherDiariesAsync(rows.Select(x => x.diary));
        return new PagedResultDto<TeacherDiaryDto>(count, items);
    }

    public async Task<TeacherDiaryDto> GetTeacherDiaryAsync(Guid id)
    {
        var diary = await teacherDiaryRepository.GetAsync(id);
        return (await MapTeacherDiariesAsync(new[] { diary })).Single();
    }

    public async Task<TeacherDiaryDto> CreateTeacherDiaryAsync(CreateUpdateTeacherDiaryDto input)
    {
        var plan = await lessonPlanRepository.GetAsync(input.LessonPlanId);
        EnsureOwnTeacherRecord(plan.TeacherId, "create a diary for");

        var existing = await teacherDiaryRepository.FirstOrDefaultAsync(x => x.LessonPlanId == input.LessonPlanId);
        if (existing != null)
        {
            throw new UserFriendlyException("A teacher diary already exists for this lesson plan.");
        }

        var diary = new TeacherDiary(GuidGenerator.Create(), CurrentTenant.Id, plan.Id, plan.TeacherId, input.ActualDate);
        ApplyDiaryInput(diary, input);
        diary.Submit();

        var inserted = await teacherDiaryRepository.InsertAsync(diary, autoSave: true);
        return (await MapTeacherDiariesAsync(new[] { inserted })).Single();
    }

    public async Task<TeacherDiaryDto> UpdateTeacherDiaryAsync(Guid id, CreateUpdateTeacherDiaryDto input)
    {
        var diary = await teacherDiaryRepository.GetAsync(id);
        EnsureOwnTeacherRecord(diary.TeacherId, "modify");
        ApplyDiaryInput(diary, input);
        diary.Submit();

        await teacherDiaryRepository.UpdateAsync(diary);
        return await GetTeacherDiaryAsync(id);
    }

    public async Task<TeacherDiaryDto> SubmitTeacherDiaryAsync(Guid id)
    {
        var diary = await teacherDiaryRepository.GetAsync(id);
        EnsureOwnTeacherRecord(diary.TeacherId, "submit");
        diary.Submit();

        await teacherDiaryRepository.UpdateAsync(diary);
        return await GetTeacherDiaryAsync(id);
    }

    private static IQueryable<LessonPlan> ApplyLessonPlanFilters(IQueryable<LessonPlan> query, GetLessonPlansInput input)
    {
        if (input.AcademicYearId.HasValue) query = query.Where(x => x.AcademicYearId == input.AcademicYearId.Value);
        if (input.TeacherId.HasValue) query = query.Where(x => x.TeacherId == input.TeacherId.Value);
        if (input.StandardId.HasValue) query = query.Where(x => x.StandardId == input.StandardId.Value);
        if (input.DivisionId.HasValue) query = query.Where(x => x.DivisionId == input.DivisionId.Value);
        if (input.SubjectId.HasValue) query = query.Where(x => x.SubjectId == input.SubjectId.Value);
        if (input.ChapterId.HasValue) query = query.Where(x => x.ChapterId == input.ChapterId.Value);
        if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status.Value);
        if (input.FromDate.HasValue) query = query.Where(x => x.PlannedDate.Date >= input.FromDate.Value.Date);
        if (input.ToDate.HasValue) query = query.Where(x => x.PlannedDate.Date <= input.ToDate.Value.Date);

        return query;
    }

    private async Task<List<LessonPlanDto>> MapLessonPlansAsync(IEnumerable<LessonPlan> lessonPlans)
    {
        var list = lessonPlans.ToList();
        var items = ObjectMapper.Map<List<LessonPlan>, List<LessonPlanDto>>(list);

        var teacherIds = list.Select(x => x.TeacherId).Distinct().ToList();
        var standardIds = list.Select(x => x.StandardId).Distinct().ToList();
        var divisionIds = list.Select(x => x.DivisionId).Distinct().ToList();
        var subjectIds = list.Select(x => x.SubjectId).Distinct().ToList();
        var chapterIds = list.Select(x => x.ChapterId).Distinct().ToList();
        var lessonPlanIds = list.Select(x => x.Id).Distinct().ToList();
        var timetableSlotIds = list
            .Where(x => x.TimetableSlotId.HasValue)
            .Select(x => x.TimetableSlotId!.Value)
            .Distinct()
            .ToList();

        var teachers = await employeeRepository.GetListAsync(x => teacherIds.Contains(x.Id));
        var standards = await standardRepository.GetListAsync(x => standardIds.Contains(x.Id));
        var divisions = await divisionRepository.GetListAsync(x => divisionIds.Contains(x.Id));
        var subjects = await subjectRepository.GetListAsync(x => subjectIds.Contains(x.Id));
        var chapters = await chapterRepository.GetListAsync(x => chapterIds.Contains(x.Id));
        var diaries = await teacherDiaryRepository.GetListAsync(x => lessonPlanIds.Contains(x.LessonPlanId));
        var timetableSlots = new List<TimetableSlot>();

        if (timetableSlotIds.Count > 0)
        {
            timetableSlots = await timetableSlotRepository.GetListAsync(x => timetableSlotIds.Contains(x.Id));
        }

        foreach (var item in items)
        {
            var teacher = teachers.FirstOrDefault(x => x.Id == item.TeacherId);
            var standard = standards.FirstOrDefault(x => x.Id == item.StandardId);
            var division = divisions.FirstOrDefault(x => x.Id == item.DivisionId);
            var subject = subjects.FirstOrDefault(x => x.Id == item.SubjectId);
            var chapter = chapters.FirstOrDefault(x => x.Id == item.ChapterId);
            var diary = diaries.FirstOrDefault(x => x.LessonPlanId == item.Id);
            var timetableSlot = item.TimetableSlotId.HasValue
                ? timetableSlots.FirstOrDefault(x => x.Id == item.TimetableSlotId.Value)
                : null;

            item.TeacherName = teacher?.EmployeeName;
            item.StandardName = standard?.Name;
            item.DivisionName = division?.Name;
            item.SubjectName = subject?.Name;
            item.ChapterName = chapter?.ChapterName;
            item.PeriodName = timetableSlot != null ? $"Period {timetableSlot.Sequence}" : item.PeriodName;
            item.StartTime = timetableSlot?.StartTime.ToString("hh:mm tt");
            item.EndTime = timetableSlot?.EndTime.ToString("hh:mm tt");
            item.SlotStartTime = item.StartTime;
            item.SlotEndTime = item.EndTime;
            item.DiaryStatus = diary?.Status ?? default;
            item.ActualCompletionPercentage = diary?.ActualCompletionPercentage ?? 0;
            item.TeacherDiaryId = diary?.Id;

            var insight = await ResolveLessonPlanInsightAsync(
                item.TeacherId,
                item.PlannedDate.Date,
                item.StandardId,
                item.DivisionId
            );
            item.InsightType = insight.Type;
            item.InsightSeverity = insight.Severity;
            item.InsightMessage = insight.Message;
            item.IsPlanCreationBlocked = insight.BlocksPlanCreation;
        }

        return items;
    }

    private async Task<List<TeacherDiaryDto>> MapTeacherDiariesAsync(IEnumerable<TeacherDiary> diaries)
    {
        var list = diaries.ToList();
        var items = ObjectMapper.Map<List<TeacherDiary>, List<TeacherDiaryDto>>(list);
        var teacherIds = list.Select(x => x.TeacherId).Distinct().ToList();
        var teachers = await employeeRepository.GetListAsync(x => teacherIds.Contains(x.Id));

        foreach (var item in items)
        {
            item.TeacherName = teachers.FirstOrDefault(x => x.Id == item.TeacherId)?.EmployeeName;
        }

        return items;
    }

    private static void ApplyDiaryInput(TeacherDiary diary, CreateUpdateTeacherDiaryDto input)
    {
        diary.Update(
            input.ActualDate,
            input.ClassConducted,
            input.ActualTopicCovered,
            input.ActualContentSummary,
            input.ActualCompletionPercentage,
            input.TeachingMethodUsed,
            input.StudentResponse,
            input.ClassUnderstandingLevel,
            input.StudentEngagementLevel,
            input.NeedRevision,
            input.Reflection,
            input.WhatWentWell,
            input.WhatWasDifficult,
            input.StudentsNeedingSupport,
            input.NextClassFollowUp,
            input.RemedialActionRequired,
            input.HomeworkAssigned,
            input.MissedReason,
            input.RescheduledToDate,
            input.IsBackdated,
            input.BackdatedReason,
            input.TeacherRemarks
        );
    }

    private void EnsureOwnTeacherRecord(Guid teacherId, string action)
    {
        if (teacherId != CurrentUser.GetId())
        {
            throw new UserFriendlyException($"You can only {action} your own lesson records.");
        }
    }

    private static TimetableDayOfWeek? ToTimetableDay(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => TimetableDayOfWeek.Monday,
        DayOfWeek.Tuesday => TimetableDayOfWeek.Tuesday,
        DayOfWeek.Wednesday => TimetableDayOfWeek.Wednesday,
        DayOfWeek.Thursday => TimetableDayOfWeek.Thursday,
        DayOfWeek.Friday => TimetableDayOfWeek.Friday,
        DayOfWeek.Saturday => TimetableDayOfWeek.Saturday,
        _ => null
    };

    private async Task<LessonPlanInsight> ResolveLessonPlanInsightAsync(
        Guid teacherId,
        DateTime date,
        Guid standardId,
        Guid divisionId)
    {
        var dateOnly = DateOnly.FromDateTime(date.Date);
        var approvedLeave = (await employeeLeaveRepository.GetListAsync(x =>
                x.EmployeeId == teacherId &&
                x.Status == EmployeeLeaveStatusType.Approved &&
                x.StartDateOnly <= dateOnly &&
                x.EndDateOnly >= dateOnly))
            .OrderByDescending(x => x.HalfDay)
            .FirstOrDefault();

        if (approvedLeave != null)
        {
            return approvedLeave.HalfDay
                ? new LessonPlanInsight(
                    "Leave",
                    "warning",
                    "Approved half-day leave exists for this date. Review the actual leave timing before creating this lesson plan.",
                    false)
                : new LessonPlanInsight(
                    "Leave",
                    "error",
                    "Approved leave exists for this date. Lesson plan creation is blocked for this slot.",
                    true);
        }

        var calendarDay = (await calendarDayRepository.GetListAsync(x =>
                x.Date.Date == date.Date &&
                x.Status == CalendarDayStatus.Active &&
                (x.AppliesToAll ||
                 (x.StandardId == standardId && (!x.DivisionId.HasValue || x.DivisionId == divisionId)) ||
                 (x.DivisionId == divisionId && !x.StandardId.HasValue))))
            .OrderByDescending(x => x.DayType)
            .FirstOrDefault(x => x.DayType != DayType.WorkingDay);

        if (calendarDay == null)
        {
            return LessonPlanInsight.None;
        }

        var type = calendarDay.DayType.ToString();
        var message = calendarDay.DayType switch
        {
            DayType.Holiday => $"Holiday: {calendarDay.Title}. You can still create the lesson plan, but this date is marked as a holiday.",
            DayType.Vacation => $"Vacation: {calendarDay.Title}. You can still create the lesson plan, but verify the academic schedule.",
            DayType.HalfDay => $"Half day: {calendarDay.Title}. Check whether this period falls inside working hours before planning.",
            DayType.EmergencyClosure => $"Emergency closure: {calendarDay.Title}. Verify before creating a lesson plan.",
            _ => $"{type}: {calendarDay.Title}. Review this calendar insight before creating the lesson plan."
        };

        return new LessonPlanInsight(type, "warning", message, false);
    }

    private static string BuildDefaultHomeworkDescription(LessonPlan lessonPlan)
    {
        var parts = new List<string>
        {
            $"Homework from approved lesson plan: {lessonPlan.Topic}."
        };

        if (!string.IsNullOrWhiteSpace(lessonPlan.LearningObjective))
        {
            parts.Add($"Learning objective: {lessonPlan.LearningObjective}");
        }

        if (!string.IsNullOrWhiteSpace(lessonPlan.ResourcesRequired))
        {
            parts.Add($"Reference resources: {lessonPlan.ResourcesRequired}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private sealed record LessonPlanInsight(
        string? Type,
        string? Severity,
        string? Message,
        bool BlocksPlanCreation)
    {
        public static LessonPlanInsight None { get; } = new(null, null, null, false);
    }
}
