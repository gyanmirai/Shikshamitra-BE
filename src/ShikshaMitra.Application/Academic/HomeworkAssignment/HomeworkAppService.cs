using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Constants.Permissions;
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
    IRepository<AssignedSubjectChapter, Guid> chapterRepository,
    IHomeworkTypeRepository homeworkTypeRepository,
    IRepository<IdentityUser, Guid> userRepository,
    IUserScopeService userScopeService,
    HomeworkManager homeworkManager,
    IAsyncQueryableExecuter asyncExecuter
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

        await homeworkManager.ValidateHomeworkTypeAsync(input.HomeworkTypeId);

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

        // Store single chapterId in the entity's ChapterId field
        homework.ChapterId = input.ChapterId;
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
            .Where(h => h.SubjectId == filter.SubjectId)
            // Only homeworks with a schedule for the requested division
            .Where(h => h.DivisionSchedules.Any(s =>
                s.DivisionId == filter.DivisionId
                && (!filter.HomeworkDate.HasValue || !s.PublishDate.HasValue || s.PublishDate.Value <= filter.HomeworkDate.Value)));

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
        var chapterIds = items.Where(h => h.ChapterId.HasValue)
            .Select(h => h.ChapterId!.Value).Distinct().ToList();
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

        var standardLookup = standards.ToDictionary(s => s.Id, s => s.Name);
        var subjectLookup = subjectLookupByHomeworkId;
        var chapterLookup = chapters.ToDictionary(c => c.Id, c => c.ChapterName);
        var hwTypeLookup = hwTypes.ToDictionary(t => t.Id, t => t.Name);
        var divisionLookup = divisions.ToDictionary(d => d.Id, d => d.Name);
        var userLookup = users.ToDictionary(u => u.Id, u => $"{u.Name} {u.Surname}".Trim());

        var dtos = items
            .Select(h => MapToHomeworkDto(h, academicMediumId, standardLookup, subjectLookup,
                chapterLookup, hwTypeLookup, divisionLookup, userLookup))
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

        if (homework.Status != HomeworkStatus.Draft)
            throw new BusinessException("ShikshaMitra.Homework:CannotUpdatePublished")
                .WithData("HomeworkId", homeworkId);

        await homeworkManager.ValidateHomeworkTypeAsync(input.HomeworkTypeId);

        // Keep existing blob if no new one provided
        var resolvedBlob = attachmentBlobName ?? homework.DocumentUrl;

        homework.UpdateBasicInfo(input.Title, input.Description);
        homework.ChapterId = input.ChapterId;
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
        var homework = await homeworkRepository.FindAsync(homeworkId)
            ?? throw new BusinessException("ShikshaMitra.Homework:NotFound").WithData("Id", homeworkId);

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
        var homework = await homeworkRepository.FindAsync(homeworkId)
            ?? throw new BusinessException("ShikshaMitra.Homework:NotFound").WithData("Id", homeworkId);

        var previousStatus = homework.Status;
        homework.ChangeStatus((HomeworkStatus) input.Status);

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

    private async Task<HomeworkDto> MapToHomeworkDtoAsync(HomeworkAssignmentEntity h, Guid academicMediumId)
    {
        var standardName = string.Empty;
        var subjectName = string.Empty;
        string? chapterName = null;
        var hwTypeName = string.Empty;
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

        if (h.ChapterId.HasValue)
        {
            var chapter = await chapterRepository.FindAsync(h.ChapterId.Value);
            chapterName = chapter?.ChapterName;
        }

        if (h.HomeworkTypeId.HasValue)
        {
            var hwType = await homeworkTypeRepository.FindAsync(h.HomeworkTypeId.Value);
            hwTypeName = hwType?.Name ?? string.Empty;
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

        return BuildHomeworkDto(h, academicMediumId, standardName, subjectName, chapterName,
            hwTypeName, divisionLookup, createdBy, updatedBy);
    }

    private static HomeworkDto MapToHomeworkDto(
        HomeworkAssignmentEntity h,
        Guid academicMediumId,
        Dictionary<Guid, string> standardLookup,
        Dictionary<Guid, string> subjectLookup,
        Dictionary<Guid, string> chapterLookup,
        Dictionary<Guid, string> hwTypeLookup,
        Dictionary<Guid, string> divisionLookup,
        Dictionary<Guid, string> userLookup
    )
    {
        var standardName = standardLookup.GetValueOrDefault(h.StandardId, string.Empty);
        var subjectName = subjectLookup.GetValueOrDefault(h.SubjectId, string.Empty);
        string? chapterName = h.ChapterId.HasValue
            ? chapterLookup.GetValueOrDefault(h.ChapterId.Value)
            : null;
        var hwTypeName = h.HomeworkTypeId.HasValue
            ? hwTypeLookup.GetValueOrDefault(h.HomeworkTypeId.Value, string.Empty)
            : string.Empty;
        var createdBy = h.CreatorId.HasValue
            ? userLookup.GetValueOrDefault(h.CreatorId.Value, string.Empty)
            : string.Empty;
        var updatedBy = h.LastModifierId.HasValue
            ? userLookup.GetValueOrDefault(h.LastModifierId.Value, string.Empty)
            : string.Empty;

        return BuildHomeworkDto(h, academicMediumId, standardName, subjectName, chapterName,
            hwTypeName, divisionLookup, createdBy, updatedBy);
    }

    private static HomeworkDto BuildHomeworkDto(
        HomeworkAssignmentEntity h,
        Guid academicMediumId,
        string standardName,
        string subjectName,
        string? chapterName,
        string hwTypeName,
        Dictionary<Guid, string> divisionLookup,
        string createdBy,
        string updatedBy
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
        Title = h.Title,
        HomeworkTypeId = h.HomeworkTypeId ?? Guid.Empty,
        HomeworkTypeName = hwTypeName,
        Description = h.Description,
        Attachment = h.DocumentUrl,
        SubmissionType = (int) h.SubmissionType,
        Status = (int) h.Status,
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
}
