using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.HomeworkAssignment;

[Authorize]
[RemoteService(IsEnabled = false)]
public class HomeworkAppService(
    IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository,
    IRepository<HomeworkDivisionSchedule, Guid> scheduleRepository,
    IDivisionRepository divisionRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<AssignedSubject, Guid> assignedSubjectRepository,
    IRepository<TeacherSubjectAllocation, Guid> teacherSubjectAllocationRepository,
    IRepository<AssignedSubjectChapter, Guid> chapterRepository,
    IHomeworkTypeRepository homeworkTypeRepository,
    IRepository<IdentityUser, Guid> userRepository,
    IUserScopeAccessService userScopeAccessService,
    HomeworkManager homeworkManager,
    IAsyncQueryableExecuter asyncExecuter,
    IRepository<StudentSubmissionEntity, Guid> studentSubmissionRepository,
    IRepository<AcademicCalendarDay, Guid> calendarDayRepository,
    IRepository<Employee, Guid> employeeRepository,
    IReportingManagerAccessService reportingManagerAccessService
) : ApplicationService, IHomeworkAppService
{


    // ─────────────────────────────────────────────────────────────────────
    // CREATE
    // ─────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add)]
    public async Task<HomeworkCreateResponseDto> CreateAsync(
        Guid academicMediumId,
        CreateHomeworkDto input,
        string? attachmentBlobName
    )
    {
        if (academicMediumId == Guid.Empty)
            throw new BusinessException("ShikshaMitra.Homework:InvalidAcademicMedium");

        await EnsureCurrentUserCanAccessTargetAsync(
            academicMediumId,
            input.StandardId,
            input.SubjectId,
            input.DivisionSchedules.Select(schedule => schedule.DivisionId));

        await homeworkManager.ValidateHomeworkTypeAsync(input.HomeworkTypeId);
        await ValidateHomeworkScheduleDatesAsync(input.StandardId, input.DivisionSchedules);

        var divisionData = input.DivisionSchedules
            .Select(d => (d.DivisionId, d.PublishAt, d.DueAt))
            .ToList();

        var homework = new HomeworkAssignmentEntity(
            GuidGenerator.Create(),
            input.StandardId,
            input.SubjectId,
            input.Title,
            input.Description,
            input.HomeworkTypeId,
            (SubmissionType) input.SubmissionType,
            CurrentTenant.Id,
            academicYearId: null,
            chapterIdsJson: null,
            attachmentsJson: null,
            educationMediumId: academicMediumId
        );

        // Store chapter IDs in JSON and single chapterId for backward compatibility
        if (input.ChapterIds != null && input.ChapterIds.Any())
        {
            homework.ChapterIdsJson = System.Text.Json.JsonSerializer.Serialize(input.ChapterIds);
            homework.ChapterId = input.ChapterIds.First();
        }
        else
        {
            homework.ChapterId = input.ChapterId;
            if (input.ChapterId.HasValue && input.ChapterId.Value != Guid.Empty)
            {
                homework.ChapterIdsJson = System.Text.Json.JsonSerializer.Serialize(new List<Guid> { input.ChapterId.Value });
            }
        }
        homework.DocumentUrl = attachmentBlobName;

        foreach (var div in input.DivisionSchedules)
        {
            homework.AddDivisionSchedule(new HomeworkDivisionSchedule(
                GuidGenerator.Create(),
                homework.Id,
                div.DivisionId,
                div.PublishAt,
                div.DueAt,
                CurrentTenant.Id
            ));
        }

        await homeworkRepository.InsertAsync(homework, autoSave: true);

        var dto = await MapToHomeworkDtoAsync(homework, academicMediumId);
        return new HomeworkCreateResponseDto
        {
            Message = L["ShikshaMitra.Homework:CreatedSuccessfully"].Value,
            Data = dto,
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET LIST (paginated, filtered by division + publish date)
    // ─────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View)]
    public async Task<HomeworkListResponseDto> GetListAsync(
        Guid academicMediumId,
        GetHomeworkListFilterDto filter
    )
    {
        var query = await homeworkRepository.WithDetailsAsync(
            h => h.DivisionSchedules,
            h => h.HomeworkType
        );

        query = query
            .Where(h => h.EducationMediumId == academicMediumId)
            .Where(h => h.StandardId == filter.StandardId)
            .Where(h => h.SubjectId == filter.SubjectId);
        query = await ApplyCurrentUserScopeAsync(query);

        if (filter.HomeworkDate.HasValue)
        {
            var homeworkDate = filter.HomeworkDate.Value.Date;
            query = query.Where(h => h.DivisionSchedules.Any(s =>
                s.DivisionId == filter.DivisionId
                && (
                    h.CreationTime.Date == homeworkDate
                    || (s.PublishDate.HasValue && s.PublishDate.Value.Date == homeworkDate)
                    || s.DueDate.Date == homeworkDate
                )));
        }
        else
        {
            query = query.Where(h => h.DivisionSchedules.Any(s =>
                s.DivisionId == filter.DivisionId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(h =>
                h.Title.ToLower().Contains(search) ||
                h.Description.ToLower().Contains(search));
        }

        query = query.OrderByDescending(h => h.CreationTime);

        var totalCount = await asyncExecuter.CountAsync(query);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = Math.Clamp(filter.PageSize, 10, 100);
        var totalPages = (int) Math.Ceiling((double) totalCount / pageSize);

        var items = await asyncExecuter.ToListAsync(
            query.Skip((page - 1) * pageSize).Take(pageSize)
        );

        // Batch-load related entities to avoid N+1
        var standardIds = items.Select(h => h.StandardId).Distinct().ToList();
        var subjectIds = items.Select(h => h.SubjectId).Distinct().ToList();
        var chapterIds = new List<Guid>();
        foreach (var h in items)
        {
            if (!string.IsNullOrWhiteSpace(h.ChapterIdsJson))
            {
                try
                {
                    var ids = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(h.ChapterIdsJson);
                    if (ids != null) chapterIds.AddRange(ids);
                }
                catch { /* ignore invalid json */ }
            }
            else if (h.ChapterId.HasValue)
            {
                chapterIds.Add(h.ChapterId.Value);
            }
        }
        chapterIds = chapterIds.Distinct().ToList();
        var homeworkTypeIds = items.Where(h => h.HomeworkTypeId.HasValue)
            .Select(h => h.HomeworkTypeId!.Value).Distinct().ToList();
        var divisionIds = items.SelectMany(h => h.DivisionSchedules.Select(s => s.DivisionId))
            .Distinct().ToList();
        var creatorIds = items.Where(h => h.CreatorId.HasValue)
            .Select(h => h.CreatorId!.Value).Distinct().ToList();
        var updaterIds = items.Where(h => h.LastModifierId.HasValue)
            .Select(h => h.LastModifierId!.Value).Distinct().ToList();

        var allUserIds = creatorIds.Union(updaterIds).Distinct().ToList();

        var standards = await standardRepository.GetListAsync(s => standardIds.Contains(s.Id));
        // Build subjectLookup keyed by h.SubjectId (may be Subject.Id or AssignedSubject.Id)
        var directSubjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
        var subjectLookupByHomeworkId = directSubjects.ToDictionary(s => s.Id, s => s.Name);
        var missingSubjectIds = subjectIds.Where(id => !subjectLookupByHomeworkId.ContainsKey(id)).ToList();
        if (missingSubjectIds.Count > 0)
        {
            // Fallback: some SubjectIds might actually be AssignedSubject.Id values
            var fallbackAssigned = await assignedSubjectRepository.GetListAsync(a => missingSubjectIds.Contains(a.Id));
            if (fallbackAssigned.Count > 0)
            {
                var resolvedSubjectIds = fallbackAssigned.Select(a => a.SubjectId).Distinct().ToList();
                var resolvedSubjects = await subjectRepository.GetListAsync(s => resolvedSubjectIds.Contains(s.Id));
                var resolvedBySubjectId = resolvedSubjects.ToDictionary(s => s.Id, s => s.Name);
                foreach (var a in fallbackAssigned)
                    if (resolvedBySubjectId.TryGetValue(a.SubjectId, out var resolvedName))
                        subjectLookupByHomeworkId[a.Id] = resolvedName; // key = AssignedSubject.Id
            }
        }
        var chapters = chapterIds.Any()
            ? await chapterRepository.GetListAsync(c => chapterIds.Contains(c.Id))
            : new List<AssignedSubjectChapter>();
        var hwTypes = homeworkTypeIds.Any()
            ? await homeworkTypeRepository.GetListAsync(t => homeworkTypeIds.Contains(t.Id))
            : new List<HomeworkType>();
        var divisions = await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id));
        var users = allUserIds.Any()
            ? await userRepository.GetListAsync(u => allUserIds.Contains(u.Id))
            : new List<IdentityUser>();
        var homeworkIds = items.Select(h => h.Id).ToList();
        var submissions = homeworkIds.Any()
            ? await studentSubmissionRepository.GetListAsync(s =>
                s.HomeworkAssignmentId.HasValue && homeworkIds.Contains(s.HomeworkAssignmentId.Value))
            : new List<StudentSubmissionEntity>();
        var submissionLookup = submissions
            .GroupBy(s => s.HomeworkAssignmentId!.Value)
            .ToDictionary(g => g.Key, g => BuildSubmissionSummary(g));

        var standardLookup = standards.ToDictionary(s => s.Id, s => s.Name);
        var subjectLookup = subjectLookupByHomeworkId;
        var chapterLookup = chapters.ToDictionary(c => c.Id, c => c.ChapterName);
        var hwTypeLookup = hwTypes.ToDictionary(t => t.Id);
        var divisionLookup = divisions.ToDictionary(d => d.Id, d => d.Name);
        var userLookup = users.ToDictionary(u => u.Id, u => $"{u.Name} {u.Surname}".Trim());

        var dtos = items
            .Select(h => MapToHomeworkDto(h, academicMediumId, standardLookup, subjectLookup,
                chapterLookup, hwTypeLookup, divisionLookup, userLookup,
                submissionLookup.GetValueOrDefault(h.Id)))
            .ToList();

        return new HomeworkListResponseDto
        {
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            Items = dtos,
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET (single)
    // ─────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View)]
    public async Task<HomeworkResponseDto> GetAsync(Guid homeworkId, Guid? academicMediumId = null)
    {
        var q = await homeworkRepository.WithDetailsAsync(
            h => h.DivisionSchedules,
            h => h.HomeworkType
        );

        var homework = await asyncExecuter.FirstOrDefaultAsync(
            q.Where(h => h.Id == homeworkId)
        ) ?? throw new BusinessException("ShikshaMitra.Homework:NotFound").WithData("Id", homeworkId);

        if (academicMediumId.HasValue && homework.EducationMediumId != academicMediumId.Value)
            throw new BusinessException("ShikshaMitra.Homework:NotFound").WithData("Id", homeworkId);

        await EnsureCurrentUserCanAccessHomeworkAsync(homework, nameof(GetAsync));

        var dto = await MapToHomeworkDtoAsync(homework, homework.EducationMediumId ?? Guid.Empty);
        return new HomeworkResponseDto { Data = dto };
    }

    // ─────────────────────────────────────────────────────────────────────
    // UPDATE
    // ─────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add)]
    public async Task<HomeworkResponseDto> UpdateAsync(
        Guid homeworkId,
        UpdateHomeworkDto input,
        string? attachmentBlobName
    )
    {
        var q = await homeworkRepository.WithDetailsAsync(h => h.DivisionSchedules);
        var homework = await asyncExecuter.FirstOrDefaultAsync(q.Where(h => h.Id == homeworkId))
            ?? throw new BusinessException("ShikshaMitra.Homework:NotFound").WithData("Id", homeworkId);

        await EnsureCurrentUserCanAccessHomeworkAsync(homework, nameof(UpdateAsync));
        await EnsureCurrentUserCanAccessTargetAsync(
            homework.EducationMediumId ?? Guid.Empty,
            input.StandardId,
            input.SubjectId,
            input.DivisionSchedules.Select(schedule => schedule.DivisionId));

        if (homework.Status != HomeworkStatus.Draft)
            throw new BusinessException("ShikshaMitra.Homework:CannotUpdatePublished")
                .WithData("HomeworkId", homeworkId);

        await homeworkManager.ValidateHomeworkTypeAsync(input.HomeworkTypeId);
        await ValidateHomeworkScheduleDatesAsync(input.StandardId, input.DivisionSchedules);

        // Keep existing blob if no new one provided
        var resolvedBlob = attachmentBlobName ?? homework.DocumentUrl;

        string? chapterIdsJson = null;
        Guid? firstChapterId = null;

        if (input.ChapterIds != null && input.ChapterIds.Any())
        {
            chapterIdsJson = System.Text.Json.JsonSerializer.Serialize(input.ChapterIds);
            firstChapterId = input.ChapterIds.First();
        }
        else
        {
            firstChapterId = input.ChapterId;
            if (input.ChapterId.HasValue && input.ChapterId.Value != Guid.Empty)
            {
                chapterIdsJson = System.Text.Json.JsonSerializer.Serialize(new List<Guid> { input.ChapterId.Value });
            }
        }

        homework.UpdateBasicInfo(input.Title, input.Description, chapterIdsJson, homework.AttachmentsJson);
        homework.ChapterId = firstChapterId;
        homework.SubjectId = input.SubjectId;
        homework.StandardId = input.StandardId;
        homework.HomeworkTypeId = input.HomeworkTypeId;
        homework.SubmissionType = (SubmissionType) input.SubmissionType;
        homework.DocumentUrl = resolvedBlob;

        // Replace division schedules
        var existing = homework.DivisionSchedules.ToList();
        await scheduleRepository.DeleteManyAsync(existing);
        homework.ClearDivisionSchedules();

        foreach (var div in input.DivisionSchedules)
        {
            homework.AddDivisionSchedule(new HomeworkDivisionSchedule(
                GuidGenerator.Create(),
                homework.Id,
                div.DivisionId,
                div.PublishAt,
                div.DueAt,
                CurrentTenant.Id
            ));
        }

        await homeworkRepository.UpdateAsync(homework, autoSave: true);

        var dto = await MapToHomeworkDtoAsync(homework, homework.EducationMediumId ?? Guid.Empty);
        return new HomeworkResponseDto { Data = dto };
    }

    // ─────────────────────────────────────────────────────────────────────
    // DELETE
    // ─────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Delete)]
    public async Task DeleteAsync(Guid homeworkId)
    {
        var q = await homeworkRepository.WithDetailsAsync(h => h.DivisionSchedules);
        var homework = await asyncExecuter.FirstOrDefaultAsync(q.Where(h => h.Id == homeworkId))
            ?? throw new BusinessException("ShikshaMitra.Homework:NotFound").WithData("Id", homeworkId);

        await EnsureCurrentUserCanAccessHomeworkAsync(homework, nameof(DeleteAsync));

        if (homework.Status != HomeworkStatus.Draft || homework.IsPublished || homework.HasPublishedDivisions)
        {
            throw new BusinessException("ShikshaMitra.Homework:CannotDeletePublished")
                .WithData("HomeworkId", homeworkId)
                .WithData("Status", homework.Status);
        }

        var submissionsCount = await studentSubmissionRepository.CountAsync(s => s.HomeworkAssignmentId == homeworkId);
        if (submissionsCount > 0)
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.HomeworkIsInUse)
                .WithData("message", $"Cannot delete homework '{homework.Title}' because it is in use: {submissionsCount} student submission(s). Please remove these submissions first.");
        }

        await homeworkRepository.DeleteAsync(homework, autoSave: true);
    }

    // ─────────────────────────────────────────────────────────────────────
    // CHANGE STATUS
    // ─────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add)]
    public async Task<HomeworkStatusUpdateResponseDto> ChangeStatusAsync(
        Guid homeworkId,
        HomeworkStatusUpdateDto input
    )
    {
        var q = await homeworkRepository.WithDetailsAsync(h => h.DivisionSchedules);
        var homework = await asyncExecuter.FirstOrDefaultAsync(q.Where(h => h.Id == homeworkId))
            ?? throw new BusinessException("ShikshaMitra.Homework:NotFound").WithData("Id", homeworkId);

        await EnsureCurrentUserCanAccessHomeworkAsync(homework, nameof(ChangeStatusAsync));

        var previousStatus = homework.Status;
        var newStatus = (HomeworkStatus)input.Status;
        if (newStatus == HomeworkStatus.Published)
        {
            await ValidateExistingHomeworkScheduleDatesAsync(homework);
        }
        else if (newStatus == HomeworkStatus.Closed)
        {
            await EnsureHomeworkCanCloseAsync(homework.Id);
        }

        homework.ChangeStatus(newStatus);

        if (newStatus == HomeworkStatus.Published)
        {
            homework.PublishAll();
        }

        await homeworkRepository.UpdateAsync(homework, autoSave: true);

        var updaterName = string.Empty;
        if (CurrentUser.Id.HasValue)
        {
            var user = await userRepository.FindAsync(CurrentUser.Id.Value);
            updaterName = user != null ? $"{user.Name} {user.Surname}".Trim() : string.Empty;
        }

        return new HomeworkStatusUpdateResponseDto
        {
            Id = homework.Id,
            PreviousStatus = (int) previousStatus,
            CurrentStatus = (int) homework.Status,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = updaterName,
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View)]
    public async Task<LearningTaskDashboardDto> GetDashboardAsync(
        Guid academicMediumId,
        LearningTaskDashboardFilterDto input
    )
    {
        if (academicMediumId == Guid.Empty)
            throw new BusinessException("ShikshaMitra.Homework:InvalidAcademicMedium");

        var asOfDate = (input.AsOfDate ?? DateTime.UtcNow).Date;
        var query = await homeworkRepository.WithDetailsAsync(h => h.DivisionSchedules);
        query = query.Where(h => h.EducationMediumId == academicMediumId);
        query = await ApplyCurrentUserScopeAsync(query);

        if (input.StandardId.HasValue) query = query.Where(h => h.StandardId == input.StandardId.Value);
        if (input.SubjectId.HasValue) query = query.Where(h => h.SubjectId == input.SubjectId.Value);
        if (input.DivisionId.HasValue) query = query.Where(h => h.DivisionSchedules.Any(s => s.DivisionId == input.DivisionId.Value));

        var homeworks = await asyncExecuter.ToListAsync(query);
        var rows = await BuildHomeworkDashboardRowsAsync(homeworks);

        var dashboard = new LearningTaskDashboardDto
        {
            TotalTasks = rows.Count,
            PendingPublish = rows
                .Where(r => r.Status == (int)HomeworkStatus.Draft || r.PublishedDivisionCount < r.TotalDivisionCount)
                .OrderBy(r => r.NextPublishAt ?? DateTime.MaxValue)
                .Take(20)
                .ToList(),
            DueToday = rows
                .Where(r => r.NextDueAt.HasValue && r.NextDueAt.Value.Date == asOfDate)
                .OrderBy(r => r.NextDueAt)
                .Take(20)
                .ToList(),
            Overdue = rows
                .Where(r => r.NextDueAt.HasValue && r.NextDueAt.Value.Date < asOfDate && r.Status != (int)HomeworkStatus.Closed)
                .OrderBy(r => r.NextDueAt)
                .Take(20)
                .ToList(),
            EvaluationPending = rows
                .Where(r => r.EvaluationPendingCount > 0)
                .OrderByDescending(r => r.EvaluationPendingCount)
                .Take(20)
                .ToList()
        };

        dashboard.PendingPublishCount = rows.Count(r => r.Status == (int)HomeworkStatus.Draft || r.PublishedDivisionCount < r.TotalDivisionCount);
        dashboard.DueTodayCount = rows.Count(r => r.NextDueAt.HasValue && r.NextDueAt.Value.Date == asOfDate);
        dashboard.OverdueCount = rows.Count(r => r.NextDueAt.HasValue && r.NextDueAt.Value.Date < asOfDate && r.Status != (int)HomeworkStatus.Closed);
        dashboard.EvaluationPendingCount = rows.Count(r => r.EvaluationPendingCount > 0);
        return dashboard;
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add)]
    public async Task<LearningTaskReminderResponseDto> SendReminderAsync(
        Guid homeworkId,
        LearningTaskReminderRequestDto input
    )
    {
        var query = await homeworkRepository.WithDetailsAsync(h => h.DivisionSchedules);
        var homework = await asyncExecuter.FirstOrDefaultAsync(query.Where(h => h.Id == homeworkId))
            ?? throw new BusinessException("ShikshaMitra.Homework:NotFound").WithData("Id", homeworkId);

        await EnsureCurrentUserCanAccessHomeworkAsync(homework, nameof(SendReminderAsync));

        var targetSchedules = homework.DivisionSchedules
            .Where(s => s.IsPublished)
            .Where(s => !input.DivisionId.HasValue || s.DivisionId == input.DivisionId.Value)
            .ToList();
        if (targetSchedules.Count == 0)
        {
            throw new BusinessException("ShikshaMitra.Homework:ReminderRequiresPublishedDivision")
                .WithData("HomeworkId", homeworkId);
        }

        var targetDivisionIds = targetSchedules.Select(s => s.DivisionId).Distinct().ToList();
        var submissions = await studentSubmissionRepository.GetListAsync(s =>
            s.HomeworkAssignmentId == homeworkId &&
            targetDivisionIds.Contains(s.DivisionId));
        var pendingSubmissionCount = submissions.Count(s =>
            s.Status is SubmissionStatus.NotSubmitted or SubmissionStatus.Draft or SubmissionStatus.Submitted or SubmissionStatus.Late or SubmissionStatus.NeedsReview);

        await homeworkRepository.UpdateAsync(homework, autoSave: true);

        return new LearningTaskReminderResponseDto
        {
            TaskId = homework.Id,
            TaskType = "homework",
            TargetDivisionCount = targetDivisionIds.Count,
            PendingSubmissionCount = pendingSubmissionCount,
            ReminderQueuedAt = DateTime.UtcNow,
            Message = string.IsNullOrWhiteSpace(input.Message)
                ? "Homework reminder queued for published target divisions."
                : input.Message.Trim()
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View)]
    public async Task<LearningTaskHistoryDto> GetHistoryAsync(Guid homeworkId)
    {
        var query = await homeworkRepository.WithDetailsAsync(h => h.DivisionSchedules);
        var homework = await asyncExecuter.FirstOrDefaultAsync(query.Where(h => h.Id == homeworkId))
            ?? throw new BusinessException("ShikshaMitra.Homework:NotFound").WithData("Id", homeworkId);

        await EnsureCurrentUserCanAccessHomeworkAsync(homework, nameof(GetHistoryAsync));

        var items = new List<LearningTaskHistoryItemDto>
        {
            new()
            {
                Action = "Created",
                Summary = $"Homework created in {homework.Status} status.",
                OccurredAt = homework.CreationTime,
                ActorUserId = homework.CreatorId,
                Severity = "info"
            }
        };

        if (homework.DivisionSchedules.Any(s => s.PublishedAt.HasValue))
        {
            items.Add(new()
            {
                Action = "Published",
                Summary = $"{homework.DivisionSchedules.Count(s => s.IsPublished)} division schedule(s) published.",
                OccurredAt = homework.DivisionSchedules.Where(s => s.PublishedAt.HasValue).Min(s => s.PublishedAt!.Value),
                ActorUserId = homework.LastModifierId ?? homework.CreatorId,
                Severity = "success"
            });
        }

        if (homework.LastModificationTime.HasValue)
        {
            items.Add(new()
            {
                Action = "Updated",
                Summary = $"Homework last updated while status was {homework.Status}.",
                OccurredAt = homework.LastModificationTime.Value,
                ActorUserId = homework.LastModifierId,
                Severity = "info"
            });
        }

        if (homework.Status == HomeworkStatus.Closed)
        {
            items.Add(new()
            {
                Action = "Closed",
                Summary = "Homework closed after pending-submission validation.",
                OccurredAt = homework.LastModificationTime ?? DateTimeOffset.UtcNow,
                ActorUserId = homework.LastModifierId,
                Severity = "success"
            });
        }

        return new LearningTaskHistoryDto
        {
            TaskId = homework.Id,
            TaskType = "homework",
            Items = items.OrderByDescending(i => i.OccurredAt).ToList()
        };
    }

    private async Task<IQueryable<HomeworkAssignmentEntity>> ApplyCurrentUserScopeAsync(
        IQueryable<HomeworkAssignmentEntity> query)
    {
        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        if (scope.IsAdmin || scope.HasTenantWideScope())
        {
            return query;
        }

        var scopeFilteredQuery = userScopeAccessService.ApplyScope(query, scope);
        var accessibleHomeworkIds = await GetCurrentTeacherSubjectOwnedHomeworkIdsAsync(query);
        accessibleHomeworkIds.UnionWith(await asyncExecuter.ToListAsync(
            scopeFilteredQuery.Select(homework => homework.Id)));
        if (scope.DivisionIds.Count == 0)
        {
            // Explicit subject ownership remains valid without a separate generic
            // division scope. This is the normal case for subject teachers/HODs.
            return query.Where(homework => accessibleHomeworkIds.Contains(homework.Id));
        }

        // Homework can target multiple divisions while the legacy aggregate has no single
        // DivisionId. An exact division-scoped user may only see a homework when every
        // target division is inside their assigned scope.
        var divisionScopedQuery = query.Where(homework =>
            homework.DivisionSchedules.Any() &&
            homework.DivisionSchedules.All(schedule => scope.DivisionIds.Contains(schedule.DivisionId)));
        accessibleHomeworkIds.UnionWith(await asyncExecuter.ToListAsync(
            divisionScopedQuery.Select(homework => homework.Id)));

        return query.Where(homework => accessibleHomeworkIds.Contains(homework.Id));
    }

    private async Task EnsureCurrentUserCanAccessHomeworkAsync(
        HomeworkAssignmentEntity homework,
        string operation)
    {
        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        if (scope.IsAdmin || scope.HasTenantWideScope() ||
            (homework.DivisionSchedules.Count > 0 &&
             homework.DivisionSchedules.All(schedule => scope.DivisionIds.Contains(schedule.DivisionId))))
        {
            return;
        }

        if (await HasTeacherSubjectAccessAsync(
                homework.StandardId,
                homework.SubjectId,
                homework.DivisionSchedules.Select(schedule => schedule.DivisionId).Distinct().ToList()))
        {
            return;
        }

        userScopeAccessService.EnsureCanAccess(homework, scope, operation: operation);
    }

    private async Task EnsureCurrentUserCanAccessTargetAsync(
        Guid academicMediumId,
        Guid standardId,
        Guid subjectId,
        IEnumerable<Guid> divisionIds)
    {
        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        var targetDivisionIds = divisionIds.Distinct().ToList();
        if (scope.IsAdmin || scope.HasTenantWideScope() ||
            (targetDivisionIds.Count > 0 && targetDivisionIds.All(scope.DivisionIds.Contains)))
        {
            return;
        }

        // A subject assignment is an explicit resource-level grant. Teachers and
        // reporting managers may create homework for that subject even when their
        // generic UserScope does not separately contain the target division.
        if (await HasTeacherSubjectAccessAsync(standardId, subjectId, targetDivisionIds))
        {
            return;
        }

        // A synthetic aggregate deliberately has no DivisionId: a single matching division
        // must not grant access to a multi-division target outside the user's scope.
        var target = new HomeworkAssignmentEntity(
            GuidGenerator.Create(),
            standardId,
            Guid.Empty,
            string.Empty,
            string.Empty,
            Guid.Empty,
            SubmissionType.OnlineText,
            CurrentTenant.Id,
            educationMediumId: academicMediumId);
        userScopeAccessService.EnsureCanAccess(target, scope, operation: "homework-target");
    }

    private async Task<bool> HasTeacherSubjectAccessAsync(
        Guid standardId,
        Guid subjectId,
        IReadOnlyCollection<Guid> divisionIds)
    {
        var teacherIds = await GetCurrentAccessibleTeacherIdsAsync();
        if (teacherIds.Count == 0)
        {
            return false;
        }

        var assignedSubjectQuery = await assignedSubjectRepository.WithDetailsAsync(
            assignment => assignment.Teachers);
        var hasClassSubjectAssignment = await asyncExecuter.AnyAsync(
            assignedSubjectQuery.Where(assignment =>
                assignment.StandardId == standardId &&
                assignment.SubjectId == subjectId &&
                assignment.Teachers.Any(teacher => teacherIds.Contains(teacher.TeacherId))));
        if (hasClassSubjectAssignment)
        {
            return true;
        }

        if (divisionIds.Count == 0)
        {
            return false;
        }

        var allocations = await teacherSubjectAllocationRepository.GetListAsync(allocation =>
            allocation.StandardId == standardId &&
            allocation.SubjectId == subjectId &&
            divisionIds.Contains(allocation.DivisionId) &&
            allocation.Teachers.Any(teacher => teacherIds.Contains(teacher.TeacherId)));
        var allocatedDivisionIds = allocations.Select(allocation => allocation.DivisionId).ToHashSet();

        // Multi-division homework must be authorized for every selected division.
        return divisionIds.All(allocatedDivisionIds.Contains);
    }

    private async Task<HashSet<Guid>> GetCurrentAccessibleTeacherIdsAsync()
    {
        var teacherIds = new HashSet<Guid>();
        if (!CurrentUser.Id.HasValue)
        {
            return teacherIds;
        }

        teacherIds.Add(CurrentUser.Id.Value);
        var employee = await employeeRepository.FirstOrDefaultAsync(
            candidate => candidate.UserId == CurrentUser.Id.Value);
        if (employee != null)
        {
            teacherIds.Add(employee.Id);
        }

        teacherIds.UnionWith(await reportingManagerAccessService
            .GetReporteeEmployeeIdsAsync(CurrentUser.Id.Value));
        return teacherIds;
    }

    private async Task<HashSet<Guid>> GetCurrentTeacherSubjectOwnedHomeworkIdsAsync(
        IQueryable<HomeworkAssignmentEntity> unscopedQuery)
    {
        var teacherIds = await GetCurrentAccessibleTeacherIdsAsync();
        if (teacherIds.Count == 0)
        {
            return [];
        }

        var assignedSubjectQuery = await assignedSubjectRepository.WithDetailsAsync(
            assignment => assignment.Teachers);
        var assignedSubjects = await asyncExecuter.ToListAsync(
            assignedSubjectQuery.Where(assignment =>
                assignment.Teachers.Any(teacher => teacherIds.Contains(teacher.TeacherId))));
        var assignedSubjectKeys = assignedSubjects
            .Select(assignment => (assignment.StandardId, assignment.SubjectId))
            .ToHashSet();

        var allocations = await teacherSubjectAllocationRepository.GetListAsync(allocation =>
            allocation.Teachers.Any(teacher => teacherIds.Contains(teacher.TeacherId)));
        var allocatedSubjectKeys = allocations
            .Select(allocation => (allocation.StandardId, allocation.DivisionId, allocation.SubjectId))
            .ToHashSet();

        var candidates = await asyncExecuter.ToListAsync(unscopedQuery);
        return candidates
            .Where(homework =>
                assignedSubjectKeys.Contains((homework.StandardId, homework.SubjectId)) ||
                (homework.DivisionSchedules.Count > 0 &&
                 homework.DivisionSchedules.All(schedule =>
                     allocatedSubjectKeys.Contains((homework.StandardId, schedule.DivisionId, homework.SubjectId)))))
            .Select(homework => homework.Id)
            .ToHashSet();
    }

    private async Task<List<LearningTaskDashboardItemDto>> BuildHomeworkDashboardRowsAsync(List<HomeworkAssignmentEntity> homeworks)
    {
        var standardIds = homeworks.Select(h => h.StandardId).Distinct().ToList();
        var subjectIds = homeworks.Select(h => h.SubjectId).Distinct().ToList();
        var divisionIds = homeworks.SelectMany(h => h.DivisionSchedules.Select(s => s.DivisionId)).Distinct().ToList();
        var homeworkIds = homeworks.Select(h => h.Id).Distinct().ToList();

        var standards = standardIds.Count == 0 ? [] : await standardRepository.GetListAsync(s => standardIds.Contains(s.Id));
        var subjects = subjectIds.Count == 0 ? [] : await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
        var divisions = divisionIds.Count == 0 ? [] : await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id));
        var submissions = homeworkIds.Count == 0
            ? []
            : await studentSubmissionRepository.GetListAsync(s => s.HomeworkAssignmentId.HasValue && homeworkIds.Contains(s.HomeworkAssignmentId.Value));

        var standardLookup = standards.ToDictionary(s => s.Id, s => s.Name);
        var subjectLookup = subjects.ToDictionary(s => s.Id, s => s.Name);
        var divisionLookup = divisions.ToDictionary(d => d.Id, d => d.Name);
        var submissionsByHomework = submissions
            .GroupBy(s => s.HomeworkAssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return homeworks.Select(homework =>
        {
            submissionsByHomework.TryGetValue(homework.Id, out var taskSubmissions);
            taskSubmissions ??= [];
            var schedules = homework.DivisionSchedules.ToList();
            var dueAt = schedules
                .Where(s => s.IsPublished)
                .Select(s => (DateTime?)s.DueDate)
                .OrderBy(d => d)
                .FirstOrDefault();
            var publishAt = schedules
                .Where(s => !s.IsPublished)
                .Select(s => s.PublishDate)
                .Where(d => d.HasValue)
                .OrderBy(d => d)
                .FirstOrDefault();

            return new LearningTaskDashboardItemDto
            {
                Id = homework.Id,
                TaskType = "homework",
                Title = homework.Title,
                StandardId = homework.StandardId,
                StandardName = standardLookup.GetValueOrDefault(homework.StandardId, string.Empty),
                SubjectId = homework.SubjectId,
                SubjectName = subjectLookup.GetValueOrDefault(homework.SubjectId, string.Empty),
                Status = (int)homework.Status,
                StatusLabel = homework.Status.ToString(),
                NextPublishAt = publishAt,
                NextDueAt = dueAt,
                PublishedDivisionCount = schedules.Count(s => s.IsPublished),
                TotalDivisionCount = schedules.Count,
                SubmittedCount = taskSubmissions.Count(s => s.Status is SubmissionStatus.Submitted or SubmissionStatus.Late or SubmissionStatus.Reviewed),
                LateCount = taskSubmissions.Count(s => s.Status == SubmissionStatus.Late),
                EvaluationPendingCount = taskSubmissions.Count(s => s.Status is SubmissionStatus.Submitted or SubmissionStatus.Late or SubmissionStatus.NeedsReview),
                DivisionIds = schedules.Select(s => s.DivisionId).Distinct().ToList(),
                DivisionNames = schedules.Select(s => divisionLookup.GetValueOrDefault(s.DivisionId, string.Empty)).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList()
            };
        }).ToList();
    }

    private async Task ValidateHomeworkScheduleDatesAsync(
        Guid standardId,
        IEnumerable<CreateHomeworkDivisionScheduleDto> schedules)
    {
        var rows = schedules.ToList();
        if (rows.Count == 0)
        {
            throw new UserFriendlyException("Select at least one division schedule before saving this homework.");
        }

        if (rows.Any(x => x.DivisionId == Guid.Empty))
        {
            throw new UserFriendlyException("Division is required for every homework schedule row.");
        }

        var divisionIds = rows.Select(row => row.DivisionId).Distinct().ToList();
        var divisions = await divisionRepository.GetListAsync(division => divisionIds.Contains(division.Id));
        var divisionLookup = divisions.ToDictionary(division => division.Id, division => division.Name);

        foreach (var row in rows)
        {
            var divisionName = divisionLookup.GetValueOrDefault(row.DivisionId, "selected division");
            if (!row.PublishAt.HasValue)
            {
                throw new UserFriendlyException($"Publish Date is required for {divisionName}.");
            }

            if (row.DueAt <= row.PublishAt.Value)
            {
                throw new UserFriendlyException(
                    $"Due Date must be after Publish Date for {divisionName}. Publish Date: {row.PublishAt.Value:dd/MM/yyyy HH:mm}; Due Date: {row.DueAt:dd/MM/yyyy HH:mm}.");
            }

            await EnsureHomeworkDueDateAllowedAsync(standardId, row.DivisionId, row.DueAt, divisionName);
        }
    }

    private async Task ValidateExistingHomeworkScheduleDatesAsync(HomeworkAssignmentEntity homework)
    {
        foreach (var row in homework.DivisionSchedules)
        {
            await EnsureHomeworkDueDateAllowedAsync(homework.StandardId, row.DivisionId, row.DueDate);
        }
    }

    private async Task EnsureHomeworkCanCloseAsync(Guid homeworkId)
    {
        var pendingCount = await studentSubmissionRepository.CountAsync(s =>
            s.HomeworkAssignmentId == homeworkId &&
            (s.Status == SubmissionStatus.Submitted ||
             s.Status == SubmissionStatus.Late ||
             s.Status == SubmissionStatus.NeedsReview ||
             s.Status == SubmissionStatus.Draft));

        if (pendingCount > 0)
        {
            throw new BusinessException("ShikshaMitra.Homework:CannotCloseWithPendingSubmissions")
                .WithData("HomeworkId", homeworkId)
                .WithData("PendingSubmissions", pendingCount);
        }
    }

    private async Task EnsureHomeworkDueDateAllowedAsync(
        Guid standardId,
        Guid divisionId,
        DateTime dueDate,
        string? divisionName = null)
    {
        var date = dueDate.Date;
        var blockedDays = await calendarDayRepository.GetListAsync(day =>
            day.Status == CalendarDayStatus.Active &&
            day.Date.Date == date &&
            (day.AppliesToAll ||
                (day.StandardId.HasValue && day.StandardId.Value == standardId) ||
                (day.DivisionId.HasValue && day.DivisionId.Value == divisionId)));

        var blockingDay = blockedDays.FirstOrDefault(day => CalendarDayBlockingRules.BlocksHomeworkDueDate(day.DayType));
        if (blockingDay != null)
        {
            var targetDivision = string.IsNullOrWhiteSpace(divisionName)
                ? "the selected division"
                : divisionName;
            throw new UserFriendlyException(
                $"Due Date {date:dd/MM/yyyy} for {targetDivision} is blocked by the academic calendar entry \"{blockingDay.Title}\". Choose another due date.");
        }
    }

    private async Task<HomeworkDto> MapToHomeworkDtoAsync(HomeworkAssignmentEntity h, Guid academicMediumId)
    {
        var standardName = string.Empty;
        var subjectName = string.Empty;
        string? chapterName = null;
        var chapterIds = new List<Guid>();
        var chapterNames = new List<string>();
        HomeworkType? hwType = null;
        var divisionLookup = new Dictionary<Guid, string>();
        var createdBy = string.Empty;
        var updatedBy = string.Empty;

        var standard = await standardRepository.FindAsync(h.StandardId);
        standardName = standard?.Name ?? string.Empty;

        var subject = await subjectRepository.FindAsync(h.SubjectId);
        if (subject == null)
        {
            // Fallback: SubjectId might actually be an AssignedSubject.Id
            var assignedSubject = await assignedSubjectRepository.FindAsync(h.SubjectId);
            if (assignedSubject != null)
                subject = await subjectRepository.FindAsync(assignedSubject.SubjectId);
        }
        subjectName = subject?.Name ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(h.ChapterIdsJson))
        {
            try
            {
                var ids = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(h.ChapterIdsJson);
                if (ids != null) chapterIds.AddRange(ids);
            }
            catch { }
        }

        if (chapterIds.Count == 0 && h.ChapterId.HasValue)
        {
            chapterIds.Add(h.ChapterId.Value);
        }

        if (chapterIds.Any())
        {
            var chapters = await chapterRepository.GetListAsync(c => chapterIds.Contains(c.Id));
            var chapterMap = chapters.ToDictionary(c => c.Id, c => c.ChapterName);
            foreach (var id in chapterIds)
            {
                if (chapterMap.TryGetValue(id, out var name))
                {
                    chapterNames.Add(name);
                }
            }
            chapterName = string.Join(", ", chapterNames);
        }

        if (h.HomeworkTypeId.HasValue)
        {
            hwType = await homeworkTypeRepository.FindAsync(h.HomeworkTypeId.Value);
        }

        var divisionIds = h.DivisionSchedules.Select(s => s.DivisionId).ToList();
        if (divisionIds.Any())
        {
            var divisions = await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id));
            divisionLookup = divisions.ToDictionary(d => d.Id, d => d.Name);
        }

        if (h.CreatorId.HasValue)
        {
            var creator = await userRepository.FindAsync(h.CreatorId.Value);
            createdBy = creator != null ? $"{creator.Name} {creator.Surname}".Trim() : string.Empty;
        }

        if (h.LastModifierId.HasValue)
        {
            var updater = await userRepository.FindAsync(h.LastModifierId.Value);
            updatedBy = updater != null ? $"{updater.Name} {updater.Surname}".Trim() : string.Empty;
        }
        var submissions = await studentSubmissionRepository.GetListAsync(s => s.HomeworkAssignmentId == h.Id);
        var submissionSummary = BuildSubmissionSummary(submissions);

        return BuildHomeworkDto(h, academicMediumId, standardName, subjectName, chapterName,
            chapterIds, chapterNames, hwType, divisionLookup, createdBy, updatedBy,
            submissionSummary);
    }

    private static HomeworkDto MapToHomeworkDto(
        HomeworkAssignmentEntity h,
        Guid academicMediumId,
        Dictionary<Guid, string> standardLookup,
        Dictionary<Guid, string> subjectLookup,
        Dictionary<Guid, string> chapterLookup,
        Dictionary<Guid, HomeworkType> hwTypeLookup,
        Dictionary<Guid, string> divisionLookup,
        Dictionary<Guid, string> userLookup,
        LearningTaskSubmissionSummary? submissionSummary = null
    )
    {
        var standardName = standardLookup.GetValueOrDefault(h.StandardId, string.Empty);
        var subjectName = subjectLookup.GetValueOrDefault(h.SubjectId, string.Empty);
        
        var chapterIds = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(h.ChapterIdsJson))
        {
            try
            {
                chapterIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(h.ChapterIdsJson) ?? new List<Guid>();
            }
            catch { }
        }
        if (chapterIds.Count == 0 && h.ChapterId.HasValue)
        {
            chapterIds.Add(h.ChapterId.Value);
        }

        var chapterNames = chapterIds
            .Select(id => chapterLookup.GetValueOrDefault(id))
            .Where(name => name != null)
            .ToList()!;

        string? chapterName = chapterNames.Any() ? string.Join(", ", chapterNames) : null;

        var hwType = h.HomeworkTypeId.HasValue
            ? hwTypeLookup.GetValueOrDefault(h.HomeworkTypeId.Value)
            : null;
        var createdBy = h.CreatorId.HasValue
            ? userLookup.GetValueOrDefault(h.CreatorId.Value, string.Empty)
            : string.Empty;
        var updatedBy = h.LastModifierId.HasValue
            ? userLookup.GetValueOrDefault(h.LastModifierId.Value, string.Empty)
            : string.Empty;

        return BuildHomeworkDto(h, academicMediumId, standardName, subjectName, chapterName,
            chapterIds, chapterNames, hwType, divisionLookup, createdBy, updatedBy,
            submissionSummary);
    }

    private static HomeworkDto BuildHomeworkDto(
        HomeworkAssignmentEntity h,
        Guid academicMediumId,
        string standardName,
        string subjectName,
        string? chapterName,
        List<Guid> chapterIds,
        List<string> chapterNames,
        HomeworkType? homeworkType,
        Dictionary<Guid, string> divisionLookup,
        string createdBy,
        string updatedBy,
        LearningTaskSubmissionSummary? submissionSummary = null
    ) => new()
    {
        Id = h.Id,
        AcademicMediumId = academicMediumId,
        StandardId = h.StandardId,
        StandardName = standardName,
        SubjectId = h.SubjectId,
        SubjectName = subjectName,
        ChapterId = h.ChapterId == Guid.Empty ? null : h.ChapterId,
        ChapterName = chapterName,
        ChapterIds = chapterIds,
        ChapterNames = chapterNames,
        Title = h.Title,
        HomeworkTypeId = h.HomeworkTypeId ?? Guid.Empty,
        HomeworkTypeName = homeworkType?.Name ?? string.Empty,
        DefaultSubmissionType = homeworkType?.DefaultSubmissionType.HasValue == true ? (int) homeworkType.DefaultSubmissionType.Value : null,
        DefaultMarks = homeworkType?.DefaultMarks,
        DefaultDueDays = homeworkType?.DefaultDueDays,
        EvaluationRequired = homeworkType?.EvaluationRequired ?? true,
        DefaultEvaluationMethod = homeworkType?.DefaultEvaluationMethod.HasValue == true ? (int) homeworkType.DefaultEvaluationMethod.Value : null,
        HomeworkTypeStatus = homeworkType != null ? (int) homeworkType.Status : null,
        Description = h.Description,
        Attachment = h.DocumentUrl,
        SubmissionType = (int) h.SubmissionType,
        Status = (int) h.Status,
        TotalStudents = submissionSummary?.TotalStudents ?? 0,
        SubmittedCount = submissionSummary?.SubmittedCount ?? 0,
        PendingCount = submissionSummary?.PendingCount ?? 0,
        LateCount = submissionSummary?.LateCount ?? 0,
        CheckedCount = submissionSummary?.CheckedCount ?? 0,
        EvaluationPendingCount = submissionSummary?.EvaluationPendingCount ?? 0,
        DivisionSchedules = h.DivisionSchedules
            .Select(s => new HomeworkDivisionScheduleDto
            {
                DivisionId = s.DivisionId,
                DivisionName = divisionLookup.GetValueOrDefault(s.DivisionId, string.Empty),
                PublishAt = s.PublishDate,
                DueAt = s.DueDate,
                IsPublished = s.IsPublished,
            })
            .ToList(),
        CreatedAt = h.CreationTime,
        CreatedBy = createdBy,
        UpdatedAt = h.LastModificationTime ?? h.CreationTime,
        UpdatedBy = updatedBy,
    };

    private static LearningTaskSubmissionSummary BuildSubmissionSummary(IEnumerable<StudentSubmissionEntity> submissions)
    {
        var list = submissions.ToList();
        var submittedCount = list.Count(s =>
            s.Status is SubmissionStatus.Submitted or SubmissionStatus.Late or SubmissionStatus.Reviewed);
        var lateCount = list.Count(s => s.Status == SubmissionStatus.Late);
        var checkedCount = list.Count(s => s.Status == SubmissionStatus.Reviewed);
        var evaluationPendingCount = list.Count(s =>
            s.Status is SubmissionStatus.Submitted or SubmissionStatus.Late or SubmissionStatus.NeedsReview);

        return new LearningTaskSubmissionSummary(
            TotalStudents: 0,
            SubmittedCount: submittedCount,
            PendingCount: 0,
            LateCount: lateCount,
            CheckedCount: checkedCount,
            EvaluationPendingCount: evaluationPendingCount
        );
    }

    private sealed record LearningTaskSubmissionSummary(
        int TotalStudents,
        int SubmittedCount,
        int PendingCount,
        int LateCount,
        int CheckedCount,
        int EvaluationPendingCount
    );
}
