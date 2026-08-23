using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Common.Excel;
using ShikshaMitra.Common.Excel.Elements;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.Students;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

[RemoteService(false)]
public class ExamSeatAllocationAppService(
    IRepository<ExamSeatAllocation, Guid> seatAllocationRepository,
    IRepository<ExamSeatingPlan, Guid> seatingPlanRepository,
    IRepository<ExamSeatingPlanSubject, Guid> seatingPlanSubjectRepository,
    IRepository<SeatingPlanAuditLog, Guid> auditLogRepository,
    IRepository<Exam, Guid> examRepository,
    IRepository<Student, Guid> studentRepository,
    IRepository<StudentElectiveSelection, Guid> studentElectiveSelectionRepository,
    IRepository<Room, Guid> roomRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<ExamType, Guid> examTypeRepository,
    IRepository<Subject, Guid> subjectRepository,
    IGuidGenerator guidGenerator,
    IDataFilter dataFilter,
    IPdfService pdfService,
    IExcelService excelService,
    ISeatingPlanNotifier seatingPlanNotifier,
    IUserScopeService userScopeService
) : ApplicationService, IExamSeatAllocationAppService
{
    private async Task<Exam> GetScopedExamAsync(Guid examId)
    {
        var exam = await examRepository.GetAsync(examId, includeDetails: true);
        if (!CurrentUser.Id.HasValue)
        {
            throw new BusinessException(UserScopeErrorCodes.NoCurrentUser);
        }

        var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
        if (CanAccessExam(exam, scope))
        {
            return exam;
        }

        throw new BusinessException(UserScopeErrorCodes.AccessDenied)
            .WithData("ExamId", examId)
            .WithData("Operation", "exam-seat-allocation");
    }

    private static bool CanAccessExam(Exam exam, UserScopeContext scope)
    {
        if (scope.IsAdmin || scope.HasTenantWideScope() ||
            scope.StandardIds.Contains(exam.StandardId) ||
            scope.WildcardDivisionStandardIds.Contains(exam.StandardId))
        {
            return true;
        }

        var examDivisionIds = exam.ExamDivisions.Select(row => row.DivisionId).Distinct().ToList();
        return examDivisionIds.Count > 0 && examDivisionIds.All(scope.DivisionIds.Contains);
    }

    /// <summary>
    /// Single source of truth for total/allocated/pending counts for one exam — used by both
    /// the dashboard list (<see cref="GetSeatAllocationTableAsync"/>) and the seating-plan draft
    /// endpoint, so the two screens can never disagree (master spec §5).
    /// </summary>
    private async Task<(int total, int allocated)> ComputeCountsAsync(Exam exam, Guid? seatingPlanId = null)
    {
        var studentsInDivisions = seatingPlanId.HasValue
            ? await GetEligibleStudentsForPlanAsync(exam, await seatingPlanRepository.GetAsync(seatingPlanId.Value))
            : await GetEligibleStudentsForExamAsync(exam);
        var total = studentsInDivisions.Count;

        var allocated = seatingPlanId.HasValue
            ? await seatAllocationRepository.CountAsync(a => a.ExamId == exam.Id && a.SeatingPlanId == seatingPlanId.Value)
            : await seatAllocationRepository.CountAsync(a => a.ExamId == exam.Id);

        return (total, allocated);
    }

    private async Task<List<Student>> GetEligibleStudentsForPlanAsync(Exam exam, ExamSeatingPlan plan)
    {
        var planSubjectIds = (await GetPlanSubjectsAsync(plan.Id)).Select(s => s.ExamSubjectId).ToHashSet();
        var divisionIds = exam.ExamSubjects
            .Where(s => planSubjectIds.Contains(s.Id))
            .SelectMany(s => s.RoomAssignments)
            .Select(a => a.DivisionId)
            .ToHashSet();

        // Preserve compatibility with old drafts that have no subject room metadata.
        if (divisionIds.Count == 0)
            divisionIds = exam.ExamDivisions.Select(d => d.DivisionId).ToHashSet();

        var students = divisionIds.Count == 0
            ? new List<Student>()
            : await studentRepository.GetListAsync(s =>
                s.StandardId == exam.StandardId && s.DivisionId.HasValue && divisionIds.Contains(s.DivisionId.Value));
        return await FilterSubjectWiseStudentsAsync(plan, students);
    }

    private async Task<List<Student>> GetEligibleStudentsForExamAsync(Exam exam)
    {
        var divisionIds = exam.ExamDivisions.Select(d => d.DivisionId).ToHashSet();
        return divisionIds.Count == 0
            ? new List<Student>()
            : await studentRepository.GetListAsync(s =>
                s.StandardId == exam.StandardId && s.DivisionId.HasValue && divisionIds.Contains(s.DivisionId.Value));
    }

    private async Task<List<Student>> FilterSubjectWiseStudentsAsync(
        ExamSeatingPlan plan,
        List<Student> candidates)
    {
        var excludedIds = ParseGuidCsv(plan.ExcludedStudentIdsCsv).ToHashSet();
        candidates = candidates.Where(s => !excludedIds.Contains(s.Id)).ToList();
        var subjectIds = (await GetPlanSubjectsAsync(plan.Id)).Select(s => s.SubjectId).Distinct().ToList();
        if (subjectIds.Count == 0)
            return candidates;

        var candidateIds = candidates.Select(s => s.Id).ToHashSet();
        var selections = await studentElectiveSelectionRepository.GetListAsync(s =>
            subjectIds.Contains(s.SelectedSubjectId) && candidateIds.Contains(s.StudentId));

        // Core subjects have no elective-selection rows. If any selected subject is core,
        // retaining all candidates is safer than excluding valid core-subject students.
        var selectedSubjectIds = selections.Select(s => s.SelectedSubjectId).ToHashSet();
        if (selections.Count == 0 || selectedSubjectIds.Count != subjectIds.Count)
            return candidates;

        var selectedIds = selections.Select(s => s.StudentId).ToHashSet();
        return candidates.Where(s => selectedIds.Contains(s.Id)).ToList();
    }

    private static SeatingPlanStatus ComputeStatus(int total, int allocated, bool isLocked, bool isPublished)
    {
        if (isPublished) return SeatingPlanStatus.Published;
        if (isLocked) return SeatingPlanStatus.Completed;
        if (allocated == 0) return SeatingPlanStatus.NotStarted;
        if (allocated < total) return SeatingPlanStatus.InProgress;
        return SeatingPlanStatus.Ready;
    }

    private async Task<ExamSeatingPlan> EnsureDefaultPlanAsync(Exam exam)
    {
        var plan = await seatingPlanRepository.FindAsync(p =>
            p.ExamId == exam.Id && p.ScopeType == SeatingPlanScopeType.AllSubjects);
        if (plan != null)
        {
            await EnsurePlanSubjectsAsync(exam, plan, exam.ExamSubjects.Select(s => s.Id).ToList());
            return plan;
        }

        plan = new ExamSeatingPlan(
            guidGenerator.Create(),
            CurrentTenant.Id,
            exam.Id,
            "All Subjects",
            SeatingPlanScopeType.AllSubjects);
        await seatingPlanRepository.InsertAsync(plan, autoSave: true);
        await EnsurePlanSubjectsAsync(exam, plan, exam.ExamSubjects.Select(s => s.Id).ToList());
        return plan;
    }

    private async Task<ExamSeatingPlan> ResolvePlanAsync(Exam exam, Guid? seatingPlanId)
    {
        if (!seatingPlanId.HasValue)
            return await EnsureDefaultPlanAsync(exam);

        return await seatingPlanRepository.FindAsync(p => p.ExamId == exam.Id && p.Id == seatingPlanId.Value)
            ?? throw new UserFriendlyException("Seating plan not found for this exam.");
    }

    private async Task EnsurePlanSubjectsAsync(Exam exam, ExamSeatingPlan plan, List<Guid> examSubjectIds)
    {
        var validById = exam.ExamSubjects.ToDictionary(s => s.Id);
        var normalizedIds = examSubjectIds.Where(validById.ContainsKey).Distinct().ToList();
        if (normalizedIds.Count == 0)
            throw new UserFriendlyException("Select at least one subject for the seating plan.");

        var existing = await seatingPlanSubjectRepository.GetListAsync(s => s.SeatingPlanId == plan.Id);
        var incoming = normalizedIds.ToHashSet();
        var toDelete = existing.Where(s => !incoming.Contains(s.ExamSubjectId)).ToList();
        var existingIds = existing.Select(s => s.ExamSubjectId).ToHashSet();
        var toInsert = normalizedIds
            .Where(id => !existingIds.Contains(id))
            .Select(id =>
            {
                var examSubject = validById[id];
                return new ExamSeatingPlanSubject(
                    guidGenerator.Create(),
                    CurrentTenant.Id,
                    plan.Id,
                    exam.Id,
                    examSubject.Id,
                    examSubject.SubjectId);
            })
            .ToList();

        if (toDelete.Count > 0)
            await seatingPlanSubjectRepository.DeleteManyAsync(toDelete, autoSave: true);
        if (toInsert.Count > 0)
            await seatingPlanSubjectRepository.InsertManyAsync(toInsert, autoSave: true);
    }

    private async Task<List<ExamSeatingPlanSubject>> GetPlanSubjectsAsync(Guid seatingPlanId) =>
        await seatingPlanSubjectRepository.GetListAsync(s => s.SeatingPlanId == seatingPlanId);

    private static List<Guid> NormalizeExamSubjectIds(
        Exam exam,
        SeatingPlanScopeType scopeType,
        IReadOnlyCollection<Guid> examSubjectIds)
    {
        var validIds = exam.ExamSubjects.Select(s => s.Id).ToHashSet();
        var normalizedIds = scopeType == SeatingPlanScopeType.AllSubjects
            ? validIds.ToList()
            : examSubjectIds.Distinct().ToList();

        if (normalizedIds.Count == 0)
            throw new UserFriendlyException("Select at least one subject for the seating plan.");

        var invalidIds = normalizedIds.Where(id => !validIds.Contains(id)).ToList();
        if (invalidIds.Count > 0)
            throw new UserFriendlyException("One or more selected subjects do not belong to this exam.");

        if (scopeType == SeatingPlanScopeType.SingleSubject && normalizedIds.Count != 1)
            throw new UserFriendlyException("A subject-wise seating plan must contain exactly one subject.");

        return normalizedIds;
    }

    private static bool HasSubjectSetChanged(
        IReadOnlyCollection<ExamSeatingPlanSubject> currentSubjects,
        IReadOnlyCollection<Guid> nextExamSubjectIds) =>
        !currentSubjects.Select(s => s.ExamSubjectId).ToHashSet().SetEquals(nextExamSubjectIds);

    private async Task EnsureSubjectWiseSubjectsAreAvailableAsync(
        Guid examId,
        Guid? currentPlanId,
        SeatingPlanScopeType scopeType,
        IReadOnlyCollection<Guid> examSubjectIds)
    {
        if (scopeType == SeatingPlanScopeType.AllSubjects)
            return;

        var requested = examSubjectIds.ToHashSet();
        var otherPlans = await seatingPlanRepository.GetListAsync(p =>
            p.ExamId == examId &&
            (!currentPlanId.HasValue || p.Id != currentPlanId.Value) &&
            p.ScopeType != SeatingPlanScopeType.AllSubjects);
        if (otherPlans.Count == 0)
            return;

        var otherPlanIds = otherPlans.Select(p => p.Id).ToHashSet();
        var otherSubjects = await seatingPlanSubjectRepository.GetListAsync(s =>
            s.ExamId == examId && otherPlanIds.Contains(s.SeatingPlanId));
        if (otherSubjects.Any(s => requested.Contains(s.ExamSubjectId)))
            throw new UserFriendlyException("One or more selected subjects already belong to another subject-wise seating plan.");
    }

    private async Task<ExamSeatingPlan?> FindMatchingPlanAsync(
        Guid examId,
        SeatingPlanScopeType scopeType,
        IReadOnlyCollection<Guid> examSubjectIds)
    {
        var requestedIds = examSubjectIds.ToHashSet();
        var plans = await seatingPlanRepository.GetListAsync(p => p.ExamId == examId && p.ScopeType == scopeType);
        foreach (var candidate in plans)
        {
            var candidateSubjects = await GetPlanSubjectsAsync(candidate.Id);
            if (candidateSubjects.Select(s => s.ExamSubjectId).ToHashSet().SetEquals(requestedIds))
                return candidate;
        }

        return null;
    }

    public async Task<SeatingPlanDraftResponseDto> GetSeatingPlanDraftAsync(Guid examId, Guid? seatingPlanId = null)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, seatingPlanId);
        var planSubjects = await GetPlanSubjectsAsync(plan.Id);

        var (total, allocated) = await ComputeCountsAsync(exam, plan.Id);
        var isLocked = plan.IsLocked;
        var isPublished = plan.Status == SeatingPlanStatus.Published;
        var status = ComputeStatus(total, allocated, isLocked, isPublished);

        return new SeatingPlanDraftResponseDto
        {
            SeatingPlanId = plan.Id,
            ExamId = examId,
            Name = plan.Name,
            ScopeType = plan.ScopeType,
            ExamSubjectIds = planSubjects.Select(s => s.ExamSubjectId).ToList(),
            SubjectIds = planSubjects.Select(s => s.SubjectId).ToList(),
            RoomIds = ParseGuidCsv(plan.RoomIdsCsv),
            AllocationRulesJson = plan.AllocationRulesJson,
            ExcludedStudentIds = ParseGuidCsv(plan.ExcludedStudentIdsCsv),
            SpecialRequirementStudentIds = ParseGuidCsv(plan.SpecialRequirementStudentIdsCsv),
            TotalStudents = total,
            AllocatedStudents = allocated,
            PendingStudents = Math.Max(0, total - allocated),
            AllocationProgress = total == 0 ? 0 : (int) Math.Round(allocated / (double) total * 100),
            Status = status,
            IsLocked = isLocked,
            LastModifiedTime = plan.LastModificationTime
        };
    }

    public async Task<SeatingPlanDraftResponseDto> SaveSeatingPlanDraftAsync(Guid examId, SaveSeatingPlanDraftInputDto input)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);

        if (input.LastKnownModifiedTime.HasValue &&
            plan.LastModificationTime.HasValue &&
            plan.LastModificationTime.Value != input.LastKnownModifiedTime.Value)
        {
            throw new UserFriendlyException(
                "This seating plan was modified by someone else since you last loaded it. Refresh to see the latest changes before saving.")
            {
                Code = "SEATING_PLAN_CONCURRENCY_CONFLICT"
            };
        }

        if (plan.IsLocked)
            throw new UserFriendlyException("Unlock this seating plan before modifying it.");

        if (!string.IsNullOrWhiteSpace(input.Name) || input.ScopeType.HasValue)
        {
            var scopeName = string.IsNullOrWhiteSpace(input.Name) ? plan.Name : input.Name!;
            var scopeType = input.ScopeType ?? plan.ScopeType;
            var nextExamSubjectIds = input.ExamSubjectIds is { Count: > 0 }
                ? NormalizeExamSubjectIds(exam, scopeType, input.ExamSubjectIds)
                : (await GetPlanSubjectsAsync(plan.Id)).Select(s => s.ExamSubjectId).ToList();
            var currentSubjects = await GetPlanSubjectsAsync(plan.Id);
            var allocatedCount = await seatAllocationRepository.CountAsync(a => a.ExamId == examId && a.SeatingPlanId == plan.Id);
            if ((plan.ScopeType != scopeType || HasSubjectSetChanged(currentSubjects, nextExamSubjectIds)) && allocatedCount > 0)
                throw new UserFriendlyException("Remove seat allocations before changing subjects for this seating plan.");

            await EnsureSubjectWiseSubjectsAreAvailableAsync(examId, plan.Id, scopeType, nextExamSubjectIds);
            plan.UpdateScope(scopeName, scopeType);
        }
        if (input.ExamSubjectIds is { Count: > 0 })
        {
            var scopeType = input.ScopeType ?? plan.ScopeType;
            var nextExamSubjectIds = NormalizeExamSubjectIds(exam, scopeType, input.ExamSubjectIds);
            var currentSubjects = await GetPlanSubjectsAsync(plan.Id);
            var allocatedCount = await seatAllocationRepository.CountAsync(a => a.ExamId == examId && a.SeatingPlanId == plan.Id);
            if (HasSubjectSetChanged(currentSubjects, nextExamSubjectIds) && allocatedCount > 0)
                throw new UserFriendlyException("Remove seat allocations before changing subjects for this seating plan.");

            await EnsureSubjectWiseSubjectsAreAvailableAsync(examId, plan.Id, scopeType, nextExamSubjectIds);
            await EnsurePlanSubjectsAsync(exam, plan, nextExamSubjectIds);
        }

        // Validate rooms against the effective subject set, not the plan's old subject set.
        // This matters when a subject-wise plan is switched from one subject to another.
        var effectiveSubjectIdsForRooms = (await GetPlanSubjectsAsync(plan.Id))
            .Select(s => s.ExamSubjectId)
            .ToHashSet();
        var allowedRoomIdsForPlan = exam.ExamSubjects
            .Where(s => effectiveSubjectIdsForRooms.Contains(s.Id))
            .SelectMany(s => s.RoomAssignments)
            .SelectMany(a => a.RoomIds)
            .ToHashSet();
        if (input.RoomIds.Any(roomId => !allowedRoomIdsForPlan.Contains(roomId)))
            throw new UserFriendlyException("One or more selected rooms are not assigned to this seating plan's subjects.");

        var (total, allocated) = await ComputeCountsAsync(exam, plan.Id);
        var status = ComputeStatus(total, allocated, plan.IsLocked, plan.Status == SeatingPlanStatus.Published);

        plan.SaveDraft(ToCsv(input.RoomIds), input.AllocationRulesJson, ToCsv(input.ExcludedStudentIds), ToCsv(input.SpecialRequirementStudentIds), status);
        await seatingPlanRepository.UpdateAsync(plan, autoSave: true);

        return await GetSeatingPlanDraftAsync(examId, plan.Id);
    }

    public async Task<SeatingPlanDraftResponseDto> SaveSeatingPlanBulkAsync(Guid examId, SaveSeatingPlanBulkInputDto input)
    {
        // Keep room replacement and plan metadata in one transaction. The room list is intentionally
        // patch-like: rooms not loaded by the workspace are preserved instead of being cleared.
        using var uow = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);

        if (input.LastKnownModifiedTime.HasValue && plan.LastModificationTime.HasValue &&
            plan.LastModificationTime.Value != input.LastKnownModifiedTime.Value)
        {
            throw new UserFriendlyException("This seating plan was modified by someone else since you last loaded it. Refresh to see the latest changes before saving.")
            { Code = "SEATING_PLAN_CONCURRENCY_CONFLICT" };
        }
        if (plan.IsLocked)
            throw new UserFriendlyException("Unlock this seating plan before modifying it.");

        var submittedStudentIds = input.Rooms.SelectMany(r => r.Students).Select(s => s.StudentId).ToList();
        if (submittedStudentIds.Count != submittedStudentIds.Distinct().Count())
            throw new UserFriendlyException("A student may appear in only one submitted room.");

        // Apply the subject scope before validating room students. Eligibility is derived from
        // the persisted plan subjects, so validating rooms first rejects valid students when the
        // request is creating or changing a subject-wise plan.
        var draftInput = new SaveSeatingPlanDraftInputDto
        {
            SeatingPlanId = plan.Id,
            Name = input.Name,
            ScopeType = input.ScopeType,
            ExamSubjectIds = input.ExamSubjectIds,
            RoomIds = input.RoomIds,
            AllocationRulesJson = input.AllocationRulesJson,
            ExcludedStudentIds = input.ExcludedStudentIds,
            SpecialRequirementStudentIds = input.SpecialRequirementStudentIds
        };
        await SaveSeatingPlanDraftAsync(examId, draftInput);

        foreach (var room in input.Rooms)
        {
            await SaveSeatAllocationAsync(examId, new SaveSeatAllocationRequestDto
            {
                SeatingPlanId = plan.Id,
                RoomId = room.RoomId,
                Students = room.Students
            });
        }

        // Recompute status/counts after the room allocations have been applied.
        await SaveSeatingPlanDraftAsync(examId, draftInput);

        await CurrentUnitOfWork!.SaveChangesAsync();
        await uow.CompleteAsync();
        return await GetSeatingPlanDraftAsync(examId, plan.Id);
    }

    private static string ToCsv(List<Guid> ids) => string.Join(",", ids);

    private static List<Guid> ParseGuidCsv(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => Guid.TryParse(s, out _))
                .Select(Guid.Parse)
                .ToList();

    private static HashSet<int> ParseIntCsv(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => int.TryParse(s, out _))
                .Select(int.Parse)
                .ToHashSet();

    private async Task<List<Room>> GetRoomsInScopeAsync(Guid examId)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, null);
        var roomIds = ParseGuidCsv(plan.RoomIdsCsv);
        return roomIds.Count > 0
            ? await roomRepository.GetListAsync(r => roomIds.Contains(r.Id))
            : [];
    }

    private async Task<List<Room>> GetRoomsInScopeAsync(Guid examId, Guid? seatingPlanId)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, seatingPlanId);
        var roomIds = ParseGuidCsv(plan?.RoomIdsCsv);
        return roomIds.Count > 0
            ? await roomRepository.GetListAsync(r => roomIds.Contains(r.Id))
            : [];
    }

    private async Task<List<SeatingPlanSubjectDto>> GetExamSubjectDtosAsync(Exam exam)
    {
        var subjectIds = exam.ExamSubjects.Select(s => s.SubjectId).Distinct().ToList();
        var subjects = subjectIds.Count > 0
            ? await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id))
            : [];
        var subjectById = subjects.ToDictionary(s => s.Id);

        return exam.ExamSubjects.Select(es =>
        {
            subjectById.TryGetValue(es.SubjectId, out var subject);
            return new SeatingPlanSubjectDto
            {
                ExamSubjectId = es.Id,
                SubjectId = es.SubjectId,
                SubjectName = subject?.Name ?? "Subject",
                ExamDate = es.ExamDate,
                StartTime = es.StartTime,
                EndTime = es.EndTime
            };
        }).ToList();
    }

    private async Task<SeatingPlanScopeDto> ToScopeDtoAsync(Exam exam, ExamSeatingPlan plan)
    {
        var planSubjects = await GetPlanSubjectsAsync(plan.Id);
        var availableSubjects = await GetExamSubjectDtosAsync(exam);
        var availableByExamSubjectId = availableSubjects.ToDictionary(s => s.ExamSubjectId);
        var (total, allocated) = await ComputeCountsAsync(exam, plan.Id);
        var status = ComputeStatus(total, allocated, plan.IsLocked, plan.Status == SeatingPlanStatus.Published);

        return new SeatingPlanScopeDto
        {
            SeatingPlanId = plan.Id,
            ExamId = exam.Id,
            Name = plan.Name,
            ScopeType = plan.ScopeType,
            Status = status,
            IsLocked = plan.IsLocked,
            Subjects = planSubjects
                .Select(s => availableByExamSubjectId.GetValueOrDefault(s.ExamSubjectId))
                .Where(s => s != null)
                .Select(s => s!)
                .ToList(),
            TotalStudents = total,
            AllocatedStudents = allocated,
            PendingStudents = Math.Max(0, total - allocated),
            AllocationProgress = total == 0 ? 0 : (int) Math.Round(allocated / (double) total * 100)
        };
    }

    public async Task<SeatingPlanScopeListResponseDto> GetSeatingPlanScopesAsync(Guid examId)
    {
        var exam = await GetScopedExamAsync(examId);
        await EnsureDefaultPlanAsync(exam);
        var plans = await seatingPlanRepository.GetListAsync(p => p.ExamId == examId);
        var items = new List<SeatingPlanScopeDto>();
        foreach (var plan in plans.OrderBy(p => p.ScopeType).ThenBy(p => p.Name))
        {
            items.Add(await ToScopeDtoAsync(exam, plan));
        }

        return new SeatingPlanScopeListResponseDto
        {
            ExamId = examId,
            AvailableSubjects = await GetExamSubjectDtosAsync(exam),
            Items = items
        };
    }

    public async Task<SeatingPlanScopeDto> SaveSeatingPlanScopeAsync(Guid examId, SaveSeatingPlanScopeInputDto input)
    {
        var exam = await GetScopedExamAsync(examId);
        var examSubjectIds = NormalizeExamSubjectIds(exam, input.ScopeType, input.ExamSubjectIds);

        ExamSeatingPlan plan;
        if (input.SeatingPlanId.HasValue)
        {
            plan = await seatingPlanRepository.FindAsync(p => p.ExamId == examId && p.Id == input.SeatingPlanId.Value)
                ?? throw new UserFriendlyException("Seating plan not found.");
            if (plan.IsLocked)
                throw new UserFriendlyException("Unlock this seating plan before modifying it.");

            var currentSubjects = await GetPlanSubjectsAsync(plan.Id);
            var allocatedCount = await seatAllocationRepository.CountAsync(a => a.ExamId == examId && a.SeatingPlanId == plan.Id);
            if ((plan.ScopeType != input.ScopeType || HasSubjectSetChanged(currentSubjects, examSubjectIds)) && allocatedCount > 0)
                throw new UserFriendlyException("Remove seat allocations before changing subjects for this seating plan.");

            await EnsureSubjectWiseSubjectsAreAvailableAsync(examId, plan.Id, input.ScopeType, examSubjectIds);
            plan.UpdateScope(input.Name, input.ScopeType);
            await seatingPlanRepository.UpdateAsync(plan, autoSave: true);
        }
        else
        {
            var matchingPlan = await FindMatchingPlanAsync(examId, input.ScopeType, examSubjectIds);
            if (matchingPlan == null)
            {
                await EnsureSubjectWiseSubjectsAreAvailableAsync(examId, null, input.ScopeType, examSubjectIds);
                plan = new ExamSeatingPlan(guidGenerator.Create(), CurrentTenant.Id, examId, input.Name, input.ScopeType);
                await seatingPlanRepository.InsertAsync(plan, autoSave: true);
            }
            else if (!matchingPlan.IsLocked && !string.Equals(matchingPlan.Name, input.Name, StringComparison.Ordinal))
            {
                plan = matchingPlan;
                plan.UpdateScope(input.Name, input.ScopeType);
                await seatingPlanRepository.UpdateAsync(plan, autoSave: true);
            }
            else
            {
                plan = matchingPlan;
            }
        }

        await EnsurePlanSubjectsAsync(exam, plan, examSubjectIds);
        return await ToScopeDtoAsync(exam, plan);
    }

    public async Task DeleteSeatingPlanScopeAsync(Guid examId, Guid seatingPlanId)
    {
        var plan = await seatingPlanRepository.FindAsync(p => p.ExamId == examId && p.Id == seatingPlanId)
            ?? throw new UserFriendlyException("Seating plan not found.");
        if (plan.IsLocked)
            throw new UserFriendlyException("Unlock this seating plan before deleting it.");

        var allocations = await seatAllocationRepository.CountAsync(a => a.SeatingPlanId == seatingPlanId);
        if (allocations > 0)
            throw new UserFriendlyException("Remove seat allocations before deleting this seating plan.");

        var subjects = await seatingPlanSubjectRepository.GetListAsync(s => s.SeatingPlanId == seatingPlanId);
        if (subjects.Count > 0)
            await seatingPlanSubjectRepository.DeleteManyAsync(subjects, autoSave: true);
        await seatingPlanRepository.DeleteAsync(plan, autoSave: true);
    }

    private static int UsableCapacity(Room room)
    {
        var total = room.TotalBenches > 0 ? room.TotalBenches * room.BenchCapacity : room.Capacity;
        var disabled = ParseIntCsv(room.DisabledSeatNumbersCsv).Count;
        return Math.Max(0, total - disabled);
    }

    private static bool SubjectSchedulesOverlap(ExamSubject left, ExamSubject right)
    {
        if (!left.ExamDate.HasValue || !right.ExamDate.HasValue)
            return false;
        if (left.ExamDate.Value.Date != right.ExamDate.Value.Date)
            return false;
        if (!left.StartTime.HasValue || !left.EndTime.HasValue || !right.StartTime.HasValue || !right.EndTime.HasValue)
            return true;

        return left.StartTime.Value.TimeOfDay < right.EndTime.Value.TimeOfDay &&
               right.StartTime.Value.TimeOfDay < left.EndTime.Value.TimeOfDay;
    }

    private static bool PlanSchedulesOverlap(
        IReadOnlyCollection<Guid> currentExamSubjectIds,
        IReadOnlyCollection<Guid> otherExamSubjectIds,
        IReadOnlyDictionary<Guid, ExamSubject> examSubjectsById)
    {
        foreach (var currentId in currentExamSubjectIds)
        {
            if (!examSubjectsById.TryGetValue(currentId, out var currentSubject))
                continue;

            foreach (var otherId in otherExamSubjectIds)
            {
                if (examSubjectsById.TryGetValue(otherId, out var otherSubject) &&
                    SubjectSchedulesOverlap(currentSubject, otherSubject))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Server-side capacity + conflict validation (spec §16) — the authoritative check finalize
    /// must pass. Mirrors the client-side adapter in the FE's utils/validate.ts so both surfaces
    /// agree, but this is the one that actually gates finalization.
    /// </summary>
    public async Task<SeatingValidationResponseDto> ValidateSeatingPlanAsync(Guid examId, Guid? seatingPlanId = null)
    {
        var blockers = new List<SeatingValidationIssueDto>();
        var warnings = new List<SeatingValidationIssueDto>();
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, seatingPlanId);

        var rooms = await GetRoomsInScopeAsync(examId, plan.Id);
        var roomsById = rooms.ToDictionary(r => r.Id);
        var allocations = await seatAllocationRepository.GetListAsync(a => a.ExamId == examId && a.SeatingPlanId == plan.Id);
        var eligibleStudents = await GetEligibleStudentsForPlanAsync(exam, plan);
        var eligibleStudentIds = eligibleStudents.Select(s => s.Id).ToHashSet();

        if (eligibleStudentIds.Count == 0)
            blockers.Add(new SeatingValidationIssueDto { Code = "NO_ELIGIBLE_STUDENTS", Message = "This seating plan has no eligible students." });
        if (rooms.Count == 0)
            blockers.Add(new SeatingValidationIssueDto { Code = "NO_ROOMS_IN_SCOPE", Message = "Select at least one room before finalizing the seating plan." });

        foreach (var alloc in allocations.Where(a => !eligibleStudentIds.Contains(a.StudentId)))
            blockers.Add(new SeatingValidationIssueDto
            {
                Code = "STUDENT_NOT_ELIGIBLE",
                Message = "An allocation belongs to a student outside this plan's eligible subject/division scope.",
                StudentId = alloc.StudentId,
                RoomId = alloc.RoomId,
                SeatId = $"{alloc.RoomId}:{alloc.SeatNumber}"
            });

        var assignedStudentIds = allocations.Select(a => a.StudentId).ToHashSet();
        foreach (var missingStudentId in eligibleStudentIds.Except(assignedStudentIds))
            blockers.Add(new SeatingValidationIssueDto
            {
                Code = "STUDENT_NOT_ALLOCATED",
                Message = "An eligible student has not been assigned a seat.",
                StudentId = missingStudentId
            });

        // Two students on the same seat in the same room.
        var bySeat = allocations.GroupBy(a => (a.RoomId, a.SeatNumber));
        foreach (var group in bySeat.Where(g => g.Count() > 1))
        {
            foreach (var alloc in group)
            {
                blockers.Add(new SeatingValidationIssueDto
                {
                    Code = "SEAT_DOUBLE_BOOKED",
                    Message = "Two students are assigned to the same seat.",
                    StudentId = alloc.StudentId,
                    RoomId = alloc.RoomId,
                    SeatId = $"{alloc.RoomId}:{alloc.SeatNumber}"
                });
            }
        }

        // Capacity + utilization per room in scope.
        foreach (var room in rooms)
        {
            var usable = UsableCapacity(room);
            var occupied = allocations.Count(a => a.RoomId == room.Id);
            if (occupied > usable)
            {
                blockers.Add(new SeatingValidationIssueDto
                {
                    Code = "ROOM_CAPACITY_EXCEEDED",
                    Message = $"Room {room.RoomName} has more students allocated than usable seats.",
                    RoomId = room.Id
                });
            }
            else if (usable > 0 && occupied / (double) usable < 0.4)
            {
                warnings.Add(new SeatingValidationIssueDto
                {
                    Code = "ROOM_LOW_UTILIZATION",
                    Message = $"Room {room.RoomName} utilization is below 40%.",
                    RoomId = room.Id
                });
            }
        }

        foreach (var alloc in allocations.Where(a => roomsById.ContainsKey(a.RoomId)))
        {
            var room = roomsById[alloc.RoomId];
            var totalSeats = room.TotalBenches > 0 ? room.TotalBenches * room.BenchCapacity : room.Capacity;
            if (alloc.SeatNumber <= 0 || alloc.SeatNumber > totalSeats ||
                ParseIntCsv(room.DisabledSeatNumbersCsv).Contains(alloc.SeatNumber) ||
                ParseIntCsv(room.ReservedSeatNumbersCsv).Contains(alloc.SeatNumber))
            {
                blockers.Add(new SeatingValidationIssueDto
                {
                    Code = "SEAT_UNAVAILABLE",
                    Message = "An allocation uses a seat outside the room layout or an unavailable seat.",
                    StudentId = alloc.StudentId,
                    RoomId = alloc.RoomId,
                    SeatId = $"{alloc.RoomId}:{alloc.SeatNumber}"
                });
            }
        }

        // Allocated to a room outside the plan's current scope.
        foreach (var alloc in allocations.Where(a => !roomsById.ContainsKey(a.RoomId)))
        {
            warnings.Add(new SeatingValidationIssueDto
            {
                Code = "ROOM_OUTSIDE_PLAN_SCOPE",
                Message = "Student is allocated to a room no longer selected for this plan.",
                StudentId = alloc.StudentId,
                RoomId = alloc.RoomId
            });
        }

        // Same room/seat reused by another seating plan whose subjects run at the same time.
        var currentPlanSubjectIds = (await GetPlanSubjectsAsync(plan.Id)).Select(s => s.ExamSubjectId).ToList();
        var examSubjectsById = exam.ExamSubjects.ToDictionary(s => s.Id);
        var otherPlanSubjects = await seatingPlanSubjectRepository.GetListAsync(s =>
            s.ExamId == examId && s.SeatingPlanId != plan.Id);
        var overlappingPlanIds = otherPlanSubjects
            .GroupBy(s => s.SeatingPlanId)
            .Where(group => PlanSchedulesOverlap(
                currentPlanSubjectIds,
                group.Select(s => s.ExamSubjectId).ToList(),
                examSubjectsById))
            .Select(group => group.Key)
            .ToHashSet();

        if (overlappingPlanIds.Count > 0)
        {
            var otherAllocations = await seatAllocationRepository.GetListAsync(a =>
                a.ExamId == examId && overlappingPlanIds.Contains(a.SeatingPlanId));
            var currentSeatKeys = allocations
                .GroupBy(a => (a.RoomId, a.SeatNumber))
                .ToDictionary(g => g.Key, g => g.First());
            foreach (var otherAlloc in otherAllocations)
            {
                if (!currentSeatKeys.TryGetValue((otherAlloc.RoomId, otherAlloc.SeatNumber), out var currentAlloc))
                    continue;

                blockers.Add(new SeatingValidationIssueDto
                {
                    Code = "ROOM_SEAT_TIME_CONFLICT",
                    Message = "This room and seat are already used by another seating plan at the same exam time.",
                    StudentId = currentAlloc.StudentId,
                    RoomId = currentAlloc.RoomId,
                    SeatId = $"{currentAlloc.RoomId}:{currentAlloc.SeatNumber}"
                });
            }
        }

        return new SeatingValidationResponseDto
        {
            IsValid = blockers.Count == 0,
            Blockers = blockers,
            Warnings = warnings
        };
    }

    public async Task<SeatingRoomSummaryResponseDto> GetSeatingRoomSummaryAsync(Guid examId, Guid? seatingPlanId = null)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, seatingPlanId);
        var rooms = await GetRoomsInScopeAsync(examId, plan.Id);
        var allocations = await seatAllocationRepository.GetListAsync(a => a.ExamId == examId && a.SeatingPlanId == plan.Id);

        var summary = rooms.Select(room =>
        {
            var usable = UsableCapacity(room);
            var occupied = allocations.Count(a => a.RoomId == room.Id);
            return new SeatingRoomSummaryDto
            {
                RoomId = room.Id,
                RoomName = room.RoomName,
                Occupied = occupied,
                UsableCapacity = usable,
                EmptySeats = Math.Max(0, usable - occupied),
                Utilization = usable == 0 ? 0 : (int) Math.Round(occupied / (double) usable * 100)
            };
        }).ToList();

        return new SeatingRoomSummaryResponseDto { Rooms = summary };
    }

    private async Task<string> SnapshotAssignmentsJsonAsync(Guid examId, Guid seatingPlanId)
    {
        var allocations = await seatAllocationRepository.GetListAsync(a => a.ExamId == examId && a.SeatingPlanId == seatingPlanId);
        var snapshot = allocations.Select(a => new { a.StudentId, a.RoomId, a.SeatNumber, a.BenchId });
        return System.Text.Json.JsonSerializer.Serialize(snapshot);
    }

    public async Task<SeatingPlanDraftResponseDto> FinalizeSeatingPlanAsync(Guid examId, Guid? seatingPlanId = null)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, seatingPlanId);

        var validation = await ValidateSeatingPlanAsync(examId, plan.Id);
        if (!validation.IsValid)
        {
            var blockerSummary = string.Join(
                " ",
                validation.Blockers
                    .Take(5)
                    .Select(issue => $"[{issue.Code}] {issue.Message}"));
            throw new UserFriendlyException(
                $"Cannot finalize while blocking issues remain. {blockerSummary}");
        }

        var now = DateTime.UtcNow;
        plan.Finalize(CurrentUser.Id ?? Guid.Empty, now);
        await seatingPlanRepository.UpdateAsync(plan, autoSave: true);

        await auditLogRepository.InsertAsync(new SeatingPlanAuditLog(
            guidGenerator.Create(), CurrentTenant.Id, examId, plan.Id, SeatingPlanAuditAction.Finalized,
            CurrentUser.Id ?? Guid.Empty, CurrentUser.Name, null, await SnapshotAssignmentsJsonAsync(examId, plan.Id)),
            autoSave: true);

        return await GetSeatingPlanDraftAsync(examId, plan.Id);
    }

    public async Task<SeatingPlanDraftResponseDto> PublishSeatingPlanAsync(Guid examId, PublishSeatingPlanInputDto input, Guid? seatingPlanId = null)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, seatingPlanId);

        if (!plan.IsLocked)
            throw new UserFriendlyException("Finalize the seating plan before publishing.");

        var now = DateTime.UtcNow;
        plan.Publish(CurrentUser.Id ?? Guid.Empty, now);
        await seatingPlanRepository.UpdateAsync(plan, autoSave: true);

        await auditLogRepository.InsertAsync(new SeatingPlanAuditLog(
            guidGenerator.Create(), CurrentTenant.Id, examId, plan.Id, SeatingPlanAuditAction.Published,
            CurrentUser.Id ?? Guid.Empty, CurrentUser.Name, null, null),
            autoSave: true);

        await seatingPlanNotifier.NotifyAsync(examId, input);

        return await GetSeatingPlanDraftAsync(examId, plan.Id);
    }

    public async Task<SeatingPlanDraftResponseDto> UnlockSeatingPlanAsync(Guid examId, UnlockSeatingPlanInputDto input, Guid? seatingPlanId = null)
    {
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw new UserFriendlyException("A reason is required to unlock a finalized seating plan.");

        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, seatingPlanId);

        plan.Unlock();
        await seatingPlanRepository.UpdateAsync(plan, autoSave: true);

        await auditLogRepository.InsertAsync(new SeatingPlanAuditLog(
            guidGenerator.Create(), CurrentTenant.Id, examId, plan.Id, SeatingPlanAuditAction.Unlocked,
            CurrentUser.Id ?? Guid.Empty, CurrentUser.Name, input.Reason, null),
            autoSave: true);

        return await GetSeatingPlanDraftAsync(examId, plan.Id);
    }

    public async Task<SeatingPlanAuditLogResponseDto> GetSeatingPlanAuditLogAsync(Guid examId, Guid? seatingPlanId = null)
    {
        var logs = await auditLogRepository.GetListAsync(l =>
            l.ExamId == examId && (!seatingPlanId.HasValue || l.SeatingPlanId == seatingPlanId.Value));
        return new SeatingPlanAuditLogResponseDto
        {
            Items = logs.OrderByDescending(l => l.ChangedAt).Select(l => new SeatingPlanAuditLogDto
            {
                Id = l.Id,
                SeatingPlanId = l.SeatingPlanId,
                Action = l.Action,
                ActorUserId = l.ActorUserId,
                ActorNameSnapshot = l.ActorNameSnapshot,
                Reason = l.Reason,
                ChangedAt = l.ChangedAt
            }).ToList()
        };
    }

    /// <summary>Flat per-seat rows for the room seating chart print view and master seating list export.</summary>
    public async Task<SeatingPrintDataResponseDto> GetSeatingPrintDataAsync(Guid examId, Guid? seatingPlanId = null)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, seatingPlanId);
        var allocations = await seatAllocationRepository.GetListAsync(a => a.ExamId == examId && a.SeatingPlanId == plan.Id);

        var roomIds = allocations.Select(a => a.RoomId).Distinct().ToList();
        var rooms = roomIds.Count > 0
            ? await roomRepository.GetListAsync(r => roomIds.Contains(r.Id))
            : new List<Room>();
        var roomsDict = rooms.ToDictionary(r => r.Id);

        var studentIds = allocations.Select(a => a.StudentId).Distinct().ToList();
        var students = studentIds.Count > 0
            ? await studentRepository.GetListAsync(s => studentIds.Contains(s.Id))
            : new List<Student>();
        var studentsDict = students.ToDictionary(s => s.Id);

        var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var standards = standardIds.Count > 0
            ? await standardRepository.GetListAsync(s => standardIds.Contains(s.Id))
            : new List<Standard>();
        var standardsDict = standards.ToDictionary(s => s.Id);

        var rows = allocations
            .OrderBy(a => roomsDict.TryGetValue(a.RoomId, out var r) ? r.RoomName : string.Empty)
            .ThenBy(a => a.SeatNumber)
            .Select(a =>
            {
                studentsDict.TryGetValue(a.StudentId, out var student);
                var stdName = student?.StandardId != null && standardsDict.TryGetValue(student.StandardId!.Value, out var std)
                    ? std.Name : string.Empty;
                return new SeatingPrintRowDto
                {
                    RoomName = roomsDict.TryGetValue(a.RoomId, out var room) ? room.RoomName : string.Empty,
                    SeatNumber = a.SeatNumber,
                    StudentName = student != null ? $"{student.FirstName} {student.MiddleName} {student.LastName}".Replace("  ", " ").Trim() : string.Empty,
                    RollNo = student?.RollNo ?? 0,
                    StandardName = stdName
                };
            }).ToList();

        return new SeatingPrintDataResponseDto { Rows = rows };
    }

    public async Task<byte[]> GenerateSeatingPrintPdfAsync(Guid examId, Guid? seatingPlanId = null)
    {
        var data = await GetSeatingPrintDataAsync(examId, seatingPlanId);
        return await pdfService.GenerateSimplePdf(data.Rows);
    }

    public async Task<byte[]> GenerateSeatingPrintExcelAsync(Guid examId, Guid? seatingPlanId = null)
    {
        var data = await GetSeatingPrintDataAsync(examId, seatingPlanId);

        var headerRow = new ExcelRow
        {
            Cells =
            [
                new ExcelCell { Value = "Room" },
                new ExcelCell { Value = "Seat Number" },
                new ExcelCell { Value = "Student Name" },
                new ExcelCell { Value = "Roll No" },
                new ExcelCell { Value = "Class" }
            ]
        };
        var dataRows = data.Rows.Select(row => new ExcelRow
        {
            Cells =
            [
                new ExcelCell { Value = row.RoomName },
                new ExcelCell { Value = row.SeatNumber },
                new ExcelCell { Value = row.StudentName },
                new ExcelCell { Value = row.RollNo },
                new ExcelCell { Value = row.StandardName }
            ]
        });

        var document = new ExcelDocument
        {
            Sheets =
            [
                new ExcelSheet
                {
                    SheetName = "Master Seating List",
                    Rows = new List<ExcelRow> { headerRow }.Concat(dataRows).ToList()
                }
            ]
        };

        return excelService.GenerateExcelAsBytes(document);
    }

    private sealed record AllocSeat(int SeatNumber, int Row, int Column, bool IsDisabled);

    private static List<AllocSeat> BuildRoomSeats(Room room)
    {
        var capacity = room.TotalBenches > 0 ? room.TotalBenches * room.BenchCapacity : room.Capacity;
        var columns = Math.Max(1, room.BenchesPerColumn);
        var benchCapacity = Math.Max(1, room.BenchCapacity);
        var disabled = ParseIntCsv(room.DisabledSeatNumbersCsv);

        var seats = new List<AllocSeat>();
        for (var i = 0; i < capacity; i++)
        {
            var seatNumber = i + 1;
            var row = i / (columns * benchCapacity);
            var column = i / benchCapacity % columns;
            seats.Add(new AllocSeat(seatNumber, row, column, disabled.Contains(seatNumber)));
        }
        return seats;
    }

    private static List<AllocSeat> OrderSeats(List<AllocSeat> seats, string fillDirection) => fillDirection switch
    {
        "back-to-front" => seats.OrderByDescending(s => s.Row).ThenBy(s => s.Column).ToList(),
        "column-wise" => seats.OrderBy(s => s.Column).ThenBy(s => s.Row).ToList(),
        _ => seats.OrderBy(s => s.Row).ThenBy(s => s.Column).ToList()
    };

    /// <summary>
    /// Server-side port of the FE's utils/autoAllocate.ts adapter (spec §12). Computes a proposal
    /// without persisting anything — the caller must explicitly save the returned assignments via
    /// SaveSeatAllocationAsync per room, matching the "preview before apply" requirement (§12.3).
    /// Note: rank ordering and special-requirement priority currently fall back to roll-number /
    /// no-op respectively, since the Student entity does not yet carry a rank or special-requirement flag.
    /// </summary>
    public async Task<AutoAllocateResponseDto> AutoAllocatePreviewAsync(Guid examId, AutoAllocateRequestDto input)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);
        var eligibleStudents = await GetEligibleStudentsForPlanAsync(exam, plan);

        var excluded = input.ExcludedStudentIds.ToHashSet();
        var alreadyAllocated = (await seatAllocationRepository.GetListAsync(a => a.ExamId == examId && a.SeatingPlanId == plan.Id))
            .Select(a => a.StudentId).ToHashSet();

        var pending = eligibleStudents
            .Where(s => !excluded.Contains(s.Id) && !alreadyAllocated.Contains(s.Id))
            .ToList();

        var ordered = input.Strategy switch
        {
            "alphabetical" => pending.OrderBy(s => $"{s.FirstName} {s.LastName}").ToList(),
            "random" => pending.OrderBy(_ => Guid.NewGuid()).ToList(),
            "alternate-division" or "alternate-class" => pending
                .GroupBy(s => s.DivisionId ?? Guid.Empty)
                .SelectMany(g => g.Select((s, idx) => (s, idx)))
                .OrderBy(x => x.idx)
                .Select(x => x.s)
                .ToList(),
            _ => pending.OrderBy(s => s.RollNo ?? int.MaxValue).ToList()
        };

        if (input.SpecialRequirementPriority)
        {
            var priorityIds = ParseGuidCsv(plan.SpecialRequirementStudentIdsCsv).ToHashSet();
            if (priorityIds.Count > 0)
            {
                ordered = ordered.OrderByDescending(s => priorityIds.Contains(s.Id)).ToList();
            }
        }

        var rooms = input.RoomIds.Count > 0
            ? await roomRepository.GetListAsync(r => input.RoomIds.Contains(r.Id))
            : new List<Room>();

        var internalOccupied = await seatAllocationRepository.GetListAsync(a =>
                a.ExamId == examId && input.RoomIds.Contains(a.RoomId));
                
        var externalOccupied = await GetExternalOverlappingAllocationsAsync(examId, plan.Id, input.RoomIds);
        
        var occupiedSeatNumbersByRoom = internalOccupied.Concat(externalOccupied)
            .GroupBy(a => a.RoomId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.SeatNumber).ToHashSet());

        var proposed = new List<AutoAllocateProposedAssignmentDto>();
        var studentQueue = new Queue<Student>(ordered);

        foreach (var room in rooms)
        {
            occupiedSeatNumbersByRoom.TryGetValue(room.Id, out var occupied);
            occupied ??= [];

            var seatQueue = new Queue<AllocSeat>(
                OrderSeats(BuildRoomSeats(room), input.FillDirection)
                    .Where(s => !s.IsDisabled && !occupied.Contains(s.SeatNumber)));

            while (studentQueue.Count > 0 && seatQueue.Count > 0)
            {
                var seat = seatQueue.Dequeue();
                if (input.LeaveGap && seatQueue.Count > 0)
                {
                    // Skip the immediate next seat in the queue to leave one empty seat between students.
                    seatQueue.Dequeue();
                }
                var student = studentQueue.Dequeue();
                proposed.Add(new AutoAllocateProposedAssignmentDto
                {
                    StudentId = student.Id,
                    RoomId = room.Id,
                    SeatNumber = seat.SeatNumber,
                    BenchId = null
                });
            }

            if (studentQueue.Count == 0) break;
        }

        var totalUsable = rooms.Sum(r => BuildRoomSeats(r).Count(s => !s.IsDisabled) - (occupiedSeatNumbersByRoom.GetValueOrDefault(r.Id)?.Count ?? 0));
        var roomsUsed = proposed.Select(p => p.RoomId).Distinct().Count();

        return new AutoAllocateResponseDto
        {
            Assignments = proposed,
            UnallocatedStudentIds = studentQueue.Select(s => s.Id).ToList(),
            TotalStudents = pending.Count,
            RoomsUsed = roomsUsed,
            EmptySeats = Math.Max(0, totalUsable - proposed.Count),
            UtilizationPercent = totalUsable == 0 ? 0 : (int) Math.Round(proposed.Count / (double) totalUsable * 100)
        };
    }

    public async Task<SeatLayoutResponseDto> GetSeatLayoutAsync(Guid examId, GetSeatLayoutInput input)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);
        var room = await roomRepository.FindAsync(input.RoomId)
            ?? throw new UserFriendlyException("Room not found.");

        var totalSeats = room.TotalBenches > 0 ? room.TotalBenches * room.BenchCapacity : room.Capacity;

        // 1. Internal allocations (this exam)
        var internalAllocations = await seatAllocationRepository.GetListAsync(a =>
            a.ExamId == examId &&
            a.SeatingPlanId == plan.Id &&
            a.RoomId == input.RoomId);

        // 2. External allocations (overlapping time slots from completely different exams)
        var externalAllocations = await GetExternalOverlappingAllocationsAsync(examId, plan.Id, new List<Guid> { input.RoomId });

        var allAllocations = internalAllocations.Concat(externalAllocations).ToList();

        // Students lookup
        var studentIds = allAllocations.Select(a => a.StudentId).Distinct().ToList();
        var students = studentIds.Count > 0
            ? await studentRepository.GetListAsync(s => studentIds.Contains(s.Id))
            : new List<Student>();
        var studentsDict = students.ToDictionary(s => s.Id);

        // Standard lookup for each student
        var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var standards = standardIds.Count > 0
            ? await standardRepository.GetListAsync(s => standardIds.Contains(s.Id))
            : new List<Standard>();
        var standardsDict = standards.ToDictionary(s => s.Id);

        // Build seat items — one per seat in the room
        // A seat can only be shown once. The plan filter above prevents normal
        // cross-plan collisions; grouping also keeps old/inconsistent data from
        // turning this read-only endpoint into a 500 response.
        var allocBySeats = allAllocations
            .GroupBy(a => a.SeatNumber)
            .ToDictionary(g => g.Key, g => g.First());
        var allItems = new List<SeatLayoutItemDto>();

        for (var seat = 1; seat <= totalSeats; seat++)
        {
            allocBySeats.TryGetValue(seat, out var alloc);
            Student? student = alloc != null && studentsDict.TryGetValue(alloc.StudentId, out var s) ? s : null;
            var stdName = student?.StandardId != null && standardsDict.TryGetValue(student.StandardId!.Value, out var std)
                ? std.Name : string.Empty;

            allItems.Add(new SeatLayoutItemDto
            {
                SeatNumber = seat,
                StudentId = alloc?.StudentId,
                StudentName = (alloc != null && alloc.ExamId != examId) ? "Blocked (Other Exam)" : (student != null ? $"{student.FirstName} {student.MiddleName} {student.LastName}".Replace("  ", " ").Trim() : null),
                StandardName = (alloc != null && alloc.ExamId != examId) ? "Reserved" : stdName,
                RollNo = (alloc != null && alloc.ExamId != examId) ? null : student?.RollNo,
                RoomNumber = room.RoomName,
                IsRoomAssign = alloc?.IsRoomAssign ?? false,
                BenchId = alloc?.BenchId,
                IsExternalExamOccupied = alloc != null && alloc.ExamId != examId
            });
        }

        // Apply search
        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            allItems = allItems.Where(i =>
                (i.StudentName?.ToLower().Contains(search) ?? false) ||
                (i.StandardName?.ToLower().Contains(search) ?? false)).ToList();
        }

        var totalCount = allItems.Count;
        var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);
        var page = Math.Max(1, input.Page);
        var pagedItems = allItems.Skip((page - 1) * input.PageSize).Take(input.PageSize).ToList();

        return new SeatLayoutResponseDto
        {
            RoomId = input.RoomId,
            Room = ObjectMapper.Map<Room, RoomDto>(room),
            TotalSeats = totalSeats,
            AllocatedCount = allAllocations.Count,
            Items = pagedItems,
            TotalCount = totalCount,
            TotalPages = totalPages,
            PageSize = input.PageSize,
            CurrentPage = page
        };
    }

    public async Task<SeatStudentPagedResponseDto> GetStudentsForDivisionAsync(Guid examId, Guid divisionId, GetSeatStudentsInput input)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);

        var studentsQuery = await studentRepository.GetQueryableAsync();
        var query = studentsQuery.Where(s => s.DivisionId == divisionId && s.StandardId == exam.StandardId);

        if (plan.ScopeType == SeatingPlanScopeType.SingleSubject)
        {
            var subjectIds = (await GetPlanSubjectsAsync(plan.Id)).Select(s => s.SubjectId).Distinct().ToList();
            if (subjectIds.Count == 1)
            {
                var selectedStudentIds = (await studentElectiveSelectionRepository.GetListAsync(s =>
                    s.SelectedSubjectId == subjectIds[0])).Select(s => s.StudentId).ToHashSet();
                if (selectedStudentIds.Count > 0)
                    query = query.Where(s => selectedStudentIds.Contains(s.Id));
            }
        }

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            query = query.Where(s =>
                (s.FirstName != null && s.FirstName.ToLower().Contains(search)) ||
                (s.LastName != null && s.LastName.ToLower().Contains(search)));
        }

        var students = await AsyncExecuter.ToListAsync(query);

        // Check which students already have seat allocations for this exam
        var studentIds = students.Select(s => s.Id).ToList();
        var allocations = await seatAllocationRepository.GetListAsync(a =>
            a.ExamId == examId && a.SeatingPlanId == plan.Id && studentIds.Contains(a.StudentId));
        var allocatedSet = allocations.ToDictionary(a => a.StudentId);

        // Standard name lookup
        var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var standards = standardIds.Count > 0
            ? await standardRepository.GetListAsync(s => standardIds.Contains(s.Id))
            : new List<Standard>();
        var standardsDict = standards.ToDictionary(s => s.Id);

        var items = students.Select(s =>
        {
            allocatedSet.TryGetValue(s.Id, out var alloc);
            var stdName = s.StandardId != null && standardsDict.TryGetValue(s.StandardId!.Value, out var std)
                ? std.Name : string.Empty;
            return new SeatStudentDto
            {
                StudentId = s.Id,
                StudentName = $"{s.FirstName} {s.MiddleName} {s.LastName}".Replace("  ", " ").Trim(),
                StandardName = stdName,
                RollNo = s.RollNo ?? 0,
                SeatNumber = alloc?.SeatNumber ?? 0,
                IsSeatAllocate = alloc != null
            };
        }).ToList();

        var totalCount = items.Count;
        var page = Math.Max(1, input.Page);
        var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);
        var pagedItems = items.Skip((page - 1) * input.PageSize).Take(input.PageSize).ToList();

        return new SeatStudentPagedResponseDto
        {
            Items = pagedItems,
            PageNumber = page,
            PageSize = input.PageSize,
            TotalPages = totalPages,
            TotalCount = totalCount
        };
    }

    public async Task<SaveSeatAllocationResponseDto> SaveSeatAllocationAsync(Guid examId, SaveSeatAllocationRequestDto input)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);
        if (plan.IsLocked)
            throw new UserFriendlyException("Unlock this seating plan before modifying it.");

        var room = await roomRepository.FindAsync(input.RoomId)
            ?? throw new UserFriendlyException("Room not found.");
        var planSubjectIds = (await GetPlanSubjectsAsync(plan.Id)).Select(s => s.ExamSubjectId).ToHashSet();
        var allowedRoomIds = ParseGuidCsv(plan.RoomIdsCsv).ToHashSet();
        allowedRoomIds.UnionWith(exam.ExamSubjects
            .Where(s => planSubjectIds.Contains(s.Id))
            .SelectMany(s => s.RoomAssignments)
            .SelectMany(a => a.RoomIds));
        if (!allowedRoomIds.Contains(room.Id))
            throw new UserFriendlyException("The selected room is not available for this seating plan.");

        var eligibleStudents = await GetEligibleStudentsForPlanAsync(exam, plan);
        var eligibleStudentIds = eligibleStudents.Select(s => s.Id).ToHashSet();
        if (input.Students.Any(s => !eligibleStudentIds.Contains(s.StudentId)))
            throw new UserFriendlyException("One or more students are not eligible for this seating plan.");

        var disabledSeats = ParseIntCsv(room.DisabledSeatNumbersCsv);
        var reservedSeats = ParseIntCsv(room.ReservedSeatNumbersCsv);
        var totalSeats = room.TotalBenches > 0 ? room.TotalBenches * room.BenchCapacity : room.Capacity;
        if (input.Students.Any(s => s.SeatNumber > totalSeats || disabledSeats.Contains(s.SeatNumber) || reservedSeats.Contains(s.SeatNumber)))
            throw new UserFriendlyException("One or more selected seats are outside the room layout or unavailable.");

        var incomingStudentIds = input.Students.Select(s => s.StudentId).ToHashSet();

        // Load by studentId (not roomId) to respect the unique index on {ExamId, StudentId}.
        // Disable the soft-delete filter so previously-deleted allocations are visible —
        // otherwise a soft-deleted row is hidden, the code tries to INSERT, and the
        // unique constraint IX_AppExamSeatAllocations_ExamId_StudentId fires.
        List<ExamSeatAllocation> existingForStudents;
        using (dataFilter.Disable<ISoftDelete>())
        {
            existingForStudents = await seatAllocationRepository.GetListAsync(a =>
                a.ExamId == examId && a.SeatingPlanId == plan.Id && incomingStudentIds.Contains(a.StudentId));
        }
        var existingByStudent = existingForStudents.ToDictionary(a => a.StudentId);

        // Separately load the room-scoped list to find seats that should be cleared
        // (students removed from this room by the current submission).
        var existingInRoom = await seatAllocationRepository.GetListAsync(a =>
            a.ExamId == examId && a.SeatingPlanId == plan.Id && a.RoomId == input.RoomId);

        var toInsert = new List<ExamSeatAllocation>();
        var toUpdate = new List<ExamSeatAllocation>();

        foreach (var student in input.Students)
        {
            // Normalise Guid.Empty → null so bench FK constraints are not violated
            var benchId = student.BenchId == Guid.Empty ? null : student.BenchId;

            if (existingByStudent.TryGetValue(student.StudentId, out var existing))
            {
                // Restore soft-deleted record if needed before updating
                if (existing.IsDeleted)
                    existing.IsDeleted = false;
                existing.Update(plan.Id, input.RoomId, student.SeatNumber, benchId, student.IsRoomAssign);
                toUpdate.Add(existing);
            }
            else
            {
                toInsert.Add(new ExamSeatAllocation(
                    guidGenerator.Create(),
                    CurrentTenant.Id,
                    plan.Id,
                    examId,
                    student.StudentId,
                    input.RoomId,
                    student.SeatNumber,
                    benchId,
                    student.IsRoomAssign));
            }
        }

        // Delete seats in this room whose students are no longer in the submission
        var toDelete = existingInRoom
            .Where(a => !incomingStudentIds.Contains(a.StudentId))
            .ToList();

        if (toInsert.Count > 0)
            await seatAllocationRepository.InsertManyAsync(toInsert, autoSave: true);
        if (toUpdate.Count > 0)
            await seatAllocationRepository.UpdateManyAsync(toUpdate, autoSave: true);
        if (toDelete.Count > 0)
            await seatAllocationRepository.DeleteManyAsync(toDelete, autoSave: true);

        return new SaveSeatAllocationResponseDto
        {
            ExamId = examId,
            RoomId = input.RoomId,
            SavedCount = toInsert.Count + toUpdate.Count
        };
    }

    public async Task<SeatAllocationDivisionsResponseDto> GetDivisionsForExamAsync(Guid examId, GetExamDivisionsInput input)
    {
        var exam = await GetScopedExamAsync(examId);

        var divisionIds = exam.ExamDivisions.Select(d => d.DivisionId).ToList();
        var divisions = await divisionRepository.GetListAsync(d =>
              divisionIds.Contains(d.Id) && d.StandardId == exam.StandardId);

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            divisions = divisions.Where(d => d.Name.ToLower().Contains(search)).ToList();
        }

        divisions = divisions
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.Id)
            .ToList();

        var totalCount = divisions.Count;
        var page = Math.Max(1, input.Page);
        var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);
        var paged = divisions.Skip((page - 1) * input.PageSize).Take(input.PageSize).ToList();

        return new SeatAllocationDivisionsResponseDto
        {
            ExamId = examId,
            Items = paged.Select(d => new DivisionLiteDto { Id = d.Id, Name = d.Name }).ToList(),
            TotalCount = totalCount,
            TotalPages = totalPages,
            PageSize = input.PageSize,
            CurrentPage = page
        };
    }

    public async Task<SeatAllocationDashboardResponseDto> GetSeatAllocationTableAsync(GetSeatAllocationTableInput input)
    {
        var examQuery = await examRepository.GetQueryableAsync();
        var exams = await AsyncExecuter.ToListAsync(
            examQuery.Where(e => e.StandardId == input.StandardId && e.Status == ExamStatus.Published));

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            exams = exams.Where(e => e.ExamName.ToLower().Contains(search)).ToList();
        }

        // Fetch supporting data
        var candidateExamIds = exams.Select(e => e.Id).ToList();
        var allExamsWithDetails = await examRepository.GetListAsync(
            e => candidateExamIds.Contains(e.Id), includeDetails: true);

        if (!CurrentUser.Id.HasValue)
        {
            return new SeatAllocationDashboardResponseDto();
        }

        var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
        allExamsWithDetails = allExamsWithDetails
            .Where(exam => CanAccessExam(exam, scope))
            .ToList();

        var examIds = allExamsWithDetails.Select(e => e.Id).ToList();
        var examTypeIds = allExamsWithDetails.Select(e => e.ExamTypeId).Distinct().ToList();
        var examTypes = await examTypeRepository.GetListAsync(t => examTypeIds.Contains(t.Id));
        var examTypesDict = examTypes.ToDictionary(t => t.Id);

        var standard = await standardRepository.FindAsync(input.StandardId);
        var allDivisionIds = allExamsWithDetails
            .SelectMany(e => e.ExamDivisions.Select(d => d.DivisionId))
            .Distinct().ToList();
        var divisions = allDivisionIds.Count > 0
            ? await divisionRepository.GetListAsync(d => allDivisionIds.Contains(d.Id))
            : new List<Division>();
        var divisionsDict = divisions.ToDictionary(d => d.Id);

        // Students per division — needed to compute accurate per-exam totals
        var studentsInDivisions = allDivisionIds.Count > 0
            ? await studentRepository.GetListAsync(s =>
                s.StandardId == input.StandardId &&
                s.DivisionId.HasValue &&
                allDivisionIds.Contains(s.DivisionId!.Value))
            : new List<Student>();
        var studentCountByDivision = studentsInDivisions
            .Where(s => s.DivisionId.HasValue)
            .GroupBy(s => s.DivisionId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var studentDivisionById = studentsInDivisions
            .Where(s => s.DivisionId.HasValue)
            .ToDictionary(s => s.Id, s => s.DivisionId!.Value);

        // Allocations per exam
        var allocCounts = await seatAllocationRepository.GetListAsync(a => examIds.Contains(a.ExamId));
        var plans = await seatingPlanRepository.GetListAsync(p => examIds.Contains(p.ExamId));
        var allSubjectPlanByExam = plans
            .Where(p => p.ScopeType == SeatingPlanScopeType.AllSubjects)
            .GroupBy(p => p.ExamId)
            .ToDictionary(g => g.Key, g => g.First());
        var planCountByExam = plans.GroupBy(p => p.ExamId).ToDictionary(g => g.Key, g => g.Count());

        // When IsRoomAssigned=true, restrict the exam list to only those with at least one
        // room-assigned allocation, and count only room-assigned allocations.
        var roomAssignedFilter = input.IsRoomAssigned == true;
        var relevantAllocs = roomAssignedFilter
            ? allocCounts.Where(a => a.IsRoomAssign).ToList()
            : allocCounts;
        var examsWithRoomAssign = roomAssignedFilter
            ? relevantAllocs.Select(a => a.ExamId).ToHashSet()
            : null;
        if (examsWithRoomAssign != null)
            allExamsWithDetails = allExamsWithDetails.Where(e => examsWithRoomAssign.Contains(e.Id)).ToList();

        var allocByExam = relevantAllocs
            .Where(a => allSubjectPlanByExam.TryGetValue(a.ExamId, out var plan) && a.SeatingPlanId == plan.Id)
            .GroupBy(a => a.ExamId)
            .ToDictionary(g => g.Key, g => g.Count());
        var allocByExamDivision = relevantAllocs
            .Where(a =>
                allSubjectPlanByExam.TryGetValue(a.ExamId, out var plan) &&
                a.SeatingPlanId == plan.Id &&
                studentDivisionById.ContainsKey(a.StudentId))
            .GroupBy(a => new { a.ExamId, DivisionId = studentDivisionById[a.StudentId] })
            .ToDictionary(g => (g.Key.ExamId, g.Key.DivisionId), g => g.Count());

        var items = allExamsWithDetails.Select(exam =>
        {
            examTypesDict.TryGetValue(exam.ExamTypeId, out var examType);
            var orderedExamDivisions = exam.ExamDivisions
                .Where(d => divisionsDict.ContainsKey(d.DivisionId))
                .Select(d => divisionsDict[d.DivisionId])
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.Id)
                .ToList();
            var divisionNames = orderedExamDivisions
                .Select(d => d.Name)
                .ToList();
            var examDivisionIds = orderedExamDivisions.Select(d => d.Id).ToList();
            var totalStudents = examDivisionIds.Sum(divId => studentCountByDivision.GetValueOrDefault(divId, 0));
            var allocated = allocByExam.GetValueOrDefault(exam.Id, 0);
            var pending = totalStudents - allocated;
            var seatingPlan = allSubjectPlanByExam.GetValueOrDefault(exam.Id);
            var status = seatingPlan == null
                ? ComputeStatus(totalStudents, allocated, isLocked: false, isPublished: false)
                : ComputeStatus(
                    totalStudents,
                    allocated,
                    seatingPlan.IsLocked,
                    seatingPlan.Status == SeatingPlanStatus.Published);
            var divisionProgress = examDivisionIds
                .Where(divisionsDict.ContainsKey)
                .Select(divId =>
                {
                    var divisionTotal = studentCountByDivision.GetValueOrDefault(divId, 0);
                    var divisionAllocated = allocByExamDivision.GetValueOrDefault((exam.Id, divId), 0);
                    var divisionPending = Math.Max(0, divisionTotal - divisionAllocated);

                    return new SeatAllocationDivisionProgressDto
                    {
                        DivisionId = divId,
                        DivisionName = divisionsDict[divId].Name,
                        TotalStudents = divisionTotal,
                        AllocatedStudents = divisionAllocated,
                        PendingStudents = divisionPending,
                        Progress = divisionTotal == 0
                            ? 0
                            : Math.Min(100, (int) Math.Round(divisionAllocated * 100d / divisionTotal))
                    };
                })
                .ToList();

            return new SeatAllocationSummaryDto
            {
                ExamId = exam.Id,
                ExamName = exam.ExamName,
                ExamTypeName = examType?.Name ?? string.Empty,
                StandardName = standard?.Name ?? string.Empty,
                Divisions = divisionNames,
                DivisionProgress = divisionProgress,
                TotalStudents = totalStudents,
                AllocatedStudents = allocated,
                PendingStudents = Math.Max(0, pending),
                SeatingPlanCount = planCountByExam.GetValueOrDefault(exam.Id, 0),
                SubjectCount = exam.ExamSubjects.Count,
                Status = (int)status
            };
        }).ToList();

        var totalCount = items.Count;
        var page = Math.Max(1, input.Page);
        var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);
        var paged = items.Skip((page - 1) * input.PageSize).Take(input.PageSize).ToList();

        return new SeatAllocationDashboardResponseDto
        {
            Items = paged,
            TotalCount = totalCount,
            TotalPages = totalPages,
            PageSize = input.PageSize,
            CurrentPage = page
        };
    }

    public async Task<ExamRoomPagedResponseDto> GetRoomsForExamAndDivisionAsync(Guid examId, Guid divisionId, GetExamRoomsInput input)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);
        var planExamSubjectIds = (await GetPlanSubjectsAsync(plan.Id)).Select(s => s.ExamSubjectId).ToHashSet();

        // Collect room IDs assigned to this division for the subjects in this seating plan.
        var roomIds = exam.ExamSubjects
            .Where(s => planExamSubjectIds.Contains(s.Id))
            .SelectMany(s => s.RoomAssignments)
            .Where(ra => ra.DivisionId == divisionId)
            .SelectMany(ra => ra.RoomIds)
            .Distinct()
            .ToList();

        var rooms = roomIds.Count > 0
            ? await roomRepository.GetListAsync(r => roomIds.Contains(r.Id))
            : new List<Room>();

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            rooms = rooms.Where(r => r.RoomName.ToLower().Contains(search)).ToList();
        }

        // Allocation counts per room for this exam
        var allocations = await seatAllocationRepository.GetListAsync(a =>
            a.ExamId == examId && a.SeatingPlanId == plan.Id && roomIds.Contains(a.RoomId));
        var allocByRoom = allocations.GroupBy(a => a.RoomId).ToDictionary(g => g.Key, g => g.Count());

        var totalCount = rooms.Count;
        var page = Math.Max(1, input.Page);
        var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);
        var paged = rooms.Skip((page - 1) * input.PageSize).Take(input.PageSize).ToList();

        var items = paged.Select(r =>
        {
            var capacity = r.TotalBenches > 0 ? r.TotalBenches * r.BenchCapacity : r.Capacity;
            var allocated = allocByRoom.GetValueOrDefault(r.Id, 0);
            return new ExamRoomDto
            {
                Id = r.Id,
                RoomName = r.RoomName,
                RoomNumber = r.RoomName,
                Capacity = capacity,
                AllocatedSeats = allocated,
                AvailableSeats = Math.Max(0, capacity - allocated),
                IsAllocationCompleted = allocated >= capacity
            };
        }).ToList();

        return new ExamRoomPagedResponseDto
        {
            Items = items,
            CurrentPage = page,
            PageSize = input.PageSize,
            TotalPages = totalPages,
            TotalCount = totalCount
        };
    }

    private async Task<List<ExamSeatAllocation>> GetExternalOverlappingAllocationsAsync(Guid examId, Guid planId, List<Guid> roomIds)
    {
        var externalAllocations = new List<ExamSeatAllocation>();
        
        var planSubjects = await seatingPlanSubjectRepository.GetListAsync(s => s.SeatingPlanId == planId);
        var planSubjectIds = planSubjects.Select(s => s.ExamSubjectId).ToHashSet();
        
        var exam = await GetScopedExamAsync(examId);
        var currentSubjects = exam.ExamSubjects.Where(s => planSubjectIds.Contains(s.Id)).ToList();
        
        var activeTimeSlots = currentSubjects
            .Where(s => s.StartTime.HasValue && s.EndTime.HasValue)
            .Select(s => new { Start = s.StartTime.Value, End = s.EndTime.Value })
            .ToList();

        if (activeTimeSlots.Count > 0)
        {
            var allOtherExams = await examRepository.GetListAsync(e => e.Id != examId, includeDetails: true);
            var overlappingOtherSubjects = allOtherExams
                .SelectMany(e => e.ExamSubjects)
                .Where(s => s.StartTime.HasValue && s.EndTime.HasValue &&
                            activeTimeSlots.Any(t => s.StartTime.Value < t.End && s.EndTime.Value > t.Start))
                .Select(s => s.Id)
                .ToHashSet();

            if (overlappingOtherSubjects.Count > 0)
            {
                var overlappingPlanSubjects = await seatingPlanSubjectRepository.GetListAsync(s => overlappingOtherSubjects.Contains(s.ExamSubjectId));
                var overlappingPlanIds = overlappingPlanSubjects.Select(s => s.SeatingPlanId).ToHashSet();

                if (overlappingPlanIds.Count > 0)
                {
                    externalAllocations = await seatAllocationRepository.GetListAsync(a => 
                        roomIds.Contains(a.RoomId) && overlappingPlanIds.Contains(a.SeatingPlanId));
                }
            }
        }
        
        return externalAllocations;
    }
}
