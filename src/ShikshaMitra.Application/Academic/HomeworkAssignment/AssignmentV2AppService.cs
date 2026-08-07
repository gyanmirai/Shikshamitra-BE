using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.HomeworkAssignment;

[Authorize]
[RemoteService(IsEnabled = false)]
public class AssignmentV2AppService(
    IRepository<AssignmentEntity, Guid> assignmentRepository,
    IRepository<AssignmentDivisionSchedule, Guid> scheduleRepository,
    IDivisionRepository divisionRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Subject, Guid> subjectRepository,
    IAssignmentTypeRepository assignmentTypeRepository,
    IRepository<IdentityUser, Guid> userRepository,
    IUserScopeService userScopeService,
    AssignmentManager assignmentManager,
    IAsyncQueryableExecuter asyncExecuter,
    IRepository<StudentSubmissionEntity, Guid> studentSubmissionRepository,
    IRepository<AcademicCalendarDay, Guid> calendarDayRepository,
    IRepository<AcademicCalendar, Guid> calendarRepository,
    IRepository<AcademicYear, Guid> academicYearRepository,
    IRepository<EducationBoard, Guid> boardRepository,
    IRepository<EducationMedium, Guid> mediumRepository,
    IRepository<EducationMediumStage, Guid> mediumStageRepository,
    IRepository<AssignedSubject, Guid> assignedSubjectRepository,
    IRepository<AssignedSubjectChapter, Guid> chapterRepository
) : ApplicationService, IAssignmentV2AppService
{

    // ─────────────────────────────────────────────────────────────────────
    // CREATE
    // ─────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add)]
    public async Task<AssignmentV2CreateResponseDto> CreateAsync(
        Guid academicMediumId,
        CreateAssignmentV2Dto input,
        string? attachmentBlobName
    )
    {
        if (academicMediumId == Guid.Empty)
            throw new BusinessException("ShikshaMitra.Assignment:InvalidAcademicMedium");

        var assignmentType = await assignmentManager.ValidateAssignmentTypeAsync(
            input.AssignmentTypeId,
            academicMediumId);
        var hierarchy = await ValidateAndResolveHierarchyAsync(academicMediumId, input);
        await ValidateAssignmentScheduleDatesAsync(
            hierarchy.AcademicYearId,
            input.StandardId,
            input.DivisionSchedules);
        var settings = ResolveEffectiveSettings(input, assignmentType);
        await EnsureCurrentUserCanAccessTargetAsync(
            hierarchy,
            input.StandardId,
            input.DivisionSchedules.Select(schedule => schedule.DivisionId));

        var assignment = new AssignmentEntity(
            GuidGenerator.Create(),
            input.StandardId,
            input.SubjectId,
            input.Title.Trim(),
            input.Description.Trim(),
            input.AssignmentTypeId,
            settings.SubmissionType,
            settings.TotalMarks,
            CurrentTenant.Id,
            academicYearId: hierarchy.AcademicYearId,
            attachmentsJson: AssignmentAttachmentSerializer.Canonicalize(attachmentBlobName),
            educationBoardId: hierarchy.BoardId,
            educationMediumId: hierarchy.MediumId,
            educationMediumStageId: hierarchy.MediumStageId,
            evaluationRequired: settings.EvaluationRequired,
            evaluationMethod: settings.EvaluationMethod
        );

        foreach (var div in input.DivisionSchedules)
        {
            assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(
                GuidGenerator.Create(),
                assignment.Id,
                div.DivisionId,
                div.PublishAt,
                div.DueAt,
                CurrentTenant.Id
            ));
        }

        if (input.ChapterIds != null && input.ChapterIds.Count > 0)
            assignment.ChapterIdsJson = System.Text.Json.JsonSerializer.Serialize(input.ChapterIds.Distinct());

        await assignmentRepository.InsertAsync(assignment, autoSave: true);

        var dto = await MapToAssignmentDtoAsync(assignment, academicMediumId);
        return new AssignmentV2CreateResponseDto
        {
            Message = L["ShikshaMitra.Assignment:CreatedSuccessfully"].Value,
            Data = dto,
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET LIST (paginated, filtered by division + publish date)
    // ─────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View)]
    public async Task<AssignmentV2ListResponseDto> GetListAsync(
        Guid academicMediumId,
        GetAssignmentV2ListFilterDto filter
    )
    {
        var query = await assignmentRepository.WithDetailsAsync(
            a => a.DivisionSchedules,
            a => a.AssignmentType
        );

        var today = Clock.Now.Date;
        var now = Clock.Now;
        var scope = await GetCurrentScopeAsync();
        query = ApplyAssignmentScope(query, scope);

        query = query
            .Where(a => a.EducationMediumId == academicMediumId)
            .Where(a => a.StandardId == filter.StandardId)
            .Where(a => a.SubjectId == filter.SubjectId)
            .Where(a => a.DivisionSchedules.Any(s =>
                s.DivisionId == filter.DivisionId
                && (!filter.AssignmentDate.HasValue ||
                    ((!s.PublishDate.HasValue || s.PublishDate.Value.Date <= filter.AssignmentDate.Value.Date)
                    && s.DueDate.Date >= filter.AssignmentDate.Value.Date))));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(a =>
                a.Title.ToLower().Contains(search) ||
                a.Description.ToLower().Contains(search));
        }

        query = query.OrderByDescending(a => a.CreationTime);

        var totalCount = await asyncExecuter.CountAsync(query);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = Math.Clamp(filter.PageSize, 10, 100);
        var totalPages = (int) Math.Ceiling((double) totalCount / pageSize);

        var items = await asyncExecuter.ToListAsync(
            query.Skip((page - 1) * pageSize).Take(pageSize)
        );

        // Batch-load related entities
        var standardIds = items.Select(a => a.StandardId).Distinct().ToList();
        var subjectIds = items.Select(a => a.SubjectId).Distinct().ToList();
        var assignmentTypeIds = items.Select(a => a.AssignmentTypeId).Distinct().ToList();
        var divisionIds = items.SelectMany(a => a.DivisionSchedules.Select(s => s.DivisionId))
            .Distinct().ToList();
        var creatorIds = items.Where(a => a.CreatorId.HasValue)
            .Select(a => a.CreatorId!.Value).Distinct().ToList();
        var updaterIds = items.Where(a => a.LastModifierId.HasValue)
            .Select(a => a.LastModifierId!.Value).Distinct().ToList();

        var allUserIds = creatorIds.Union(updaterIds).Distinct().ToList();

        var standards = await standardRepository.GetListAsync(s => standardIds.Contains(s.Id));
        var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
        var assignmentTypes = await assignmentTypeRepository.GetListAsync(t => assignmentTypeIds.Contains(t.Id));
        var divisions = await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id));
        var users = allUserIds.Any()
            ? await userRepository.GetListAsync(u => allUserIds.Contains(u.Id))
            : new List<IdentityUser>();

        var standardLookup = standards.ToDictionary(s => s.Id, s => s.Name);
        var subjectLookup = subjects.ToDictionary(s => s.Id, s => s.Name);
        var typeLookup = assignmentTypes.ToDictionary(t => t.Id);
        var divisionLookup = divisions.ToDictionary(d => d.Id, d => d.Name);
        var userLookup = users.ToDictionary(u => u.Id, u => $"{u.Name} {u.Surname}".Trim());

        var dtos = items
            .Select(a => MapToAssignmentDto(a, academicMediumId, standardLookup, subjectLookup,
                typeLookup, divisionLookup, userLookup))
            .ToList();

        return new AssignmentV2ListResponseDto
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
    public async Task<AssignmentV2ResponseDto> GetAsync(Guid id, Guid? academicMediumId = null)
    {
        var q = await assignmentRepository.WithDetailsAsync(
            a => a.DivisionSchedules,
            a => a.AssignmentType
        );

        var assignment = await asyncExecuter.FirstOrDefaultAsync(
            q.Where(a => a.Id == id)
        ) ?? throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", id);

        if (academicMediumId.HasValue && assignment.EducationMediumId != academicMediumId.Value)
            throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", id);

        await EnsureCurrentUserCanAccessAssignmentAsync(assignment, "get");
        var dto = await MapToAssignmentDtoAsync(assignment, assignment.EducationMediumId ?? Guid.Empty);
        return new AssignmentV2ResponseDto { Data = dto };
    }

    // ─────────────────────────────────────────────────────────────────────
    // UPDATE
    // ─────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add)]
    public async Task<AssignmentV2ResponseDto> UpdateAsync(
        Guid assignmentId,
        UpdateAssignmentV2Dto input,
        string? attachmentBlobName
    )
    {
        var q = await assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);
        var assignment = await asyncExecuter.FirstOrDefaultAsync(q.Where(a => a.Id == assignmentId))
            ?? throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", assignmentId);

        await EnsureCurrentUserCanAccessAssignmentAsync(assignment, "update");
        if (assignment.Status != AssignmentStatus.Draft)
            throw new BusinessException("ShikshaMitra.Assignment:CannotUpdatePublished")
                .WithData("AssignmentId", assignmentId);

        var assignmentType = await assignmentManager.ValidateAssignmentTypeAsync(
            input.AssignmentTypeId,
            assignment.EducationMediumId);
        var hierarchy = await ValidateAndResolveHierarchyAsync(
            assignment.EducationMediumId ?? Guid.Empty,
            input);
        await ValidateAssignmentScheduleDatesAsync(
            hierarchy.AcademicYearId,
            input.StandardId,
            input.DivisionSchedules);
        var settings = ResolveEffectiveSettings(input, assignmentType);
        await EnsureCurrentUserCanAccessTargetAsync(
            hierarchy,
            input.StandardId,
            input.DivisionSchedules.Select(schedule => schedule.DivisionId));

        var resolvedBlob = input.RemoveAttachment
            ? null
            : attachmentBlobName != null
                ? AssignmentAttachmentSerializer.Canonicalize(attachmentBlobName)
                : assignment.AttachmentsJson;

        assignment.UpdateBasicInfo(
            input.Title.Trim(),
            input.Description.Trim(),
            settings.TotalMarks,
            resolvedBlob,
            settings.EvaluationRequired,
            settings.EvaluationMethod);
        assignment.AcademicYearId = hierarchy.AcademicYearId;
        assignment.EducationBoardId = hierarchy.BoardId;
        assignment.EducationMediumStageId = hierarchy.MediumStageId;
        assignment.StandardId = input.StandardId;
        assignment.SubjectId = input.SubjectId;
        assignment.AssignmentTypeId = input.AssignmentTypeId;
        assignment.SubmissionType = settings.SubmissionType;
        assignment.ChapterIdsJson = input.ChapterIds != null && input.ChapterIds.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(input.ChapterIds.Distinct())
            : null;

        // Replace division schedules
        var existing = assignment.DivisionSchedules.ToList();
        await scheduleRepository.DeleteManyAsync(existing);
        assignment.ClearDivisionSchedules();

        foreach (var div in input.DivisionSchedules)
        {
            assignment.AddDivisionSchedule(new AssignmentDivisionSchedule(
                GuidGenerator.Create(),
                assignment.Id,
                div.DivisionId,
                div.PublishAt,
                div.DueAt,
                CurrentTenant.Id
            ));
        }

        await assignmentRepository.UpdateAsync(assignment, autoSave: true);

        var dto = await MapToAssignmentDtoAsync(assignment, assignment.EducationMediumId ?? Guid.Empty);
        return new AssignmentV2ResponseDto { Data = dto };
    }

    // ─────────────────────────────────────────────────────────────────────
    // DELETE
    // ─────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Delete)]
    public async Task DeleteAsync(Guid assignmentId)
    {
        var q = await assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);
        var assignment = await asyncExecuter.FirstOrDefaultAsync(q.Where(a => a.Id == assignmentId))
            ?? throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", assignmentId);

        await EnsureCurrentUserCanAccessAssignmentAsync(assignment, "delete");
        if (assignment.Status != AssignmentStatus.Draft || assignment.HasPublishedDivisions)
        {
            throw new BusinessException("ShikshaMitra.Assignment:CannotDeletePublished")
                .WithData("AssignmentId", assignmentId)
                .WithData("Status", assignment.Status);
        }

        var submissionsCount = await studentSubmissionRepository.CountAsync(s => s.AssignmentId == assignmentId);
        if (submissionsCount > 0)
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.AssignmentIsInUse)
                .WithData("message", $"Cannot delete assignment '{assignment.Title}' because it is in use: {submissionsCount} student submission(s). Please remove these submissions first.");
        }

        await assignmentRepository.DeleteAsync(assignment, autoSave: true);
    }

    // ─────────────────────────────────────────────────────────────────────
    // CHANGE STATUS
    // ─────────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add)]
    public async Task<AssignmentStatusUpdateResponseDto> ChangeStatusAsync(
        Guid assignmentId,
        AssignmentStatusUpdateDto input
    )
    {
        var q = await assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);
        var assignment = await asyncExecuter.FirstOrDefaultAsync(q.Where(a => a.Id == assignmentId))
            ?? throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", assignmentId);

        await EnsureCurrentUserCanAccessAssignmentAsync(assignment, "change-status");
        var previousStatus = assignment.Status;
        var newStatus = (AssignmentStatus) input.Status;
        if (newStatus == AssignmentStatus.Published)
        {
            await ValidateExistingAssignmentScheduleDatesAsync(assignment);
        }
        else if (newStatus == AssignmentStatus.Closed)
        {
            await EnsureAssignmentCanCloseAsync(assignment.Id);
        }

        assignment.ChangeStatus(newStatus);

        if (newStatus == AssignmentStatus.Published)
        {
            assignment.PublishAll();
        }

        await assignmentRepository.UpdateAsync(assignment, autoSave: true);

        var updaterName = string.Empty;
        if (CurrentUser.Id.HasValue)
        {
            var user = await userRepository.FindAsync(CurrentUser.Id.Value);
            updaterName = user != null ? $"{user.Name} {user.Surname}".Trim() : string.Empty;
        }

        return new AssignmentStatusUpdateResponseDto
        {
            Id = assignment.Id,
            PreviousStatus = (int) previousStatus,
            CurrentStatus = (int) assignment.Status,
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
            throw new BusinessException("ShikshaMitra.Assignment:InvalidAcademicMedium");

        var asOfDate = (input.AsOfDate ?? DateTime.UtcNow).Date;
        var query = await assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);
        query = ApplyAssignmentScope(query, await GetCurrentScopeAsync());
        query = query.Where(a => a.EducationMediumId == academicMediumId);

        if (input.StandardId.HasValue) query = query.Where(a => a.StandardId == input.StandardId.Value);
        if (input.SubjectId.HasValue) query = query.Where(a => a.SubjectId == input.SubjectId.Value);
        if (input.DivisionId.HasValue) query = query.Where(a => a.DivisionSchedules.Any(s => s.DivisionId == input.DivisionId.Value));

        var assignments = await asyncExecuter.ToListAsync(query);
        var rows = await BuildAssignmentDashboardRowsAsync(assignments);

        var dashboard = new LearningTaskDashboardDto
        {
            TotalTasks = rows.Count,
            PendingPublish = rows
                .Where(r => r.Status == (int) AssignmentStatus.Draft || r.PublishedDivisionCount < r.TotalDivisionCount)
                .OrderBy(r => r.NextPublishAt ?? DateTime.MaxValue)
                .Take(20)
                .ToList(),
            DueToday = rows
                .Where(r => r.NextDueAt.HasValue && r.NextDueAt.Value.Date == asOfDate)
                .OrderBy(r => r.NextDueAt)
                .Take(20)
                .ToList(),
            Overdue = rows
                .Where(r => r.NextDueAt.HasValue && r.NextDueAt.Value.Date < asOfDate && r.Status != (int) AssignmentStatus.Closed)
                .OrderBy(r => r.NextDueAt)
                .Take(20)
                .ToList(),
            EvaluationPending = rows
                .Where(r => r.EvaluationPendingCount > 0)
                .OrderByDescending(r => r.EvaluationPendingCount)
                .Take(20)
                .ToList()
        };

        dashboard.PendingPublishCount = rows.Count(r => r.Status == (int) AssignmentStatus.Draft || r.PublishedDivisionCount < r.TotalDivisionCount);
        dashboard.DueTodayCount = rows.Count(r => r.NextDueAt.HasValue && r.NextDueAt.Value.Date == asOfDate);
        dashboard.OverdueCount = rows.Count(r => r.NextDueAt.HasValue && r.NextDueAt.Value.Date < asOfDate && r.Status != (int) AssignmentStatus.Closed);
        dashboard.EvaluationPendingCount = rows.Count(r => r.EvaluationPendingCount > 0);
        return dashboard;
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add)]
    public async Task<LearningTaskReminderResponseDto> SendReminderAsync(
        Guid assignmentId,
        LearningTaskReminderRequestDto input
    )
    {
        var query = await assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);
        var assignment = await asyncExecuter.FirstOrDefaultAsync(query.Where(a => a.Id == assignmentId))
            ?? throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", assignmentId);

        await EnsureCurrentUserCanAccessAssignmentAsync(assignment, "send-reminder");
        var targetSchedules = assignment.DivisionSchedules
            .Where(s => s.IsPublished)
            .Where(s => !input.DivisionId.HasValue || s.DivisionId == input.DivisionId.Value)
            .ToList();
        if (targetSchedules.Count == 0)
        {
            throw new BusinessException("ShikshaMitra.Assignment:ReminderRequiresPublishedDivision")
                .WithData("AssignmentId", assignmentId);
        }

        var targetDivisionIds = targetSchedules.Select(s => s.DivisionId).Distinct().ToList();
        var submissions = await studentSubmissionRepository.GetListAsync(s =>
            s.AssignmentId == assignmentId &&
            targetDivisionIds.Contains(s.DivisionId));
        var pendingSubmissionCount = submissions.Count(s =>
            s.Status is SubmissionStatus.NotSubmitted or SubmissionStatus.Draft or SubmissionStatus.Submitted or SubmissionStatus.Late or SubmissionStatus.NeedsReview);

        await assignmentRepository.UpdateAsync(assignment, autoSave: true);

        return new LearningTaskReminderResponseDto
        {
            TaskId = assignment.Id,
            TaskType = "assignment",
            TargetDivisionCount = targetDivisionIds.Count,
            PendingSubmissionCount = pendingSubmissionCount,
            ReminderQueuedAt = DateTime.UtcNow,
            Message = string.IsNullOrWhiteSpace(input.Message)
                ? "Assignment reminder queued for published target divisions."
                : input.Message.Trim()
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View)]
    public async Task<LearningTaskHistoryDto> GetHistoryAsync(Guid assignmentId)
    {
        var query = await assignmentRepository.WithDetailsAsync(a => a.DivisionSchedules);
        var assignment = await asyncExecuter.FirstOrDefaultAsync(query.Where(a => a.Id == assignmentId))
            ?? throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", assignmentId);

        await EnsureCurrentUserCanAccessAssignmentAsync(assignment, "get-history");
        var items = new List<LearningTaskHistoryItemDto>
        {
            new()
            {
                Action = "Created",
                Summary = $"Assignment created in {assignment.Status} status.",
                OccurredAt = assignment.CreationTime,
                ActorUserId = assignment.CreatorId,
                Severity = "info"
            }
        };

        if (assignment.DivisionSchedules.Any(s => s.PublishedAt.HasValue))
        {
            items.Add(new()
            {
                Action = "Published",
                Summary = $"{assignment.DivisionSchedules.Count(s => s.IsPublished)} division schedule(s) published.",
                OccurredAt = assignment.DivisionSchedules.Where(s => s.PublishedAt.HasValue).Min(s => s.PublishedAt!.Value),
                ActorUserId = assignment.LastModifierId ?? assignment.CreatorId,
                Severity = "success"
            });
        }

        if (assignment.LastModificationTime.HasValue)
        {
            items.Add(new()
            {
                Action = "Updated",
                Summary = $"Assignment last updated while status was {assignment.Status}.",
                OccurredAt = assignment.LastModificationTime.Value,
                ActorUserId = assignment.LastModifierId,
                Severity = "info"
            });
        }

        if (assignment.Status == AssignmentStatus.Closed)
        {
            items.Add(new()
            {
                Action = "Closed",
                Summary = "Assignment closed after pending-submission validation.",
                OccurredAt = assignment.LastModificationTime ?? DateTimeOffset.UtcNow,
                ActorUserId = assignment.LastModifierId,
                Severity = "success"
            });
        }

        return new LearningTaskHistoryDto
        {
            TaskId = assignment.Id,
            TaskType = "assignment",
            Items = items.OrderByDescending(i => i.OccurredAt).ToList()
        };
    }

    private async Task<UserScopeContext> GetCurrentScopeAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.AccessDenied);
        }

        return await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
    }

    private static IQueryable<AssignmentEntity> ApplyAssignmentScope(
        IQueryable<AssignmentEntity> query,
        UserScopeContext scope)
    {
        if (scope.IsAdmin || scope.HasTenantWideScope())
        {
            return query;
        }

        if (!scope.HasAnyAccess)
        {
            return query.Where(_ => false);
        }

        return query.Where(assignment =>
            assignment.AcademicYearId.HasValue &&
            assignment.EducationBoardId.HasValue &&
            assignment.EducationMediumId.HasValue &&
            assignment.EducationMediumStageId.HasValue &&
            (scope.AcademicYearIds.Contains(assignment.AcademicYearId.Value) ||
             scope.WildcardBoardAcademicYearIds.Contains(assignment.AcademicYearId.Value)) &&
            (scope.WildcardBoardAcademicYearIds.Contains(assignment.AcademicYearId.Value) ||
             scope.WildcardMediumBoardIds.Contains(assignment.EducationBoardId.Value) ||
             scope.WildcardMediumStageMediumIds.Contains(assignment.EducationMediumId.Value) ||
             scope.WildcardStandardMediumStageIds.Contains(assignment.EducationMediumStageId.Value) ||
             scope.WildcardDivisionStandardIds.Contains(assignment.StandardId) ||
             assignment.DivisionSchedules.All(schedule => scope.DivisionIds.Contains(schedule.DivisionId))));
    }

    private async Task EnsureCurrentUserCanAccessTargetAsync(
        AssignmentHierarchy hierarchy,
        Guid standardId,
        IEnumerable<Guid> divisionIds)
    {
        var scope = await GetCurrentScopeAsync();
        if (scope.IsAdmin || scope.HasTenantWideScope())
        {
            return;
        }

        var accessible = AssignmentScopePolicy.CanAccessAggregate(
            scope,
            hierarchy.AcademicYearId,
            hierarchy.BoardId,
            hierarchy.MediumId,
            hierarchy.MediumStageId,
            standardId,
            divisionIds);

        if (!accessible)
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.AccessDenied)
                .WithData("Operation", "assignment-target");
        }
    }

    private async Task EnsureCurrentUserCanAccessAssignmentAsync(
        AssignmentEntity assignment,
        string operation)
    {
        var scope = await GetCurrentScopeAsync();
        if (scope.IsAdmin || scope.HasTenantWideScope())
        {
            return;
        }

        if (!AssignmentScopePolicy.CanAccessAggregate(scope, assignment))
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.AccessDenied)
                .WithData("AssignmentId", assignment.Id)
                .WithData("Operation", operation);
        }
    }

    private async Task<List<LearningTaskDashboardItemDto>> BuildAssignmentDashboardRowsAsync(List<AssignmentEntity> assignments)
    {
        var standardIds = assignments.Select(a => a.StandardId).Distinct().ToList();
        var subjectIds = assignments.Select(a => a.SubjectId).Distinct().ToList();
        var divisionIds = assignments.SelectMany(a => a.DivisionSchedules.Select(s => s.DivisionId)).Distinct().ToList();
        var assignmentIds = assignments.Select(a => a.Id).Distinct().ToList();

        var standards = standardIds.Count == 0 ? [] : await standardRepository.GetListAsync(s => standardIds.Contains(s.Id));
        var subjects = subjectIds.Count == 0 ? [] : await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
        var divisions = divisionIds.Count == 0 ? [] : await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id));
        var submissions = assignmentIds.Count == 0
            ? []
            : await studentSubmissionRepository.GetListAsync(s => s.AssignmentId.HasValue && assignmentIds.Contains(s.AssignmentId.Value));

        var standardLookup = standards.ToDictionary(s => s.Id, s => s.Name);
        var subjectLookup = subjects.ToDictionary(s => s.Id, s => s.Name);
        var divisionLookup = divisions.ToDictionary(d => d.Id, d => d.Name);
        var submissionsByAssignment = submissions
            .GroupBy(s => s.AssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return assignments.Select(assignment =>
        {
            submissionsByAssignment.TryGetValue(assignment.Id, out var taskSubmissions);
            taskSubmissions ??= [];
            var schedules = assignment.DivisionSchedules.ToList();
            var dueAt = schedules
                .Where(s => s.IsPublished)
                .Select(s => (DateTime?) s.DueDate)
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
                Id = assignment.Id,
                TaskType = "assignment",
                Title = assignment.Title,
                StandardId = assignment.StandardId,
                StandardName = standardLookup.GetValueOrDefault(assignment.StandardId, string.Empty),
                SubjectId = assignment.SubjectId,
                SubjectName = subjectLookup.GetValueOrDefault(assignment.SubjectId, string.Empty),
                Status = (int) assignment.Status,
                StatusLabel = assignment.Status.ToString(),
                NextPublishAt = publishAt,
                NextDueAt = dueAt,
                PublishedDivisionCount = schedules.Count(s => s.IsPublished),
                TotalDivisionCount = schedules.Count,
                SubmittedCount = taskSubmissions.Count(s => s.Status is SubmissionStatus.Submitted or SubmissionStatus.Late or SubmissionStatus.Reviewed),
                LateCount = taskSubmissions.Count(s => s.Status == SubmissionStatus.Late),
                EvaluationPendingCount = IsEvaluationRequired(assignment)
                    ? taskSubmissions.Count(s => s.Status is SubmissionStatus.Submitted or SubmissionStatus.Late or SubmissionStatus.NeedsReview)
                    : 0,
                DivisionIds = schedules.Select(s => s.DivisionId).Distinct().ToList(),
                DivisionNames = schedules.Select(s => divisionLookup.GetValueOrDefault(s.DivisionId, string.Empty)).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList()
            };
        }).ToList();
    }

    private static bool IsEvaluationRequired(AssignmentEntity assignment) =>
        AssignmentEvaluationResolver.Resolve(assignment, assignment.AssignmentType).IsRequired;

    private async Task ValidateAssignmentScheduleDatesAsync(
        Guid academicYearId,
        Guid standardId,
        IEnumerable<CreateAssignmentV2DivisionScheduleDto> schedules)
    {
        var rows = schedules.ToList();
        await assignmentManager.ValidateAssignmentAsync(
            academicYearId,
            standardId,
            rows.Select(row => (row.DivisionId, row.PublishAt, row.DueAt)).ToList());

        foreach (var row in rows)
        {
            await EnsureAssignmentDueDateAllowedAsync(
                academicYearId,
                standardId,
                row.DivisionId,
                row.DueAt);
        }
    }

    private async Task ValidateExistingAssignmentScheduleDatesAsync(AssignmentEntity assignment)
    {
        foreach (var row in assignment.DivisionSchedules)
        {
            if (!assignment.AcademicYearId.HasValue)
            {
                continue;
            }

            await EnsureAssignmentDueDateAllowedAsync(
                assignment.AcademicYearId.Value,
                assignment.StandardId,
                row.DivisionId,
                row.DueDate);
        }
    }

    private async Task EnsureAssignmentCanCloseAsync(Guid assignmentId)
    {
        var assignment = await assignmentRepository.GetAsync(assignmentId);
        if (assignment.SubmissionType == SubmissionType.NoSubmissionRequired ||
            !IsEvaluationRequired(assignment))
        {
            return;
        }

        var pendingCount = await studentSubmissionRepository.CountAsync(s =>
            s.AssignmentId == assignmentId &&
            (s.Status == SubmissionStatus.Submitted ||
             s.Status == SubmissionStatus.Late ||
             s.Status == SubmissionStatus.NeedsReview ||
             s.Status == SubmissionStatus.Draft));

        if (pendingCount > 0)
        {
            throw new BusinessException("ShikshaMitra.Assignment:CannotCloseWithPendingSubmissions")
                .WithData("AssignmentId", assignmentId)
                .WithData("PendingSubmissions", pendingCount);
        }
    }

    private async Task EnsureAssignmentDueDateAllowedAsync(
        Guid academicYearId,
        Guid standardId,
        Guid divisionId,
        DateTime dueDate)
    {
        var calendars = await calendarRepository.GetListAsync(calendar =>
            calendar.AcademicYearId == academicYearId &&
            calendar.Status == CalendarStatus.Published);
        var applicableCalendar = calendars
            .OrderByDescending(calendar => calendar.IsDefault)
            .ThenByDescending(calendar => calendar.PublishedAt ?? DateTime.MinValue)
            .ThenBy(calendar => calendar.Id)
            .FirstOrDefault();
        if (applicableCalendar == null)
        {
            return;
        }

        var date = dueDate.Date;
        var candidateDays = await calendarDayRepository.GetListAsync(day =>
            day.AcademicCalendarId == applicableCalendar.Id &&
            day.Status == CalendarDayStatus.Active &&
            day.Date.Date == date);
        var blockedDays = candidateDays.Where(day =>
            AssignmentCalendarPolicy.AppliesTo(day, standardId, divisionId));

        var blockingDay = blockedDays.FirstOrDefault(day => CalendarDayBlockingRules.BlocksHomeworkDueDate(day.DayType));
        if (blockingDay != null)
        {
            throw new BusinessException("ShikshaMitra.Assignment:DueDateBlockedByCalendar")
                .WithData("DueDate", date.ToString("yyyy-MM-dd"))
                .WithData("CalendarDay", blockingDay.Title)
                .WithData("DayType", blockingDay.DayType);
        }
    }

    private async Task<AssignmentHierarchy> ValidateAndResolveHierarchyAsync(
        Guid academicMediumId,
        CreateAssignmentV2Dto input)
    {
        var medium = await mediumRepository.FindAsync(academicMediumId)
            ?? throw new BusinessException(HomeworkAssignmentErrorCodes.AcademicMediumNotFound)
                .WithData("AcademicMediumId", academicMediumId);
        var board = await boardRepository.FindAsync(medium.EducationBoardId)
            ?? throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidAcademicHierarchy);
        var academicYearId = input.AcademicYearId ?? board.AcademicYearId;
        if (academicYearId != board.AcademicYearId ||
            await academicYearRepository.FindAsync(academicYearId) == null)
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidAcademicHierarchy)
                .WithData("AcademicYearId", academicYearId);
        }

        var standard = await standardRepository.FindAsync(input.StandardId)
            ?? throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidAcademicHierarchy)
                .WithData("StandardId", input.StandardId);
        var stage = await mediumStageRepository.FindAsync(standard.EducationMediumStageId);
        if (stage == null || stage.EducationMediumId != academicMediumId)
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidAcademicHierarchy)
                .WithData("StandardId", input.StandardId)
                .WithData("AcademicMediumId", academicMediumId);
        }

        var subject = await subjectRepository.FindAsync(input.SubjectId);
        if (subject == null || subject.EducationMediumId != academicMediumId)
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidAcademicHierarchy)
                .WithData("SubjectId", input.SubjectId)
                .WithData("AcademicMediumId", academicMediumId);
        }

        var assignedSubject = await assignedSubjectRepository.FindAsync(row =>
            row.StandardId == input.StandardId && row.SubjectId == input.SubjectId);
        if (assignedSubject == null)
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidAcademicHierarchy)
                .WithData("StandardId", input.StandardId)
                .WithData("SubjectId", input.SubjectId);
        }

        var chapterIds = input.ChapterIds?.Distinct().ToList() ?? [];
        if (chapterIds.Count > 0)
        {
            var validChapterCount = await chapterRepository.CountAsync(chapter =>
                chapterIds.Contains(chapter.Id) && chapter.AssignedSubjectId == assignedSubject.Id);
            if (validChapterCount != chapterIds.Count)
            {
                throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidAcademicHierarchy)
                    .WithData("ChapterIds", string.Join(",", chapterIds));
            }
        }

        return new AssignmentHierarchy(
            academicYearId,
            board.Id,
            medium.Id,
            stage.Id);
    }

    private static EffectiveAssignmentSettings ResolveEffectiveSettings(
        CreateAssignmentV2Dto input,
        AssignmentType assignmentType)
    {
        var submissionType = input.SubmissionType.HasValue
            ? (SubmissionType) input.SubmissionType.Value
            : assignmentType.DefaultSubmissionType ?? SubmissionType.OnlineText;
        if (!Enum.IsDefined(submissionType))
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidSubmissionType);
        }

        var evaluationRequired = input.EvaluationRequired ?? assignmentType.EvaluationRequired;
        var evaluationMethod = input.EvaluationMethod.HasValue
            ? (EvaluationMethod?) input.EvaluationMethod.Value
            : assignmentType.DefaultEvaluationMethod;
        if (!evaluationRequired)
        {
            evaluationMethod = EvaluationMethod.NoEvaluation;
        }
        else if (!evaluationMethod.HasValue || evaluationMethod == EvaluationMethod.NoEvaluation)
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.EvaluationMethodRequired);
        }
        else if (!Enum.IsDefined(evaluationMethod.Value))
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidEvaluationMethod);
        }
        else if (evaluationMethod is EvaluationMethod.Grade or EvaluationMethod.Checklist)
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.UnsupportedEvaluationPersistence)
                .WithData("EvaluationMethod", evaluationMethod);
        }

        if (evaluationMethod != EvaluationMethod.Marks && input.TotalMarks.HasValue)
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.IncompatibleEvaluationMarks)
                .WithData("EvaluationMethod", evaluationMethod);
        }

        var totalMarks = input.TotalMarks ?? assignmentType.DefaultMarks ?? 0;
        if (totalMarks < 0 ||
            (evaluationRequired && evaluationMethod == EvaluationMethod.Marks && totalMarks <= 0) ||
            (!evaluationRequired && input.TotalMarks.HasValue))
        {
            throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidDefaultMarks);
        }

        if (!evaluationRequired || evaluationMethod != EvaluationMethod.Marks)
        {
            totalMarks = 0;
        }

        return new EffectiveAssignmentSettings(
            submissionType,
            totalMarks,
            evaluationRequired,
            evaluationMethod);
    }

    private sealed record AssignmentHierarchy(
        Guid AcademicYearId,
        Guid BoardId,
        Guid MediumId,
        Guid MediumStageId);

    private sealed record EffectiveAssignmentSettings(
        SubmissionType SubmissionType,
        decimal TotalMarks,
        bool EvaluationRequired,
        EvaluationMethod? EvaluationMethod);

    private async Task<AssignmentV2Dto> MapToAssignmentDtoAsync(AssignmentEntity a, Guid academicMediumId)
    {
        var standard = await standardRepository.FindAsync(a.StandardId);
        var subject = await subjectRepository.FindAsync(a.SubjectId);

        AssignmentType? assignmentType = null;
        if (a.AssignmentType != null)
            assignmentType = a.AssignmentType;
        else
        {
            assignmentType = await assignmentTypeRepository.FindAsync(a.AssignmentTypeId);
        }

        var divisionIds = a.DivisionSchedules.Select(s => s.DivisionId).ToList();
        var divisionLookup = divisionIds.Any()
            ? (await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id)))
                .ToDictionary(d => d.Id, d => d.Name)
            : new Dictionary<Guid, string>();

        var createdBy = string.Empty;
        var updatedBy = string.Empty;
        if (a.CreatorId.HasValue)
        {
            var u = await userRepository.FindAsync(a.CreatorId.Value);
            createdBy = u != null ? $"{u.Name} {u.Surname}".Trim() : string.Empty;
        }
        if (a.LastModifierId.HasValue)
        {
            var u = await userRepository.FindAsync(a.LastModifierId.Value);
            updatedBy = u != null ? $"{u.Name} {u.Surname}".Trim() : string.Empty;
        }

        return BuildAssignmentDto(a, academicMediumId,
            standard?.Name ?? string.Empty,
            subject?.Name ?? string.Empty,
            assignmentType,
            divisionLookup,
            createdBy, updatedBy);
    }

    private static AssignmentV2Dto MapToAssignmentDto(
        AssignmentEntity a,
        Guid academicMediumId,
        Dictionary<Guid, string> standardLookup,
        Dictionary<Guid, string> subjectLookup,
        Dictionary<Guid, AssignmentType> typeLookup,
        Dictionary<Guid, string> divisionLookup,
        Dictionary<Guid, string> userLookup
    ) => BuildAssignmentDto(
        a, academicMediumId,
        standardLookup.GetValueOrDefault(a.StandardId, string.Empty),
        subjectLookup.GetValueOrDefault(a.SubjectId, string.Empty),
        typeLookup.GetValueOrDefault(a.AssignmentTypeId),
        divisionLookup,
        a.CreatorId.HasValue ? userLookup.GetValueOrDefault(a.CreatorId.Value, string.Empty) : string.Empty,
        a.LastModifierId.HasValue ? userLookup.GetValueOrDefault(a.LastModifierId.Value, string.Empty) : string.Empty
    );

    private static List<Guid> DeserializeChapterIds(string? chapterIdsJson)
    {
        if (string.IsNullOrWhiteSpace(chapterIdsJson))
        {
            return [];
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(chapterIdsJson) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private static AssignmentV2Dto BuildAssignmentDto(
        AssignmentEntity a,
        Guid academicMediumId,
        string standardName,
        string subjectName,
        AssignmentType? assignmentType,
        Dictionary<Guid, string> divisionLookup,
        string createdBy,
        string updatedBy
    )
    {
        var evaluation = AssignmentEvaluationResolver.Resolve(a, assignmentType);
        return new AssignmentV2Dto
    {
        Id = a.Id,
        AcademicYearId = a.AcademicYearId,
        AcademicMediumId = academicMediumId,
        StandardId = a.StandardId,
        StandardName = standardName,
        SubjectId = a.SubjectId,
        SubjectName = subjectName,
        Title = a.Title,
        AssignmentTypeId = a.AssignmentTypeId,
        AssignmentTypeName = assignmentType?.Name ?? string.Empty,
        DefaultSubmissionType = assignmentType?.DefaultSubmissionType.HasValue == true ? (int) assignmentType.DefaultSubmissionType.Value : null,
        DefaultMarks = assignmentType?.DefaultMarks,
        DefaultDueDays = assignmentType?.DefaultDueDays,
        EvaluationRequired = evaluation.IsRequired,
        EvaluationMethod = (int) evaluation.Method,
        DefaultEvaluationMethod = assignmentType?.DefaultEvaluationMethod.HasValue == true ? (int) assignmentType.DefaultEvaluationMethod.Value : null,
        AssignmentTypeStatus = assignmentType != null ? (int) assignmentType.Status : null,
        TotalMarks = a.TotalMarks,
        Description = a.Description,
        Attachment = AssignmentAttachmentSerializer.ReadSingle(a.AttachmentsJson),
        ChapterIds = DeserializeChapterIds(a.ChapterIdsJson),
        SubmissionType = (int) a.SubmissionType,
        Status = (int) a.Status,
        DivisionSchedules = a.DivisionSchedules
            .Select(s => new AssignmentV2DivisionScheduleDto
            {
                DivisionId = s.DivisionId,
                DivisionName = divisionLookup.GetValueOrDefault(s.DivisionId, string.Empty),
                PublishAt = s.PublishDate,
                DueAt = s.DueDate,
                IsPublished = s.IsPublished,
            })
            .ToList(),
        CreatedAt = a.CreationTime,
        CreatedBy = createdBy,
        UpdatedAt = a.LastModificationTime ?? a.CreationTime,
        UpdatedBy = updatedBy,
    };
    }
}
