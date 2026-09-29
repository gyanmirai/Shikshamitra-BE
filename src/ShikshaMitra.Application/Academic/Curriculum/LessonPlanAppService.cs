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
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace ShikshaMitra.Academic.Curriculum;

[Authorize]
public class LessonPlanAppService(
    IRepository<LessonPlan, Guid> lessonPlanRepository,
    IRepository<TeacherDiary, Guid> teacherDiaryRepository,
    LessonPlanProgressManager lessonPlanProgressManager,
    SubjectTeacherReassignmentManager teacherReassignmentManager,
    ITimetableRepository timetableRepository,
    IRepository<TimetableSlot, Guid> timetableSlotRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<AssignedSubject, Guid> assignedSubjectRepository,
    IRepository<AssignedSubjectChapter, Guid> chapterRepository,
    IRepository<TeacherSubjectAllocation, Guid> teacherSubjectAllocationRepository,
    IRepository<Employee, Guid> employeeRepository,
    IRepository<IdentityUser, Guid> identityUserRepository,
    IRepository<AcademicCalendarDay, Guid> calendarDayRepository,
    IRepository<EmployeeLeave, Guid> employeeLeaveRepository,
    IRepository<EducationMediumStage, Guid> mediumStageRepository,
    IHomeworkAppService homeworkAppService,
    IUserScopeAccessService userScopeAccessService,
    IClassTeacherAccessService classTeacherAccessService,
    IReportingManagerAccessService reportingManagerAccessService
) : ApplicationService, ILessonPlanAppService
{
    private static readonly string[] LessonPlanReviewerRoles =
    {
        "admin",
        "administrator",
        "coordinator",
        "academic coordinator"
    };

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.View)]
    public async Task<PagedResultDto<LessonPlanDto>> GetListAsync(GetLessonPlansInput input)
    {
        var query = await lessonPlanRepository.GetQueryableAsync();
        query = await ApplyLessonPlanFiltersAsync(query, input);
        var unscopedQuery = query;

        HashSet<Guid> accessibleTeacherIds = [];
        if (!IsAdminUser() && !await HasClassTeacherAccessAsync())
        {
            accessibleTeacherIds = await GetCurrentAccessibleTeacherIdsAsync();
            if (accessibleTeacherIds.Count == 0)
            {
                return new PagedResultDto<LessonPlanDto>(0, new List<LessonPlanDto>());
            }

            var scopedQuery = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
            var subjectOwnedPlanIds = await GetTeacherAndReporteeSubjectOwnedPlanIdsAsync(unscopedQuery, accessibleTeacherIds);

            query = scopedQuery
                .Concat(unscopedQuery.Where(plan => accessibleTeacherIds.Contains(plan.TeacherId)))
                .Concat(unscopedQuery.Where(plan => subjectOwnedPlanIds.Contains(plan.Id)))
                .Distinct();

            query = query.Where(plan => accessibleTeacherIds.Contains(plan.TeacherId));
        }

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

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.View)]
    public async Task<LessonPlanTimelineDto> GetTimelineAsync(DateTimeOffset date, Guid? teacherId = null, Guid? academicMediumId = null)
    {
        var targetTeacherId = await ResolveTimelineTeacherIdAsync(teacherId);

        HashSet<Guid> accessibleTeacherIds = [];
        if (!IsAdminUser() && !await HasClassTeacherAccessAsync())
        {
            accessibleTeacherIds = await GetCurrentAccessibleTeacherIdsAsync();
            if (accessibleTeacherIds.Count == 0 || !accessibleTeacherIds.Contains(targetTeacherId))
            {
                throw new AbpAuthorizationException("You are not authorized to view this timeline.");
            }
        }
        // PlannedDate is persisted as an instant. A local midnight can fall on
        // the previous UTC date, so matching either date admits adjacent-day
        // plans. Use one non-overlapping interval for the requested local day.
        var selectedDate = date.Date;
        var dayStartUtc = new DateTimeOffset(selectedDate, date.Offset).UtcDateTime;
        var dayEndUtc = new DateTimeOffset(selectedDate.AddDays(1), date.Offset).UtcDateTime;
        var lessonPlansQuery = await lessonPlanRepository.GetQueryableAsync();
        lessonPlansQuery = lessonPlansQuery.Where(x => x.TeacherId == targetTeacherId &&
            x.PlannedDate >= dayStartUtc && x.PlannedDate < dayEndUtc);

        List<Guid>? allowedStandardIds = null;
        if (academicMediumId.HasValue)
        {
            var mediumStageQuery = await mediumStageRepository.GetQueryableAsync();
            var stageIds = await AsyncExecuter.ToListAsync(mediumStageQuery
                .Where(m => m.EducationMediumId == academicMediumId.Value)
                .Select(m => m.Id));

            var standardQuery = await standardRepository.GetQueryableAsync();
            allowedStandardIds = await AsyncExecuter.ToListAsync(standardQuery
                .Where(s => stageIds.Contains(s.EducationMediumStageId))
                .Select(s => s.Id));

            lessonPlansQuery = lessonPlansQuery.Where(lp => allowedStandardIds.Contains(lp.StandardId));
        }

        var unscopedLessonPlansQuery = lessonPlansQuery;
        if (!IsAdminUser() && !await HasClassTeacherAccessAsync())
        {
            var scopedLessonPlansQuery = await userScopeAccessService.ApplyCurrentUserScopeAsync(lessonPlansQuery);
            var subjectOwnedPlanIds = await GetTeacherAndReporteeSubjectOwnedPlanIdsAsync(unscopedLessonPlansQuery, accessibleTeacherIds);

            lessonPlansQuery = scopedLessonPlansQuery
                .Concat(unscopedLessonPlansQuery.Where(plan => accessibleTeacherIds.Contains(plan.TeacherId)))
                .Concat(unscopedLessonPlansQuery.Where(plan => subjectOwnedPlanIds.Contains(plan.Id)))
                .Distinct();
        }

        var lessonPlans = await AsyncExecuter.ToListAsync(lessonPlansQuery);

        // A rejected plan remains available in list/history views, but once its
        // timetable slot has a replacement it must not compete with the active
        // plan for the single daily-timeline card.
        var supersededRejectedPlanIds = lessonPlans
            .Where(plan =>
                plan.Status == LessonPlanStatus.Rejected &&
                plan.TimetableSlotId.HasValue &&
                lessonPlans.Any(replacement =>
                    replacement.Id != plan.Id &&
                    replacement.Status != LessonPlanStatus.Rejected &&
                    replacement.TimetableSlotId == plan.TimetableSlotId &&
                    replacement.StandardId == plan.StandardId &&
                    replacement.DivisionId == plan.DivisionId &&
                    replacement.SubjectId == plan.SubjectId))
            .Select(plan => plan.Id)
            .ToHashSet();
        lessonPlans = lessonPlans
            .Where(plan => !supersededRejectedPlanIds.Contains(plan.Id))
            .ToList();

        var mappedPlans = await MapLessonPlansAsync(lessonPlans);
        var mappedById = mappedPlans.ToDictionary(x => x.Id);
        var matchedPlanIds = new HashSet<Guid>();
        var slots = new List<TimelineSlotDto>();
        var timetableDay = ToTimetableDay(selectedDate.DayOfWeek);

        if (timetableDay != null)
        {
            // Project only this teacher's periods for the requested weekday. Starting
            // at the timetable root preserves ABP's tenant filter for child slots.
            var timetableQuery = await timetableRepository.GetQueryableAsync();
            var teachingSlotsQuery =
                from timetable in timetableQuery
                where timetable.Status == TimetableStatus.Published
                from slot in timetable.Slots
                where slot.Day == timetableDay.Value
                from slotSubject in slot.Subjects
                where slotSubject.TeacherId == targetTeacherId
                orderby timetable.Id, slot.Sequence, slot.Id, slotSubject.Id
                select new
                {
                    timetable.StandardId,
                    timetable.DivisionId,
                    TimetableSlotId = slot.Id,
                    slot.Sequence,
                    slot.StartTime,
                    slot.EndTime,
                    slotSubject.SubjectId
                };
            if (allowedStandardIds != null)
            {
                teachingSlotsQuery = teachingSlotsQuery.Where(x => allowedStandardIds.Contains(x.StandardId));
            }
            var teachingSlots = await AsyncExecuter.ToListAsync(teachingSlotsQuery);
            if (teachingSlots.Count > 0)
            {
                var standardIds = teachingSlots.Select(x => x.StandardId).Distinct().ToList();
                var divisionIds = teachingSlots.Select(x => x.DivisionId).Distinct().ToList();
                var subjectIds = teachingSlots.Select(x => x.SubjectId).Distinct().ToList();
                var standards = await AsyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync())
                    .Where(x => standardIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
                var divisions = await AsyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync())
                    .Where(x => divisionIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
                var subjects = await AsyncExecuter.ToListAsync((await subjectRepository.GetQueryableAsync())
                    .Where(x => subjectIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));

                var selectedDateOnly = DateOnly.FromDateTime(selectedDate);
                var approvedLeave = (await employeeLeaveRepository.GetListAsync(x =>
                        x.EmployeeId == targetTeacherId &&
                        x.Status == EmployeeLeaveStatusType.Approved &&
                        x.StartDateOnly <= selectedDateOnly &&
                        x.EndDateOnly >= selectedDateOnly))
                    .OrderByDescending(x => x.HalfDay)
                    .FirstOrDefault();
                var calendarDays = approvedLeave == null
                    ? await calendarDayRepository.GetListAsync(day =>
                        day.Date >= selectedDate && day.Date < selectedDate.AddDays(1) &&
                        day.Status == CalendarDayStatus.Active &&
                        (day.AppliesToAll ||
                         (day.StandardId.HasValue && standardIds.Contains(day.StandardId.Value)) ||
                         (day.DivisionId.HasValue && divisionIds.Contains(day.DivisionId.Value))))
                    : [];
                var insightsByClass = new Dictionary<(Guid StandardId, Guid DivisionId), LessonPlanInsight>();

                foreach (var slot in teachingSlots)
                {
                    var standard = standards.FirstOrDefault(s => s.Id == slot.StandardId);
                    var division = divisions.FirstOrDefault(d => d.Id == slot.DivisionId);
                    var subject = subjects.FirstOrDefault(sub => sub.Id == slot.SubjectId);
                    var slotStartTime = slot.StartTime.ToString("hh:mm tt");
                    var slotEndTime = slot.EndTime.ToString("hh:mm tt");
                    var plan = lessonPlans.FirstOrDefault(lp =>
                        !matchedPlanIds.Contains(lp.Id) &&
                        lp.TimetableSlotId == slot.TimetableSlotId &&
                        MatchesTimelineContext(lp, targetTeacherId, slot.StandardId, slot.DivisionId, slot.SubjectId));

                    // A saved timetable can replace slot IDs. Recover a plan only
                    // when its date, teacher, class and subject match unambiguously.
                    if (plan == null)
                    {
                        var contextualPlans = lessonPlans
                            .Where(lp =>
                                !matchedPlanIds.Contains(lp.Id) &&
                                lp.StandardId == slot.StandardId &&
                                lp.DivisionId == slot.DivisionId &&
                                lp.SubjectId == slot.SubjectId &&
                                lp.TimetableSlotId != slot.TimetableSlotId)
                            .ToList();
                        var timeMatchedPlans = contextualPlans
                            .Where(lp =>
                                mappedById.TryGetValue(lp.Id, out var dto) &&
                                dto.StartTime == slotStartTime &&
                                dto.EndTime == slotEndTime)
                            .ToList();

                        if (timeMatchedPlans.Count == 1)
                        {
                            plan = timeMatchedPlans[0];
                        }
                        else if (contextualPlans.Count == 1)
                        {
                            plan = contextualPlans[0];
                        }
                    }

                    LessonPlanDto? mappedPlan = null;
                    if (plan != null && mappedById.TryGetValue(plan.Id, out var dto))
                    {
                        mappedPlan = dto;
                        matchedPlanIds.Add(dto.Id);
                    }

                    var classKey = (slot.StandardId, slot.DivisionId);
                    if (!insightsByClass.TryGetValue(classKey, out var insight))
                    {
                        var calendarDay = calendarDays
                            .Where(day => day.AppliesToAll ||
                                (day.StandardId == slot.StandardId &&
                                 (!day.DivisionId.HasValue || day.DivisionId == slot.DivisionId)) ||
                                (day.DivisionId == slot.DivisionId && !day.StandardId.HasValue))
                            .OrderByDescending(day => day.DayType)
                            .FirstOrDefault(day => day.DayType != DayType.WorkingDay);
                        insight = approvedLeave != null
                            ? CreateLeaveInsight(approvedLeave)
                            : CreateCalendarInsight(calendarDay);
                        insightsByClass[classKey] = insight;
                    }

                    slots.Add(new TimelineSlotDto
                    {
                        TimetableSlotId = slot.TimetableSlotId,
                        PeriodName = $"Period {slot.Sequence}",
                        StartTime = slotStartTime,
                        EndTime = slotEndTime,
                        StandardId = slot.StandardId,
                        StandardName = standard?.Name ?? "Class",
                        DivisionId = slot.DivisionId,
                        DivisionName = division?.Name ?? "Division",
                        SubjectId = slot.SubjectId,
                        SubjectName = subject?.Name ?? "Subject",
                        IsTimetableSlotOnly = plan == null,
                        LessonPlan = mappedPlan,
                        InsightType = insight.Type,
                        InsightSeverity = insight.Severity,
                        InsightMessage = insight.Message,
                        IsPlanCreationBlocked = insight.BlocksPlanCreation
                    });
                }
            }
        }

        foreach (var plan in mappedPlans.Where(plan => !matchedPlanIds.Contains(plan.Id)))
        {
            slots.Add(new TimelineSlotDto
            {
                TimetableSlotId = plan.TimetableSlotId ?? plan.Id,
                PeriodName = string.IsNullOrWhiteSpace(plan.PeriodName) ? "Manual Plan" : plan.PeriodName,
                StartTime = plan.StartTime ?? string.Empty,
                EndTime = plan.EndTime ?? string.Empty,
                StandardId = plan.StandardId,
                StandardName = plan.StandardName ?? "Class",
                DivisionId = plan.DivisionId,
                DivisionName = plan.DivisionName ?? "Division",
                SubjectId = plan.SubjectId,
                SubjectName = plan.SubjectName ?? "Subject",
                IsTimetableSlotOnly = false,
                LessonPlan = plan,
                InsightType = plan.InsightType,
                InsightSeverity = plan.InsightSeverity,
                InsightMessage = plan.InsightMessage,
                IsPlanCreationBlocked = plan.IsPlanCreationBlocked
            });
        }

        return new LessonPlanTimelineDto
        {
            Date = selectedDate,
            TeacherId = targetTeacherId,
            Slots = slots
                .OrderBy(s => string.IsNullOrWhiteSpace(s.StartTime))
                .ThenBy(s => s.StartTime)
                .ThenBy(s => s.PeriodName)
                .ToList()
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.View)]
    public async Task<LessonPlanDto> GetAsync(Guid id)
    {
        var plan = await lessonPlanRepository.GetAsync(id);
        await EnsureLessonPlanAccessAsync(plan, "lesson-plan-read");

        if (!IsAdminUser() && !await HasClassTeacherAccessAsync())
        {
            var accessibleTeacherIds = await GetCurrentAccessibleTeacherIdsAsync();
            if (accessibleTeacherIds.Count == 0 || !accessibleTeacherIds.Contains(plan.TeacherId))
            {
                throw new AbpAuthorizationException("You are not authorized to view this lesson plan.");
            }
        }

        return (await MapLessonPlansAsync(new[] { plan })).Single();
    }

    private static bool MatchesTimelineContext(
        LessonPlan plan,
        Guid teacherId,
        Guid standardId,
        Guid divisionId,
        Guid subjectId) =>
        plan.TeacherId == teacherId &&
        plan.StandardId == standardId &&
        plan.DivisionId == divisionId &&
        plan.SubjectId == subjectId;

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.View)]
    public async Task<LessonPlanChapterProgressDto> GetChapterProgressAsync(GetLessonPlanChapterProgressInput input)
    {
        await EnsureChapterMatchesPlanTargetAsync(input.ChapterId, input.StandardId, input.SubjectId);
        // Authorize the requested academic context without persisting a plan or
        // using an arbitrary teacher's ownership to bypass resource scope.
        var target = new LessonPlan(Guid.Empty, CurrentTenant.Id, input.AcademicYearId,
            Guid.Empty, input.StandardId, input.DivisionId, input.SubjectId, input.ChapterId,
            Clock.Now, string.Empty, 0);
        await EnsureLessonPlanAccessAsync(target, "lesson-plan-chapter-progress");
        return new LessonPlanChapterProgressDto
        {
            MinimumCompletionPercentage = await lessonPlanProgressManager.GetMinimumAsync(
                input.AcademicYearId, input.StandardId, input.DivisionId, input.SubjectId, input.ChapterId)
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.Add)]
    public async Task<LessonPlanDto> CreateAsync(CreateLessonPlanDto input)
    {
        await EnsureOwnTeacherRecordAsync(input.TeacherId, "create");
        await EnsureChapterMatchesPlanTargetAsync(input.ChapterId, input.StandardId, input.SubjectId);

        // PlannedDate is a school-calendar date selected in the browser. Preserve the
        // submitted offset for calendar validation, then persist its UTC instant.
        // Converting to DateTime before taking Date shifts positive-offset midnights
        // into the previous UTC date (for example, 15 Sep IST becomes 14 Sep UTC).
        var plannedLocalDate = input.PlannedDate.Date;
        var plannedDateUtc = input.PlannedDate.UtcDateTime;
        await EnsureLessonPlanCanBeCreatedForLocalDateAsync(input.TeacherId, plannedLocalDate, input.StandardId, input.DivisionId);
        await EnsureUniqueLessonPlanAsync(input.TeacherId, plannedDateUtc, input.Topic, input.TimetableSlotId);

        var lessonPlan = new LessonPlan(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.AcademicYearId,
            input.TeacherId,
            input.StandardId,
            input.DivisionId,
            input.SubjectId,
            input.ChapterId,
            plannedDateUtc,
            input.Topic,
            input.PlannedCompletionPercentage,
            input.TimetableSlotId,
            input.IsAiGenerated
        );

        await teacherReassignmentManager.EnsureCurrentTimetableTeacherAsync(lessonPlan);

        lessonPlan.UpdatePlan(
            input.ChapterId,
            input.Topic,
            input.LearningObjective,
            input.TeachingMethod,
            input.ResourcesRequired,
            input.PlannedCompletionPercentage
        );

        if (input.Status == LessonPlanStatus.Submitted)
        {
            lessonPlan.Submit();
        }

        await EnsureLessonPlanAccessAsync(lessonPlan, "lesson-plan-create");
        await lessonPlanProgressManager.EnsureTargetAsync(lessonPlan, input.ChapterId, input.PlannedCompletionPercentage);
        var inserted = await lessonPlanRepository.InsertAsync(lessonPlan, autoSave: true);
        return (await MapLessonPlansAsync(new[] { inserted })).Single();
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.Add)]
    public async Task<LessonPlanDto> UpdateAsync(Guid id, UpdateLessonPlanDto input)
    {
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        await EnsureLessonPlanAccessAsync(lessonPlan, "lesson-plan-update");
        await EnsureCurrentTeacherOwnsLessonPlanAsync(lessonPlan, "revise");
        EnsureLessonPlanEditable(lessonPlan);
        await EnsureChapterMatchesPlanTargetAsync(input.ChapterId, lessonPlan.StandardId, lessonPlan.SubjectId);
        await lessonPlanProgressManager.EnsureTargetAsync(lessonPlan, input.ChapterId, input.PlannedCompletionPercentage);

        await EnsureUniqueLessonPlanAsync(
            lessonPlan.TeacherId,
            lessonPlan.PlannedDate,
            input.Topic,
            lessonPlan.TimetableSlotId,
            lessonPlan.Id
        );

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

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.Add)]
    public async Task<LessonPlanDto> SubmitAsync(Guid id)
    {
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        await EnsureLessonPlanAccessAsync(lessonPlan, "lesson-plan-submit");
        await EnsureCurrentTeacherOwnsLessonPlanAsync(lessonPlan, "submit");
        EnsureLessonPlanSubmittable(lessonPlan);
        await EnsureChapterMatchesPlanTargetAsync(lessonPlan.ChapterId, lessonPlan.StandardId, lessonPlan.SubjectId);
        await EnsureLessonPlanCanBeCreatedForLocalDateAsync(
            lessonPlan.TeacherId,
            LessonPlanCalendarDate.FromStoredInstant(lessonPlan.PlannedDate),
            lessonPlan.StandardId,
            lessonPlan.DivisionId);
        await EnsureUniqueLessonPlanAsync(lessonPlan.TeacherId, lessonPlan.PlannedDate, lessonPlan.Topic, lessonPlan.TimetableSlotId, lessonPlan.Id);
        await lessonPlanProgressManager.EnsureTargetAsync(lessonPlan, lessonPlan.ChapterId, lessonPlan.PlannedCompletionPercentage);

        lessonPlan.Submit();
        await lessonPlanRepository.UpdateAsync(lessonPlan);
        return await GetAsync(id);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.Add)]
    public async Task BulkSubmitAsync(BulkSubmitLessonPlansDto input)
    {
        foreach (var id in input.LessonPlanIds)
        {
            var plan = await lessonPlanRepository.GetAsync(id);
            await EnsureLessonPlanAccessAsync(plan, "lesson-plan-submit");
            await EnsureCurrentTeacherOwnsLessonPlanAsync(plan, "submit");
            EnsureLessonPlanSubmittable(plan);
            await EnsureChapterMatchesPlanTargetAsync(plan.ChapterId, plan.StandardId, plan.SubjectId);
            await EnsureLessonPlanCanBeCreatedForLocalDateAsync(
                plan.TeacherId,
                LessonPlanCalendarDate.FromStoredInstant(plan.PlannedDate),
                plan.StandardId,
                plan.DivisionId);
            await EnsureUniqueLessonPlanAsync(plan.TeacherId, plan.PlannedDate, plan.Topic, plan.TimetableSlotId, plan.Id);
            await lessonPlanProgressManager.EnsureTargetAsync(plan, plan.ChapterId, plan.PlannedCompletionPercentage);
            plan.Submit();
            await lessonPlanRepository.UpdateAsync(plan);
        }
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.Add)]
    public async Task<LessonPlanDto> ApproveAsync(Guid id, ReviewRemarksDto input)
    {
        await EnsureCanReviewLessonPlansAsync();
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        await EnsureLessonPlanAccessAsync(lessonPlan, "lesson-plan-approve");
        await EnsureReviewerIsNotPlanOwnerAsync(lessonPlan);

        if (!await HasClassTeacherAccessAsync() && !await IsReportingManagerOrAdminAsync(lessonPlan.TeacherId))
        {
            throw new AbpAuthorizationException("You can only review lesson plans of employees in your reporting hierarchy.");
        }

        if (lessonPlan.Status == LessonPlanStatus.Approved)
        {
            return await GetAsync(id);
        }

        EnsurePlanIsAwaitingReview(lessonPlan);

        lessonPlan.Approve(CurrentUser.GetId(), input.Comment);
        await lessonPlanRepository.UpdateAsync(lessonPlan);
        return await GetAsync(id);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.Add)]
    public async Task<LessonPlanDto> RejectAsync(Guid id, ReviewRemarksDto input)
    {
        await EnsureCanReviewLessonPlansAsync();
        EnsureReviewCommentPresent(input, "reject");
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        await EnsureLessonPlanAccessAsync(lessonPlan, "lesson-plan-reject");
        await EnsureReviewerIsNotPlanOwnerAsync(lessonPlan);

        if (!await HasClassTeacherAccessAsync() && !await IsReportingManagerOrAdminAsync(lessonPlan.TeacherId))
        {
            throw new AbpAuthorizationException("You can only review lesson plans of employees in your reporting hierarchy.");
        }

        if (lessonPlan.Status == LessonPlanStatus.Rejected)
        {
            return await GetAsync(id);
        }

        EnsurePlanIsAwaitingReview(lessonPlan);

        lessonPlan.Reject(CurrentUser.GetId(), input.Comment);
        await lessonPlanRepository.UpdateAsync(lessonPlan);
        return await GetAsync(id);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.Add)]
    public async Task<LessonPlanDto> RequestChangesAsync(Guid id, ReviewRemarksDto input)
    {
        await EnsureCanReviewLessonPlansAsync();
        EnsureReviewCommentPresent(input, "request changes for");
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        await EnsureLessonPlanAccessAsync(lessonPlan, "lesson-plan-request-changes");
        await EnsureReviewerIsNotPlanOwnerAsync(lessonPlan);

        if (!await HasClassTeacherAccessAsync() && !await IsReportingManagerOrAdminAsync(lessonPlan.TeacherId))
        {
            throw new AbpAuthorizationException("You can only review lesson plans of employees in your reporting hierarchy.");
        }

        if (lessonPlan.Status == LessonPlanStatus.NeedsChanges)
        {
            return await GetAsync(id);
        }

        EnsurePlanIsAwaitingReview(lessonPlan);

        lessonPlan.RequestChanges(CurrentUser.GetId(), input.Comment);
        await lessonPlanRepository.UpdateAsync(lessonPlan);
        return await GetAsync(id);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.Add)]
    public async Task<HomeworkCreateResponseDto> CreateHomeworkFromLessonPlanAsync(Guid id, CreateHomeworkFromLessonPlanDto input)
    {
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        await EnsureLessonPlanAccessAsync(lessonPlan, "lesson-plan-create-homework");
        await EnsureOwnTeacherRecordAsync(lessonPlan.TeacherId, "create homework from");

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

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.View)]
    public async Task<PagedResultDto<TeacherDiaryDto>> GetTeacherDiariesAsync(GetTeacherDiariesInput input)
    {
        var diaryQuery = await teacherDiaryRepository.GetQueryableAsync();
        var planQuery = await lessonPlanRepository.GetQueryableAsync();
        var unscopedPlanQuery = planQuery;

        HashSet<Guid> accessibleTeacherIds = [];
        if (!IsAdminUser() && !await HasClassTeacherAccessAsync())
        {
            accessibleTeacherIds = await GetCurrentAccessibleTeacherIdsAsync();
            if (accessibleTeacherIds.Count == 0)
            {
                return new PagedResultDto<TeacherDiaryDto>(0, new List<TeacherDiaryDto>());
            }

            var scopedPlanQuery = await userScopeAccessService.ApplyCurrentUserScopeAsync(planQuery);
            var subjectOwnedPlanIds = await GetTeacherAndReporteeSubjectOwnedPlanIdsAsync(unscopedPlanQuery, accessibleTeacherIds);

            planQuery = scopedPlanQuery
                .Concat(unscopedPlanQuery.Where(plan => accessibleTeacherIds.Contains(plan.TeacherId)))
                .Concat(unscopedPlanQuery.Where(plan => subjectOwnedPlanIds.Contains(plan.Id)))
                .Distinct();
        }

        var query =
            from diary in diaryQuery
            join plan in planQuery on diary.LessonPlanId equals plan.Id
            select new { diary, plan };

        if (input.AcademicMediumId.HasValue)
        {
            var mediumStageQuery = await mediumStageRepository.GetQueryableAsync();
            var stageIds = await AsyncExecuter.ToListAsync(mediumStageQuery
                .Where(m => m.EducationMediumId == input.AcademicMediumId.Value)
                .Select(m => m.Id));

            var standardQuery = await standardRepository.GetQueryableAsync();
            var allowedStandardIds = await AsyncExecuter.ToListAsync(standardQuery
                .Where(s => stageIds.Contains(s.EducationMediumStageId))
                .Select(s => s.Id));

            query = query.Where(x => allowedStandardIds.Contains(x.plan.StandardId));
        }

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

        if (!IsAdminUser() && !await HasClassTeacherAccessAsync())
        {
            query = query.Where(x => accessibleTeacherIds.Contains(x.diary.TeacherId));
        }

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

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.View)]
    public async Task<TeacherDiaryDto> GetTeacherDiaryAsync(Guid id)
    {
        var diary = await teacherDiaryRepository.GetAsync(id);
        var diaryPlan = await lessonPlanRepository.GetAsync(diary.LessonPlanId);
        await EnsureLessonPlanAccessAsync(diaryPlan, "teacher-diary-read");

        if (!IsAdminUser() && !await HasClassTeacherAccessAsync())
        {
            var accessibleTeacherIds = await GetCurrentAccessibleTeacherIdsAsync();
            if (accessibleTeacherIds.Count == 0 || !accessibleTeacherIds.Contains(diary.TeacherId))
            {
                throw new AbpAuthorizationException("You are not authorized to view this diary entry.");
            }
        }

        return (await MapTeacherDiariesAsync(new[] { diary })).Single();
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.Add)]
    public async Task<TeacherDiaryDto> CreateTeacherDiaryAsync(CreateUpdateTeacherDiaryDto input)
    {
        var plan = await lessonPlanRepository.GetAsync(input.LessonPlanId);
        await EnsureLessonPlanAccessAsync(plan, "teacher-diary-create");
        await EnsureOwnTeacherRecordAsync(plan.TeacherId, "create a diary for");
        await EnsureTeacherDiaryAllowedAsync(plan, input);

        var existing = await teacherDiaryRepository.FirstOrDefaultAsync(x => x.LessonPlanId == input.LessonPlanId);
        if (existing != null)
        {
            throw new UserFriendlyException("A teacher diary already exists for this lesson plan.");
        }

        var diary = new TeacherDiary(GuidGenerator.Create(), CurrentTenant.Id, plan.Id, plan.TeacherId, input.ActualDate.UtcDateTime);
        ApplyDiaryInput(diary, input);

        var inserted = await teacherDiaryRepository.InsertAsync(diary, autoSave: true);
        return (await MapTeacherDiariesAsync(new[] { inserted })).Single();
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.Add)]
    public async Task<TeacherDiaryDto> UpdateTeacherDiaryAsync(Guid id, CreateUpdateTeacherDiaryDto input)
    {
        var diary = await teacherDiaryRepository.GetAsync(id);
        var existingPlan = await lessonPlanRepository.GetAsync(diary.LessonPlanId);
        await EnsureLessonPlanAccessAsync(existingPlan, "teacher-diary-update");
        await EnsureOwnTeacherRecordAsync(diary.TeacherId, "modify");

        if (input.LessonPlanId != diary.LessonPlanId)
        {
            throw new UserFriendlyException("Teacher diary cannot be moved to a different lesson plan.");
        }

        var plan = await lessonPlanRepository.GetAsync(input.LessonPlanId);
        await EnsureTeacherDiaryAllowedAsync(plan, input);
        ApplyDiaryInput(diary, input);

        await teacherDiaryRepository.UpdateAsync(diary);
        return await GetTeacherDiaryAsync(id);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_LessonPlan.Add)]
    public async Task<TeacherDiaryDto> SubmitTeacherDiaryAsync(Guid id)
    {
        var diary = await teacherDiaryRepository.GetAsync(id);
        var plan = await lessonPlanRepository.GetAsync(diary.LessonPlanId);
        await EnsureLessonPlanAccessAsync(plan, "teacher-diary-submit");
        await EnsureOwnTeacherRecordAsync(diary.TeacherId, "submit");
        diary.Submit();

        await teacherDiaryRepository.UpdateAsync(diary);
        return await GetTeacherDiaryAsync(id);
    }

    private async Task<IQueryable<LessonPlan>> ApplyLessonPlanFiltersAsync(IQueryable<LessonPlan> query, GetLessonPlansInput input)
    {
        if (input.AcademicMediumId.HasValue)
        {
            var mediumStageQuery = await mediumStageRepository.GetQueryableAsync();
            var stageIds = await AsyncExecuter.ToListAsync(mediumStageQuery
                .Where(m => m.EducationMediumId == input.AcademicMediumId.Value)
                .Select(m => m.Id));

            var standardQuery = await standardRepository.GetQueryableAsync();
            var allowedStandardIds = await AsyncExecuter.ToListAsync(standardQuery
                .Where(s => stageIds.Contains(s.EducationMediumStageId))
                .Select(s => s.Id));

            query = query.Where(lp => allowedStandardIds.Contains(lp.StandardId));
        }

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
        if (list.Count == 0)
        {
            return items;
        }

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

        // Name lookups must not materialize the setup graph: its auto-includes traverse
        // the academic hierarchy and multiply rows even for a single class or subject.
        var teachers = await AsyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
            .Where(x => teacherIds.Contains(x.Id)).Select(x => new LessonEmployeeName(x.Id, x.UserId, x.FirstName, x.LastName)));
        var standards = await AsyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync())
            .Where(x => standardIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
        var divisions = await AsyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync())
            .Where(x => divisionIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
        var subjects = await AsyncExecuter.ToListAsync((await subjectRepository.GetQueryableAsync())
            .Where(x => subjectIds.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
        var chapters = await AsyncExecuter.ToListAsync((await chapterRepository.GetQueryableAsync())
            .Where(x => chapterIds.Contains(x.Id)).Select(x => new { x.Id, x.ChapterName }));
        var diaries = await AsyncExecuter.ToListAsync((await teacherDiaryRepository.GetQueryableAsync())
            .Where(x => lessonPlanIds.Contains(x.LessonPlanId))
            .Select(x => new { x.Id, x.LessonPlanId, x.Status, x.ActualCompletionPercentage }));
        var reviewerIds = list
            .Where(x => x.ReviewedBy.HasValue)
            .Select(x => x.ReviewedBy!.Value)
            .Distinct()
            .ToList();
        var reviewers = reviewerIds.Count > 0
            ? await AsyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
                .Where(x => x.UserId.HasValue && reviewerIds.Contains(x.UserId.Value))
                .Select(x => new LessonEmployeeName(x.Id, x.UserId, x.FirstName, x.LastName)))
            : new List<LessonEmployeeName>();
        var reviewerUsers = reviewerIds.Count > 0
            ? await AsyncExecuter.ToListAsync((await identityUserRepository.GetQueryableAsync())
                .Where(x => reviewerIds.Contains(x.Id))
                .Select(x => new LessonIdentityName(x.Id, x.Name, x.Surname, x.UserName)))
            : new List<LessonIdentityName>();
        var timetableSlots = new List<LessonSlotLookup>();

        if (timetableSlotIds.Count > 0)
        {
            timetableSlots = await AsyncExecuter.ToListAsync((await timetableSlotRepository.GetQueryableAsync())
                .Where(x => timetableSlotIds.Contains(x.Id))
                .Select(x => new LessonSlotLookup(x.Id, x.Sequence, x.StartTime, x.EndTime)));
        }

        var insights = await LoadLessonPlanInsightsAsync(list);
        foreach (var item in items)
        {
            var teacher = teachers.FirstOrDefault(x => x.Id == item.TeacherId);
            var standard = standards.FirstOrDefault(x => x.Id == item.StandardId);
            var division = divisions.FirstOrDefault(x => x.Id == item.DivisionId);
            var subject = subjects.FirstOrDefault(x => x.Id == item.SubjectId);
            var chapter = chapters.FirstOrDefault(x => x.Id == item.ChapterId);
            var reviewer = item.ReviewedBy.HasValue
                ? reviewers.FirstOrDefault(x => x.UserId == item.ReviewedBy.Value)
                : null;
            var reviewerUser = item.ReviewedBy.HasValue
                ? reviewerUsers.FirstOrDefault(x => x.Id == item.ReviewedBy.Value)
                : null;
            var diary = diaries.FirstOrDefault(x => x.LessonPlanId == item.Id);
            var timetableSlot = item.TimetableSlotId.HasValue
                ? timetableSlots.FirstOrDefault(x => x.Id == item.TimetableSlotId.Value)
                : null;

            item.TeacherName = teacher?.EmployeeName;
            item.StandardName = standard?.Name;
            item.DivisionName = division?.Name;
            item.SubjectName = subject?.Name;
            item.ChapterName = chapter?.ChapterName;
            item.ReviewedByName = reviewer?.EmployeeName ?? GetIdentityUserDisplayName(reviewerUser);
            item.PeriodName = timetableSlot != null ? $"Period {timetableSlot.Sequence}" : item.PeriodName;
            item.StartTime = timetableSlot?.StartTime.ToString("hh:mm tt");
            item.EndTime = timetableSlot?.EndTime.ToString("hh:mm tt");
            item.SlotStartTime = item.StartTime;
            item.SlotEndTime = item.EndTime;
            item.DiaryStatus = diary?.Status ?? default;
            item.ActualCompletionPercentage = diary?.ActualCompletionPercentage ?? 0;
            item.TeacherDiaryId = diary?.Id;

            var insight = insights[item.Id];
            item.InsightType = insight.Type;
            item.InsightSeverity = insight.Severity;
            item.InsightMessage = insight.Message;
            item.IsPlanCreationBlocked = insight.BlocksPlanCreation;
        }

        return items;
    }

    private sealed record LessonEmployeeName(Guid Id, Guid? UserId, string? FirstName, string? LastName)
    {
        public string EmployeeName => string.Join(" ", new[] { FirstName, LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private sealed record LessonIdentityName(Guid Id, string? Name, string? Surname, string UserName);
    private sealed record LessonSlotLookup(Guid Id, int Sequence, TimeOnly StartTime, TimeOnly EndTime);

    private static string? GetIdentityUserDisplayName(LessonIdentityName? user)
    {
        if (user == null)
        {
            return null;
        }

        var fullName = string.Join(
            " ",
            new[] { user.Name, user.Surname }.Where(x => !string.IsNullOrWhiteSpace(x))
        );

        return string.IsNullOrWhiteSpace(fullName) ? user.UserName : fullName;
    }

    private async Task<List<TeacherDiaryDto>> MapTeacherDiariesAsync(IEnumerable<TeacherDiary> diaries)
    {
        var list = diaries.ToList();
        var items = ObjectMapper.Map<List<TeacherDiary>, List<TeacherDiaryDto>>(list);
        var teacherIds = list.Select(x => x.TeacherId).Distinct().ToList();
        var teachers = await AsyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
            .Where(x => teacherIds.Contains(x.Id)).Select(x => new LessonEmployeeName(x.Id, x.UserId, x.FirstName, x.LastName)));

        foreach (var item in items)
        {
            item.TeacherName = teachers.FirstOrDefault(x => x.Id == item.TeacherId)?.EmployeeName;
        }

        return items;
    }

    private static void ApplyDiaryInput(TeacherDiary diary, CreateUpdateTeacherDiaryDto input)
    {
        diary.Update(
            input.ActualDate.UtcDateTime,
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
            input.RescheduledToDate?.UtcDateTime,
            input.IsBackdated,
            input.BackdatedReason,
            input.TeacherRemarks
        );
    }

    private async Task EnsureLessonPlanAccessAsync(LessonPlan lessonPlan, string operation)
    {
        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        if (UserScopeQueryExtensions.HasAccessToEntity(lessonPlan, scope))
        {
            return;
        }

        if (IsAdminUser() || await HasClassTeacherAccessAsync())
        {
            return;
        }

        var accessibleTeacherIds = await GetCurrentAccessibleTeacherIdsAsync();
        if (accessibleTeacherIds.Contains(lessonPlan.TeacherId))
        {
            return;
        }

        var assignmentQuery = await assignedSubjectRepository.WithDetailsAsync(
            assignment => assignment.Teachers);
        var assignment = await AsyncExecuter.FirstOrDefaultAsync(
            assignmentQuery.Where(candidate =>
                candidate.StandardId == lessonPlan.StandardId &&
                candidate.SubjectId == lessonPlan.SubjectId));

        var assignedBySubject = assignment != null &&
            assignment.Teachers.Any(teacher => accessibleTeacherIds.Contains(teacher.TeacherId));

        var assignedByDivisionAllocation = await teacherSubjectAllocationRepository.AnyAsync(
            allocation =>
                allocation.StandardId == lessonPlan.StandardId &&
                allocation.DivisionId == lessonPlan.DivisionId &&
                allocation.SubjectId == lessonPlan.SubjectId &&
                allocation.Teachers.Any(teacher => accessibleTeacherIds.Contains(teacher.TeacherId)));

        if (!assignedBySubject && !assignedByDivisionAllocation)
        {
            userScopeAccessService.EnsureCanAccess(lessonPlan, scope, operation: operation);
        }
    }

    private Task<Employee?> GetCurrentEmployeeAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            return Task.FromResult<Employee?>(null);
        }

        return employeeRepository.FirstOrDefaultAsync(x => x.UserId == CurrentUser.Id.Value);
    }

    private async Task<HashSet<Guid>> GetCurrentAccessibleTeacherIdsAsync()
    {
        var teacherIds = new HashSet<Guid>();
        if (!CurrentUser.Id.HasValue)
        {
            return teacherIds;
        }

        var currentUserId = CurrentUser.Id.Value;
        var currentEmployee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == currentUserId);
        if (currentEmployee != null)
        {
            teacherIds.Add(currentEmployee.Id);
        }

        var reporteeEmployeeIds = await reportingManagerAccessService.GetReporteeEmployeeIdsAsync(currentUserId);
        foreach (var reporteeId in reporteeEmployeeIds)
        {
            teacherIds.Add(reporteeId);
        }

        return teacherIds;
    }

    private async Task<HashSet<Guid>> GetTeacherAndReporteeSubjectOwnedPlanIdsAsync(
        IQueryable<LessonPlan> unscopedQuery,
        HashSet<Guid> accessibleTeacherIds)
    {
        if (IsAdminUser() || accessibleTeacherIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var assignedSubjectQuery = await assignedSubjectRepository.WithDetailsAsync(
            assignment => assignment.Teachers);
        var assignedSubjects = await AsyncExecuter.ToListAsync(
            assignedSubjectQuery.Where(assignment =>
                assignment.Teachers.Any(teacher => accessibleTeacherIds.Contains(teacher.TeacherId))));
        var assignedSubjectKeys = assignedSubjects
            .Select(assignment => (assignment.StandardId, assignment.SubjectId))
            .ToHashSet();

        var allocations = await teacherSubjectAllocationRepository.GetListAsync(
            allocation => allocation.Teachers.Any(teacher => accessibleTeacherIds.Contains(teacher.TeacherId)));
        var allocatedSubjectKeys = allocations
            .Select(allocation => (allocation.StandardId, allocation.DivisionId, allocation.SubjectId))
            .ToHashSet();

        var teacherPlans = await AsyncExecuter.ToListAsync(
            unscopedQuery.Where(plan => accessibleTeacherIds.Contains(plan.TeacherId)));

        return teacherPlans
            .Where(plan =>
                assignedSubjectKeys.Contains((plan.StandardId, plan.SubjectId)) ||
                allocatedSubjectKeys.Contains((plan.StandardId, plan.DivisionId, plan.SubjectId)))
            .Select(plan => plan.Id)
            .ToHashSet();
    }

    private async Task EnsureOwnTeacherRecordAsync(Guid teacherId, string action)
    {
        if (IsAdminUser() || await HasClassTeacherAccessAsync())
        {
            return;
        }

        var accessibleTeacherIds = await GetCurrentAccessibleTeacherIdsAsync();
        if (accessibleTeacherIds.Contains(teacherId))
        {
            return;
        }

        var currentEmployee = await GetCurrentEmployeeAsync();
        if (currentEmployee == null)
        {
            throw new AbpAuthorizationException(
                "Your authenticated user is not linked to an employee record, so lesson records cannot be created or changed.");
        }

        throw new AbpAuthorizationException($"You can only {action} your own lesson records.");
    }

    private async Task EnsureCurrentTeacherOwnsLessonPlanAsync(LessonPlan lessonPlan, string action)
    {
        var currentUserId = CurrentUser.GetId();
        if (lessonPlan.TeacherId == currentUserId)
        {
            return;
        }

        var assignedTeacher = await employeeRepository.FirstOrDefaultAsync(
            employee => employee.Id == lessonPlan.TeacherId && employee.UserId == currentUserId
        );
        if (assignedTeacher != null)
        {
            return;
        }

        throw new AbpAuthorizationException(
            $"Only the assigned teacher can {action} this lesson plan."
        );
    }

    private bool IsAdminUser()
    {
        return CurrentUser.Roles.Any(role =>
            string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "administrator", StringComparison.OrdinalIgnoreCase)) ||
            CurrentUser.IsInRole("admin") ||
            CurrentUser.IsInRole("administrator");
    }

	private Task<bool> HasClassTeacherAccessAsync() =>
		CurrentUser.Id.HasValue
			? classTeacherAccessService.HasAssignmentAsync(CurrentUser.Id.Value)
			: Task.FromResult(false);

    private async Task<bool> IsReportingManagerOrAdminAsync(Guid planTeacherId)
    {
        if (IsAdminUser())
        {
            return true;
        }

        if (!CurrentUser.Id.HasValue)
        {
            return false;
        }

        var accessibleTeacherIds = await GetCurrentAccessibleTeacherIdsAsync();
        return accessibleTeacherIds.Contains(planTeacherId);
    }

    private async Task EnsureCanReviewLessonPlansAsync()
    {
        if (IsAdminUser())
        {
            return;
        }

        var hasReviewerRole = LessonPlanReviewerRoles.Any(role =>
            CurrentUser.Roles.Any(currentRole =>
                string.Equals(currentRole, role, StringComparison.OrdinalIgnoreCase) ||
                currentRole.Contains("coordinator", StringComparison.OrdinalIgnoreCase)) ||
            CurrentUser.IsInRole(role) ||
            (CurrentUser.Roles.Any(r => r.Contains("coordinator", StringComparison.OrdinalIgnoreCase))));

        if (hasReviewerRole)
        {
            return;
        }

        if (CurrentUser.Id.HasValue && await reportingManagerAccessService.HasReporteesAsync(CurrentUser.Id.Value))
        {
            return;
        }

        throw new AbpAuthorizationException(
            "Only Admin, Coordinator roles, or assigned Reporting Managers can approve, reject, or request changes for lesson plans.");
    }

    private async Task EnsureReviewerIsNotPlanOwnerAsync(LessonPlan lessonPlan)
    {
        var currentUserId = CurrentUser.GetId();
        if (lessonPlan.TeacherId == currentUserId)
        {
            throw new AbpAuthorizationException("You cannot review your own lesson plan.");
        }

        var teacher = await employeeRepository.FirstOrDefaultAsync(x => x.Id == lessonPlan.TeacherId);
        if (teacher?.UserId == currentUserId)
        {
            throw new AbpAuthorizationException("You cannot review your own lesson plan.");
        }
    }

    private static void EnsurePlanIsAwaitingReview(LessonPlan lessonPlan)
    {
        if (lessonPlan.Status != LessonPlanStatus.Submitted)
        {
            throw new UserFriendlyException("Only submitted lesson plans can be approved, rejected, or sent back for changes.");
        }
    }

    private static void EnsureLessonPlanEditable(LessonPlan lessonPlan)
    {
        if (lessonPlan.Status is LessonPlanStatus.Approved or LessonPlanStatus.Rejected)
        {
            throw new LessonPlanNotEditableException(lessonPlan.Status);
        }

        if (lessonPlan.Status is not (LessonPlanStatus.Draft or LessonPlanStatus.NeedsChanges))
        {
            throw new UserFriendlyException("Lesson plans can be edited only while draft or changes requested.");
        }
    }

    private static void EnsureLessonPlanSubmittable(LessonPlan lessonPlan)
    {
        if (lessonPlan.Status is LessonPlanStatus.Approved or LessonPlanStatus.Rejected)
        {
            throw new LessonPlanNotEditableException(lessonPlan.Status);
        }

        if (lessonPlan.Status == LessonPlanStatus.NeedsChanges)
        {
            throw new LessonPlanRevisionRequiredException();
        }

        if (lessonPlan.Status != LessonPlanStatus.Draft)
        {
            throw new UserFriendlyException("Only draft lesson plans can be submitted.");
        }
    }

    private static void EnsureReviewCommentPresent(ReviewRemarksDto input, string action)
    {
        if (string.IsNullOrWhiteSpace(input.Comment))
        {
            throw new UserFriendlyException($"Please add a reason to {action} this lesson plan.");
        }
    }

    private async Task EnsureLessonPlanCanBeCreatedForLocalDateAsync(
        Guid teacherId,
        DateTime localDate,
        Guid standardId,
        Guid divisionId)
    {
        var insight = await ResolveLessonPlanInsightAsync(teacherId, localDate.Date, standardId, divisionId);
        if (insight.BlocksPlanCreation)
        {
            throw new UserFriendlyException(insight.Message ?? "Lesson plan creation is blocked for this date.");
        }
    }

    private async Task EnsureTeacherDiaryAllowedAsync(LessonPlan plan, CreateUpdateTeacherDiaryDto input)
    {
        if (plan.Status != LessonPlanStatus.Approved)
        {
            throw new UserFriendlyException("Teacher diary can be submitted only against an approved lesson plan.");
        }

        if (input.LessonPlanId != plan.Id)
        {
            throw new UserFriendlyException("Teacher diary lesson plan does not match the selected plan.");
        }

        if (input.IsBackdated && string.IsNullOrWhiteSpace(input.BackdatedReason))
        {
            throw new UserFriendlyException("Backdated teacher diary requires a reason.");
        }

        if (!input.ClassConducted && string.IsNullOrWhiteSpace(input.MissedReason))
        {
            throw new UserFriendlyException("Missed reason is required when class was not conducted.");
        }

        if (input.ClassConducted && string.IsNullOrWhiteSpace(input.ActualTopicCovered))
        {
            throw new UserFriendlyException("Actual topic covered is required when class was conducted.");
        }

        // Equivalent offset and UTC payloads must be checked against the same school day.
        // Taking the UTC date directly turns midnight in India into the previous day.
        var actualLocalDate = LessonPlanCalendarDate.FromStoredInstant(input.ActualDate.UtcDateTime);
        var calendarInsight = await ResolveLessonPlanInsightAsync(plan.TeacherId, actualLocalDate, plan.StandardId, plan.DivisionId);
        if (calendarInsight.BlocksPlanCreation && !input.IsBackdated)
        {
            throw new UserFriendlyException(calendarInsight.Message ?? "Teacher diary cannot be submitted for this date.");
        }
    }

    private async Task EnsureUniqueLessonPlanAsync(
        Guid teacherId,
        DateTime plannedDate,
        string topic,
        Guid? timetableSlotId,
        Guid? excludedLessonPlanId = null)
    {
        var normalizedTopic = NormalizeLessonTopic(topic);
        var query = await lessonPlanRepository.GetQueryableAsync();
        var sameDayPlans = await AsyncExecuter.ToListAsync(
            query.Where(plan =>
                plan.TeacherId == teacherId &&
                plan.PlannedDate.Date == plannedDate.Date &&
                plan.Status != LessonPlanStatus.Rejected &&
                (!excludedLessonPlanId.HasValue || plan.Id != excludedLessonPlanId.Value))
        );

        var duplicateTopicExists = !string.IsNullOrWhiteSpace(normalizedTopic) &&
            sameDayPlans.Any(plan => NormalizeLessonTopic(plan.Topic) == normalizedTopic);

        if (duplicateTopicExists)
        {
            throw new UserFriendlyException(
                "A lesson plan with the same topic already exists for this teacher on this date.");
        }

        if (timetableSlotId.HasValue && timetableSlotId.Value != Guid.Empty)
        {
            var duplicateSlotExists = sameDayPlans.Any(plan => plan.TimetableSlotId == timetableSlotId.Value);
            if (duplicateSlotExists)
            {
                throw new UserFriendlyException(
                    "A lesson plan already exists for this teacher and timetable period on this date.");
            }
        }
    }

    private async Task EnsureChapterMatchesPlanTargetAsync(Guid chapterId, Guid standardId, Guid subjectId)
    {
        var chapter = await chapterRepository.FindAsync(chapterId)
            ?? throw new UserFriendlyException("Selected chapter is invalid.");

        var assignedSubject = await assignedSubjectRepository.FindAsync(chapter.AssignedSubjectId);
        if (assignedSubject == null ||
            assignedSubject.StandardId != standardId ||
            assignedSubject.SubjectId != subjectId)
        {
            throw new UserFriendlyException("Selected chapter does not belong to the selected class and subject.");
        }
    }

    private static string NormalizeLessonTopic(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return string.Empty;
        }

        return string.Join(' ', topic.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private async Task<Guid> ResolveTimelineTeacherIdAsync(Guid? teacherId)
    {
        if (teacherId.HasValue && teacherId.Value != Guid.Empty)
        {
            return teacherId.Value;
        }

        var currentUserId = CurrentUser.GetId();
        var employee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == currentUserId);
        return employee?.Id ?? currentUserId;
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

    private async Task<Dictionary<Guid, LessonPlanInsight>> LoadLessonPlanInsightsAsync(IReadOnlyCollection<LessonPlan> plans)
    {
        if (plans.Count == 0)
        {
            return new Dictionary<Guid, LessonPlanInsight>();
        }

        var dates = plans.ToDictionary(plan => plan.Id, plan => LessonPlanCalendarDate.FromStoredInstant(plan.PlannedDate).Date);
        var startDate = dates.Values.Min();
        var endDate = dates.Values.Max();
        var startDateOnly = DateOnly.FromDateTime(startDate);
        var endDateOnly = DateOnly.FromDateTime(endDate);
        var teacherIds = plans.Select(plan => plan.TeacherId).Distinct().ToList();
        var standardIds = plans.Select(plan => plan.StandardId).Distinct().ToList();
        var divisionIds = plans.Select(plan => plan.DivisionId).Distinct().ToList();
        var leaves = await employeeLeaveRepository.GetListAsync(leave =>
            teacherIds.Contains(leave.EmployeeId) && leave.Status == EmployeeLeaveStatusType.Approved &&
            leave.StartDateOnly <= endDateOnly && leave.EndDateOnly >= startDateOnly);
        var calendarDays = await calendarDayRepository.GetListAsync(day =>
            day.Date.Date >= startDate && day.Date.Date <= endDate && day.Status == CalendarDayStatus.Active &&
            (day.AppliesToAll ||
             (day.StandardId.HasValue && standardIds.Contains(day.StandardId.Value)) ||
             (day.DivisionId.HasValue && divisionIds.Contains(day.DivisionId.Value))));
        var leavesByTeacher = leaves.ToLookup(leave => leave.EmployeeId);
        var calendarDaysByDate = calendarDays.ToLookup(day => day.Date.Date);

        return plans.ToDictionary(plan => plan.Id, plan =>
        {
            var date = dates[plan.Id];
            var dateOnly = DateOnly.FromDateTime(date);
            var approvedLeave = leavesByTeacher[plan.TeacherId]
                .Where(leave => leave.StartDateOnly <= dateOnly && leave.EndDateOnly >= dateOnly)
                .OrderByDescending(leave => leave.HalfDay)
                .FirstOrDefault();
            if (approvedLeave != null)
            {
                return CreateLeaveInsight(approvedLeave);
            }

            var calendarDay = calendarDaysByDate[date]
                .Where(day => day.AppliesToAll ||
                    (day.StandardId == plan.StandardId && (!day.DivisionId.HasValue || day.DivisionId == plan.DivisionId)) ||
                    (day.DivisionId == plan.DivisionId && !day.StandardId.HasValue))
                .OrderByDescending(day => day.DayType)
                .FirstOrDefault(day => day.DayType != DayType.WorkingDay);
            return CreateCalendarInsight(calendarDay);
        });
    }

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
            return CreateLeaveInsight(approvedLeave);
        }

        var calendarDay = (await calendarDayRepository.GetListAsync(x =>
                x.Date.Date == date.Date &&
                x.Status == CalendarDayStatus.Active &&
                (x.AppliesToAll ||
                 (x.StandardId == standardId && (!x.DivisionId.HasValue || x.DivisionId == divisionId)) ||
                 (x.DivisionId == divisionId && !x.StandardId.HasValue))))
            .OrderByDescending(x => x.DayType)
            .FirstOrDefault(x => x.DayType != DayType.WorkingDay);

        return CreateCalendarInsight(calendarDay);
    }

    private static LessonPlanInsight CreateLeaveInsight(EmployeeLeave approvedLeave)
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

    private static LessonPlanInsight CreateCalendarInsight(AcademicCalendarDay? calendarDay)
    {
        if (calendarDay == null)
        {
            return LessonPlanInsight.None;
        }

        var type = calendarDay.DayType.ToString();
        var blocksPlanCreation = CalendarDayBlockingRules.BlocksLessonPlan(calendarDay.DayType);
        var message = calendarDay.DayType switch
        {
            DayType.Holiday => $"Holiday: {calendarDay.Title}. Lesson plan creation is blocked for this date.",
            DayType.Vacation => $"Vacation: {calendarDay.Title}. Lesson plan creation is blocked for this date.",
            DayType.HalfDay => $"Half day: {calendarDay.Title}. Check whether this period falls inside working hours before planning.",
            DayType.EmergencyClosure => $"Emergency closure: {calendarDay.Title}. Lesson plan creation is blocked for this date.",
            _ => $"{type}: {calendarDay.Title}. Review this calendar insight before creating the lesson plan."
        };

        return new LessonPlanInsight(type, blocksPlanCreation ? "error" : "warning", message, blocksPlanCreation);
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
