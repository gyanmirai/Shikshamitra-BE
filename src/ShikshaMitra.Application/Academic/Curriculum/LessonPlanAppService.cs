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
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.Authorization;
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
    IRepository<AssignedSubject, Guid> assignedSubjectRepository,
    IRepository<AssignedSubjectChapter, Guid> chapterRepository,
    IRepository<Employee, Guid> employeeRepository,
    IRepository<AcademicCalendarDay, Guid> calendarDayRepository,
    IRepository<EmployeeLeave, Guid> employeeLeaveRepository,
    IRepository<EducationMediumStage, Guid> mediumStageRepository,
    IHomeworkAppService homeworkAppService
) : ApplicationService, ILessonPlanAppService
{
    private static readonly string[] LessonPlanReviewerRoles =
    {
        "admin",
        "administrator",
        "coordinator",
        "academic coordinator"
    };

    public async Task<PagedResultDto<LessonPlanDto>> GetListAsync(GetLessonPlansInput input)
    {
        var query = await lessonPlanRepository.GetQueryableAsync();
        query = await ApplyLessonPlanFiltersAsync(query, input);

        if (!IsAdminUser())
        {
            var currentUserId = CurrentUser.GetId();
            var currentEmployee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == currentUserId);
            if (currentEmployee == null)
            {
                return new PagedResultDto<LessonPlanDto>(0, new List<LessonPlanDto>());
            }

            var managedEmployeeIds = await GetReportingEmployeesAsync(currentEmployee.Id);
            query = query.Where(x => x.TeacherId == currentEmployee.Id || managedEmployeeIds.Contains(x.TeacherId));
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

    public async Task<LessonPlanTimelineDto> GetTimelineAsync(DateTimeOffset date, Guid? teacherId = null, Guid? academicMediumId = null)
    {
        var targetTeacherId = await ResolveTimelineTeacherIdAsync(teacherId);

        if (!IsAdminUser())
        {
            var currentUserId = CurrentUser.GetId();
            var currentEmployee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == currentUserId);
            if (currentEmployee == null)
            {
                throw new AbpAuthorizationException("You are not authorized to view this timeline.");
            }

            if (targetTeacherId != currentEmployee.Id)
            {
                var managedEmployeeIds = await GetReportingEmployeesAsync(currentEmployee.Id);
                if (!managedEmployeeIds.Contains(targetTeacherId))
                {
                    throw new AbpAuthorizationException("You are not authorized to view this timeline.");
                }
            }
        }
        var lessonPlansQuery = await lessonPlanRepository.GetQueryableAsync();
        lessonPlansQuery = lessonPlansQuery.Where(x => x.TeacherId == targetTeacherId && x.PlannedDate.Date == date.DateTime.Date);

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

        var lessonPlans = await AsyncExecuter.ToListAsync(lessonPlansQuery);
        var mappedPlans = await MapLessonPlansAsync(lessonPlans);
        var mappedById = mappedPlans.ToDictionary(x => x.Id);
        var matchedPlanIds = new HashSet<Guid>();
        var slots = new List<TimelineSlotDto>();
        var timetableDay = ToTimetableDay(date.DateTime.DayOfWeek);

        if (timetableDay != null)
        {
            var timetableQuery = await timetableRepository.WithDetailsAsync();
            var timetablesQuery = timetableQuery.Where(x => x.Status == TimetableStatus.Published);
            if (allowedStandardIds != null)
            {
                timetablesQuery = timetablesQuery.Where(x => allowedStandardIds.Contains(x.StandardId));
            }
            var timetables = await AsyncExecuter.ToListAsync(timetablesQuery);

            var standards = await standardRepository.GetListAsync();
            var divisions = await divisionRepository.GetListAsync();
            var subjects = await subjectRepository.GetListAsync();

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
                        var slotStartTime = slot.StartTime.ToString("hh:mm tt");
                        var slotEndTime = slot.EndTime.ToString("hh:mm tt");
                        var plan = lessonPlans.FirstOrDefault(lp =>
                            !matchedPlanIds.Contains(lp.Id) &&
                            lp.TimetableSlotId == slot.Id &&
                            MatchesTimelineContext(lp, targetTeacherId, timetable.StandardId, timetable.DivisionId, slotSubject.SubjectId));

                        // Timetable slots used to be recreated when a timetable was saved. If a
                        // lesson plan still points at the previous slot id, recover it only when
                        // there is one unambiguous plan for this teacher/date/context.
                        if (plan == null)
                        {
                            var contextualPlans = lessonPlans
                                .Where(lp =>
                                    !matchedPlanIds.Contains(lp.Id) &&
                                    lp.StandardId == timetable.StandardId &&
                                    lp.DivisionId == timetable.DivisionId &&
                                    lp.SubjectId == slotSubject.SubjectId &&
                                    lp.TimetableSlotId != slot.Id)
                                .ToList();

                            // A recreated timetable can have more than one occurrence of the
                            // same subject for a class/division. Match the old slot's time first
                            // so each plan is attached to its original period rather than being
                            // left unmapped or assigned to an arbitrary slot.
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
                            StartTime = slotStartTime,
                            EndTime = slotEndTime,
                            StandardId = timetable.StandardId,
                            StandardName = standard?.Name ?? "Class",
                            DivisionId = timetable.DivisionId,
                            DivisionName = division?.Name ?? "Division",
                            SubjectId = slotSubject.SubjectId,
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
            Date = date.DateTime,
            TeacherId = targetTeacherId,
            Slots = slots
                .OrderBy(s => string.IsNullOrWhiteSpace(s.StartTime))
                .ThenBy(s => s.StartTime)
                .ThenBy(s => s.PeriodName)
                .ToList()
        };
    }

    public async Task<LessonPlanDto> GetAsync(Guid id)
    {
        var plan = await lessonPlanRepository.GetAsync(id);

        if (!IsAdminUser())
        {
            var currentUserId = CurrentUser.GetId();
            var currentEmployee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == currentUserId);
            if (currentEmployee == null)
            {
                throw new AbpAuthorizationException("You are not authorized to view this lesson plan.");
            }

            if (plan.TeacherId != currentEmployee.Id)
            {
                var managedEmployeeIds = await GetReportingEmployeesAsync(currentEmployee.Id);
                if (!managedEmployeeIds.Contains(plan.TeacherId))
                {
                    throw new AbpAuthorizationException("You are not authorized to view this lesson plan.");
                }
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

    public async Task<LessonPlanDto> CreateAsync(CreateLessonPlanDto input)
    {
        EnsureOwnTeacherRecord(input.TeacherId, "create");
        await EnsureChapterMatchesPlanTargetAsync(input.ChapterId, input.StandardId, input.SubjectId);

        await EnsureLessonPlanCanBeCreatedForDateAsync(input.TeacherId, input.PlannedDate, input.StandardId, input.DivisionId);
        await EnsureUniqueLessonPlanAsync(input.TeacherId, input.PlannedDate, input.Topic, input.TimetableSlotId);

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

        if (input.Status == LessonPlanStatus.Submitted)
        {
            lessonPlan.Submit();
        }

        var inserted = await lessonPlanRepository.InsertAsync(lessonPlan, autoSave: true);
        return (await MapLessonPlansAsync(new[] { inserted })).Single();
    }

    public async Task<LessonPlanDto> UpdateAsync(Guid id, UpdateLessonPlanDto input)
    {
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        EnsureOwnTeacherRecord(lessonPlan.TeacherId, "modify");
        EnsureLessonPlanEditable(lessonPlan);
        await EnsureChapterMatchesPlanTargetAsync(input.ChapterId, lessonPlan.StandardId, lessonPlan.SubjectId);

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

    public async Task<LessonPlanDto> SubmitAsync(Guid id)
    {
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        EnsureOwnTeacherRecord(lessonPlan.TeacherId, "submit");
        EnsureLessonPlanSubmittable(lessonPlan);
        await EnsureChapterMatchesPlanTargetAsync(lessonPlan.ChapterId, lessonPlan.StandardId, lessonPlan.SubjectId);
        await EnsureLessonPlanCanBeCreatedForDateAsync(lessonPlan.TeacherId, lessonPlan.PlannedDate, lessonPlan.StandardId, lessonPlan.DivisionId);
        await EnsureUniqueLessonPlanAsync(lessonPlan.TeacherId, lessonPlan.PlannedDate, lessonPlan.Topic, lessonPlan.TimetableSlotId, lessonPlan.Id);

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
            EnsureLessonPlanSubmittable(plan);
            await EnsureChapterMatchesPlanTargetAsync(plan.ChapterId, plan.StandardId, plan.SubjectId);
            await EnsureLessonPlanCanBeCreatedForDateAsync(plan.TeacherId, plan.PlannedDate, plan.StandardId, plan.DivisionId);
            await EnsureUniqueLessonPlanAsync(plan.TeacherId, plan.PlannedDate, plan.Topic, plan.TimetableSlotId, plan.Id);
            plan.Submit();
            await lessonPlanRepository.UpdateAsync(plan);
        }
    }

    public async Task<LessonPlanDto> ApproveAsync(Guid id, ReviewRemarksDto input)
    {
        await EnsureCanReviewLessonPlansAsync();
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        await EnsureReviewerIsNotPlanOwnerAsync(lessonPlan);
        EnsurePlanIsAwaitingReview(lessonPlan);

        if (!await IsReportingManagerOrAdminAsync(lessonPlan.TeacherId))
        {
            throw new AbpAuthorizationException("You can only review lesson plans of employees in your reporting hierarchy.");
        }

        lessonPlan.Approve(CurrentUser.GetId(), input.Comment);
        await lessonPlanRepository.UpdateAsync(lessonPlan);
        return await GetAsync(id);
    }

    public async Task<LessonPlanDto> RejectAsync(Guid id, ReviewRemarksDto input)
    {
        await EnsureCanReviewLessonPlansAsync();
        EnsureReviewCommentPresent(input, "reject");
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        await EnsureReviewerIsNotPlanOwnerAsync(lessonPlan);
        EnsurePlanIsAwaitingReview(lessonPlan);

        if (!await IsReportingManagerOrAdminAsync(lessonPlan.TeacherId))
        {
            throw new AbpAuthorizationException("You can only review lesson plans of employees in your reporting hierarchy.");
        }

        lessonPlan.Reject(CurrentUser.GetId(), input.Comment);
        await lessonPlanRepository.UpdateAsync(lessonPlan);
        return await GetAsync(id);
    }

    public async Task<LessonPlanDto> RequestChangesAsync(Guid id, ReviewRemarksDto input)
    {
        await EnsureCanReviewLessonPlansAsync();
        EnsureReviewCommentPresent(input, "request changes for");
        var lessonPlan = await lessonPlanRepository.GetAsync(id);
        await EnsureReviewerIsNotPlanOwnerAsync(lessonPlan);
        EnsurePlanIsAwaitingReview(lessonPlan);

        if (!await IsReportingManagerOrAdminAsync(lessonPlan.TeacherId))
        {
            throw new AbpAuthorizationException("You can only review lesson plans of employees in your reporting hierarchy.");
        }

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

        if (!IsAdminUser())
        {
            var currentUserId = CurrentUser.GetId();
            var currentEmployee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == currentUserId);
            if (currentEmployee == null)
            {
                return new PagedResultDto<TeacherDiaryDto>(0, new List<TeacherDiaryDto>());
            }

            var managedEmployeeIds = await GetReportingEmployeesAsync(currentEmployee.Id);
            query = query.Where(x => x.diary.TeacherId == currentEmployee.Id || managedEmployeeIds.Contains(x.diary.TeacherId));
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

    public async Task<TeacherDiaryDto> GetTeacherDiaryAsync(Guid id)
    {
        var diary = await teacherDiaryRepository.GetAsync(id);

        if (!IsAdminUser())
        {
            var currentUserId = CurrentUser.GetId();
            var currentEmployee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == currentUserId);
            if (currentEmployee == null)
            {
                throw new AbpAuthorizationException("You are not authorized to view this diary entry.");
            }

            if (diary.TeacherId != currentEmployee.Id)
            {
                var managedEmployeeIds = await GetReportingEmployeesAsync(currentEmployee.Id);
                if (!managedEmployeeIds.Contains(diary.TeacherId))
                {
                    throw new AbpAuthorizationException("You are not authorized to view this diary entry.");
                }
            }
        }

        return (await MapTeacherDiariesAsync(new[] { diary })).Single();
    }

    public async Task<TeacherDiaryDto> CreateTeacherDiaryAsync(CreateUpdateTeacherDiaryDto input)
    {
        var plan = await lessonPlanRepository.GetAsync(input.LessonPlanId);
        EnsureOwnTeacherRecord(plan.TeacherId, "create a diary for");
        await EnsureTeacherDiaryAllowedAsync(plan, input);

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

        if (input.LessonPlanId != diary.LessonPlanId)
        {
            throw new UserFriendlyException("Teacher diary cannot be moved to a different lesson plan.");
        }

        var plan = await lessonPlanRepository.GetAsync(input.LessonPlanId);
        await EnsureTeacherDiaryAllowedAsync(plan, input);
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
        var reviewerIds = list
            .Where(x => x.ReviewedBy.HasValue)
            .Select(x => x.ReviewedBy!.Value)
            .Distinct()
            .ToList();
        var reviewers = reviewerIds.Count > 0
            ? await employeeRepository.GetListAsync(x => x.UserId.HasValue && reviewerIds.Contains(x.UserId.Value))
            : new List<Employee>();
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
            var reviewer = item.ReviewedBy.HasValue
                ? reviewers.FirstOrDefault(x => x.UserId == item.ReviewedBy.Value)
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
            item.ReviewedByName = reviewer?.EmployeeName;
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

    private bool IsAdminUser()
    {
        return CurrentUser.Roles.Any(role =>
            string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "administrator", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<Guid>> GetReportingEmployeesAsync(Guid managerEmployeeId)
    {
        var allEmployees = await employeeRepository.GetListAsync();
        var result = new List<Guid>();
        var visited = new HashSet<Guid>();
        
        void Traverse(Guid parentId)
        {
            if (!visited.Add(parentId))
            {
                return;
            }
            var children = allEmployees.Where(x => x.ReportingManagerId == parentId).ToList();
            foreach (var child in children)
            {
                if (!result.Contains(child.Id))
                {
                    result.Add(child.Id);
                    Traverse(child.Id);
                }
            }
        }
        
        Traverse(managerEmployeeId);
        return result;
    }

    private async Task<bool> IsReportingManagerOrAdminAsync(Guid planTeacherId)
    {
        if (IsAdminUser())
        {
            return true;
        }

        var currentUserId = CurrentUser.GetId();
        var currentEmployee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == currentUserId);
        if (currentEmployee == null)
        {
            return false;
        }

        var targetEmployee = await employeeRepository.FirstOrDefaultAsync(x => x.Id == planTeacherId);
        if (targetEmployee == null)
        {
            return false;
        }

        var visited = new HashSet<Guid>();
        var nextManagerId = targetEmployee.ReportingManagerId;

        while (nextManagerId.HasValue && nextManagerId.Value != Guid.Empty)
        {
            if (nextManagerId.Value == currentEmployee.Id)
            {
                return true;
            }

            if (!visited.Add(nextManagerId.Value))
            {
                break;
            }

            var manager = await employeeRepository.FirstOrDefaultAsync(x => x.Id == nextManagerId.Value);
            nextManagerId = manager?.ReportingManagerId;
        }

        return false;
    }

    private async Task EnsureCanReviewLessonPlansAsync()
    {
        var hasReviewerRole = LessonPlanReviewerRoles.Any(role =>
            CurrentUser.Roles.Any(currentRole =>
                string.Equals(currentRole, role, StringComparison.OrdinalIgnoreCase) ||
                currentRole.Contains("coordinator", StringComparison.OrdinalIgnoreCase)));

        if (hasReviewerRole)
        {
            return;
        }

        var currentUserId = CurrentUser.GetId();
        var currentEmployee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == currentUserId);
        if (currentEmployee != null)
        {
            var subordinates = await GetReportingEmployeesAsync(currentEmployee.Id);
            if (subordinates.Count > 0)
            {
                return;
            }
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
        if (lessonPlan.Status is not (LessonPlanStatus.Draft or LessonPlanStatus.Rejected or LessonPlanStatus.NeedsChanges))
        {
            throw new UserFriendlyException("Lesson plans can be edited only while draft, rejected, or changes requested.");
        }
    }

    private static void EnsureLessonPlanSubmittable(LessonPlan lessonPlan)
    {
        if (lessonPlan.Status is not (LessonPlanStatus.Draft or LessonPlanStatus.Rejected or LessonPlanStatus.NeedsChanges))
        {
            throw new UserFriendlyException("Only draft, rejected, or changes-requested lesson plans can be submitted.");
        }
    }

    private static void EnsureReviewCommentPresent(ReviewRemarksDto input, string action)
    {
        if (string.IsNullOrWhiteSpace(input.Comment))
        {
            throw new UserFriendlyException($"Please add a reason to {action} this lesson plan.");
        }
    }

    private async Task EnsureLessonPlanCanBeCreatedForDateAsync(
        Guid teacherId,
        DateTime plannedDate,
        Guid standardId,
        Guid divisionId)
    {
        var insight = await ResolveLessonPlanInsightAsync(teacherId, plannedDate.Date, standardId, divisionId);
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

        var calendarInsight = await ResolveLessonPlanInsightAsync(plan.TeacherId, input.ActualDate.Date, plan.StandardId, plan.DivisionId);
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
