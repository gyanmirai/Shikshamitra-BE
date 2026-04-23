using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
    IAsyncQueryableExecuter asyncExecuter
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

        await assignmentManager.ValidateAssignmentTypeAsync(input.AssignmentTypeId);

        var assignment = new AssignmentEntity(
            GuidGenerator.Create(),
            input.StandardId,
            input.SubjectId,
            input.Title,
            input.Description,
            input.AssignmentTypeId,
            (SubmissionType) input.SubmissionType,
            input.TotalMarks,
            CurrentTenant.Id,
            academicYearId: null,
            attachmentsJson: attachmentBlobName,
            educationMediumId: academicMediumId
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

        query = query
            .Where(a => a.EducationMediumId == academicMediumId)
            .Where(a => a.StandardId == filter.StandardId)
            .Where(a => a.SubjectId == filter.SubjectId)
            .Where(a => a.DivisionSchedules.Any(s =>
                s.DivisionId == filter.DivisionId
                && (!s.PublishDate.HasValue || s.PublishDate.Value <= filter.AssignmentDate)));

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
        var typeLookup = assignmentTypes.ToDictionary(t => t.Id, t => t.Name);
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

        if (assignment.Status != AssignmentStatus.Draft)
            throw new BusinessException("ShikshaMitra.Assignment:CannotUpdatePublished")
                .WithData("AssignmentId", assignmentId);

        await assignmentManager.ValidateAssignmentTypeAsync(input.AssignmentTypeId);

        var resolvedBlob = attachmentBlobName ?? assignment.AttachmentsJson;

        assignment.UpdateBasicInfo(input.Title, input.Description, input.TotalMarks, resolvedBlob);
        assignment.StandardId = input.StandardId;
        assignment.SubjectId = input.SubjectId;
        assignment.AssignmentTypeId = input.AssignmentTypeId;
        assignment.SubmissionType = (SubmissionType) input.SubmissionType;

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
        var assignment = await assignmentRepository.FindAsync(assignmentId)
            ?? throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", assignmentId);

        if (assignment.Status != AssignmentStatus.Draft)
            throw new BusinessException("ShikshaMitra.Assignment:CannotDeletePublished")
                .WithData("AssignmentId", assignmentId);

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
        var assignment = await assignmentRepository.FindAsync(assignmentId)
            ?? throw new BusinessException("ShikshaMitra.Assignment:NotFound").WithData("Id", assignmentId);

        var previousStatus = assignment.Status;
        assignment.ChangeStatus((AssignmentStatus) input.Status);

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

    private async Task<AssignmentV2Dto> MapToAssignmentDtoAsync(AssignmentEntity a, Guid academicMediumId)
    {
        var standard = await standardRepository.FindAsync(a.StandardId);
        var subject = await subjectRepository.FindAsync(a.SubjectId);

        string assignmentTypeName = string.Empty;
        if (a.AssignmentType != null)
            assignmentTypeName = a.AssignmentType.Name;
        else
        {
            var at = await assignmentTypeRepository.FindAsync(a.AssignmentTypeId);
            assignmentTypeName = at?.Name ?? string.Empty;
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
            assignmentTypeName,
            divisionLookup,
            createdBy, updatedBy);
    }

    private static AssignmentV2Dto MapToAssignmentDto(
        AssignmentEntity a,
        Guid academicMediumId,
        Dictionary<Guid, string> standardLookup,
        Dictionary<Guid, string> subjectLookup,
        Dictionary<Guid, string> typeLookup,
        Dictionary<Guid, string> divisionLookup,
        Dictionary<Guid, string> userLookup
    ) => BuildAssignmentDto(
        a, academicMediumId,
        standardLookup.GetValueOrDefault(a.StandardId, string.Empty),
        subjectLookup.GetValueOrDefault(a.SubjectId, string.Empty),
        typeLookup.GetValueOrDefault(a.AssignmentTypeId, string.Empty),
        divisionLookup,
        a.CreatorId.HasValue ? userLookup.GetValueOrDefault(a.CreatorId.Value, string.Empty) : string.Empty,
        a.LastModifierId.HasValue ? userLookup.GetValueOrDefault(a.LastModifierId.Value, string.Empty) : string.Empty
    );

    private static AssignmentV2Dto BuildAssignmentDto(
        AssignmentEntity a,
        Guid academicMediumId,
        string standardName,
        string subjectName,
        string assignmentTypeName,
        Dictionary<Guid, string> divisionLookup,
        string createdBy,
        string updatedBy
    ) => new()
    {
        Id = a.Id,
        AcademicMediumId = academicMediumId,
        StandardId = a.StandardId,
        StandardName = standardName,
        SubjectId = a.SubjectId,
        SubjectName = subjectName,
        Title = a.Title,
        AssignmentTypeId = a.AssignmentTypeId,
        AssignmentTypeName = assignmentTypeName,
        TotalMarks = a.TotalMarks,
        Description = a.Description,
        Attachment = a.AttachmentsJson,
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
