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
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
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
    IRepository<ExamSeatingPublication, Guid> seatingPublicationRepository,
    IRepository<SeatingPlanAuditLog, Guid> auditLogRepository,
    IRepository<Exam, Guid> examRepository,
    IRepository<Student, Guid> studentRepository,
    IRepository<StudentElectiveSelection, Guid> studentElectiveSelectionRepository,
    IRepository<Room, Guid> roomRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<ExamType, Guid> examTypeRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<AssignedSubject, Guid> assignedSubjectRepository,
    IRepository<Employee, Guid> employeeRepository,
    IGuidGenerator guidGenerator,
    IDataFilter dataFilter,
    IPdfService pdfService,
    IExcelService excelService,
    ISeatingPlanNotifier seatingPlanNotifier,
    IUserScopeService userScopeService
) : ApplicationService, IExamSeatAllocationAppService
{
    private async Task<ExamSeatingPublication?> FindSeatingPublicationAsync(Guid examId) =>
        await seatingPublicationRepository.FindAsync(publication => publication.ExamId == examId);

    private async Task ThrowIfExaminationSeatingPublishedAsync(Guid examId)
    {
        if (await FindSeatingPublicationAsync(examId) != null)
            throw new UserFriendlyException("Published examination seating is read-only. Create a new examination revision for corrections.")
            {
                Code = "EXAMINATION_SEATING_PUBLISHED"
            };
    }

    private async Task ReopenLegacyPlanDraftAsync(ExamSeatingPlan plan)
    {
        if (!plan.IsLocked && plan.Status != SeatingPlanStatus.Published)
            return;

        plan.ReopenDraft();
        await seatingPlanRepository.UpdateAsync(plan, autoSave: true);
    }

    private async Task<Exam> GetScopedExamAsync(Guid examId)
    {
        var exam = await examRepository.GetAsync(examId, includeDetails: true);
        if (!CurrentUser.Id.HasValue)
        {
            throw new AbpAuthorizationException("An authenticated user is required to manage examination seating.");
        }

        var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
        if (CanAccessExam(exam, scope) || await CurrentUserIsPrimaryTeacherForAllExamSubjectsAsync(exam))
        {
            return exam;
        }

        throw new AbpAuthorizationException("You are not authorized to manage seating for this examination.");
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

    private async Task<List<Guid>> GetCurrentPrimaryTeacherSubjectIdsAsync(Guid standardId)
    {
        if (!CurrentUser.Id.HasValue)
        {
            return [];
        }

        var currentEmployee = await employeeRepository.FirstOrDefaultAsync(
            employee => employee.UserId == CurrentUser.Id.Value);
        if (currentEmployee == null)
        {
            return [];
        }

        var assignmentsWithTeachers = await assignedSubjectRepository.WithDetailsAsync(
            assignment => assignment.Teachers);
        return await AsyncExecuter.ToListAsync(assignmentsWithTeachers
            .Where(assignment =>
                assignment.StandardId == standardId &&
                assignment.Status == SubjectStatus.Published &&
                assignment.Teachers.Any(teacher =>
                    teacher.TeacherType == SubjectTeacherType.Primary &&
                    teacher.TeacherId == currentEmployee.Id))
            .Select(assignment => assignment.SubjectId)
            .Distinct());
    }

    private async Task<bool> CurrentUserIsPrimaryTeacherForAllExamSubjectsAsync(Exam exam)
    {
        var examSubjectIds = exam.ExamSubjects.Select(subject => subject.SubjectId).ToHashSet();
        if (examSubjectIds.Count == 0)
        {
            return false;
        }

        var primaryTeacherSubjectIds = await GetCurrentPrimaryTeacherSubjectIdsAsync(exam.StandardId);
        return examSubjectIds.IsSubsetOf(primaryTeacherSubjectIds);
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
            if (await FindSeatingPublicationAsync(exam.Id) == null)
            {
                await ReopenLegacyPlanDraftAsync(plan);
                await EnsurePlanSubjectsAsync(exam, plan, exam.ExamSubjects.Select(s => s.Id).ToList());
            }
            return plan;
        }

        await ThrowIfExaminationSeatingPublishedAsync(exam.Id);

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
        var requested = examSubjectIds.ToHashSet();
        var otherPlans = await seatingPlanRepository.GetListAsync(p =>
            p.ExamId == examId &&
            (!currentPlanId.HasValue || p.Id != currentPlanId.Value));
        if (otherPlans.Count == 0)
            return;

        var otherPlanIds = otherPlans.Select(p => p.Id).ToHashSet();
        var otherSubjects = await seatingPlanSubjectRepository.GetListAsync(s =>
            s.ExamId == examId && otherPlanIds.Contains(s.SeatingPlanId));
        if (otherSubjects.Any(s => requested.Contains(s.ExamSubjectId)))
            throw new UserFriendlyException("One or more selected subjects already belong to another seating plan. Open that plan or choose different subjects.");
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
            LastModifiedTime = plan.LastModificationTime,
            Version = plan.ConcurrencyStamp
        };
    }

    public async Task<SeatingPlanDraftResponseDto> SaveSeatingPlanDraftAsync(Guid examId, SaveSeatingPlanDraftInputDto input)
    {
        var exam = await GetScopedExamAsync(examId);
        await ThrowIfExaminationSeatingPublishedAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);

        if (!string.IsNullOrWhiteSpace(input.Version) &&
            !string.Equals(plan.ConcurrencyStamp, input.Version, StringComparison.Ordinal))
        {
            throw new UserFriendlyException(
                "This seating plan was modified by someone else since you last loaded it. Refresh to see the latest changes before saving.")
            {
                Code = "SEATING_PLAN_CONCURRENCY_CONFLICT"
            };
        }

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

        await ReopenLegacyPlanDraftAsync(plan);

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
        await ThrowIfExaminationSeatingPublishedAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);

        if (!string.IsNullOrWhiteSpace(input.Version) &&
            !string.Equals(plan.ConcurrencyStamp, input.Version, StringComparison.Ordinal))
        {
            throw new UserFriendlyException(
                "This seating plan was modified by someone else since you last loaded it. Refresh to see the latest changes before saving.")
            { Code = "SEATING_PLAN_CONCURRENCY_CONFLICT" };
        }

        if (input.LastKnownModifiedTime.HasValue && plan.LastModificationTime.HasValue &&
            plan.LastModificationTime.Value != input.LastKnownModifiedTime.Value)
        {
            throw new UserFriendlyException("This seating plan was modified by someone else since you last loaded it. Refresh to see the latest changes before saving.")
            { Code = "SEATING_PLAN_CONCURRENCY_CONFLICT" };
        }
        await ReopenLegacyPlanDraftAsync(plan);

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

        // Free the unique room/seat keys before applying replacements. Updating two occupied
        // seats directly (including a swap) creates a cycle in EF's unique-index ordering.
        var submittedRoomIds = input.Rooms.Select(room => room.RoomId).ToHashSet();
        if (submittedRoomIds.Count > 0)
        {
            List<ExamSeatAllocation> existingInSubmittedRooms;
            using (dataFilter.Disable<ISoftDelete>())
            {
                existingInSubmittedRooms = await seatAllocationRepository.GetListAsync(allocation =>
                    allocation.ExamId == examId && allocation.SeatingPlanId == plan.Id &&
                    Enumerable.Contains(submittedRoomIds, allocation.RoomId));

                for (var index = 0; index < existingInSubmittedRooms.Count; index++)
                {
                    var allocation = existingInSubmittedRooms[index];
                    allocation.Update(plan.Id, allocation.RoomId, int.MinValue + index,
                        allocation.BenchId, allocation.IsRoomAssign);
                }

                if (existingInSubmittedRooms.Count > 0)
                    await seatAllocationRepository.UpdateManyAsync(existingInSubmittedRooms, autoSave: true);
            }
        }

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
        await ThrowIfExaminationSeatingPublishedAsync(examId);
        var examSubjectIds = NormalizeExamSubjectIds(exam, input.ScopeType, input.ExamSubjectIds);

        ExamSeatingPlan plan;
        if (input.SeatingPlanId.HasValue)
        {
            plan = await seatingPlanRepository.FindAsync(p => p.ExamId == examId && p.Id == input.SeatingPlanId.Value)
                ?? throw new UserFriendlyException("Seating plan not found.");
            await ReopenLegacyPlanDraftAsync(plan);

            var currentSubjects = await GetPlanSubjectsAsync(plan.Id);
            var allocatedCount = await seatAllocationRepository.CountAsync(a => a.ExamId == examId && a.SeatingPlanId == plan.Id);
            var subjectSetChanged = plan.ScopeType != input.ScopeType ||
                HasSubjectSetChanged(currentSubjects, examSubjectIds);
            if (subjectSetChanged && allocatedCount > 0)
            {
                if (!input.ResetAllocationsWhenSubjectsChange)
                    throw new UserFriendlyException("Changing subjects clears this plan's seat allocations. Confirm the reset and try again.");

                var allocations = await seatAllocationRepository.GetListAsync(allocation =>
                    allocation.ExamId == examId && allocation.SeatingPlanId == plan.Id);
                if (allocations.Count > 0)
                    await seatAllocationRepository.DeleteManyAsync(allocations, autoSave: true);
                plan.SaveDraft(string.Empty, "{}", string.Empty, string.Empty, SeatingPlanStatus.NotStarted);
            }

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
        await GetScopedExamAsync(examId);
        await ThrowIfExaminationSeatingPublishedAsync(examId);
        var plan = await seatingPlanRepository.FindAsync(p => p.ExamId == examId && p.Id == seatingPlanId)
            ?? throw new UserFriendlyException("Seating plan not found.");
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

    private static string BuildSessionKey(ExamSubject subject)
    {
        if (!subject.ExamDate.HasValue || !subject.StartTime.HasValue || !subject.EndTime.HasValue)
            return $"unscheduled:{subject.Id:N}";

        return string.Join(
            "|",
            subject.ExamDate.Value.ToString("yyyy-MM-dd"),
            subject.StartTime.Value.ToString("HH:mm:ss"),
            subject.EndTime.Value.ToString("HH:mm:ss"));
    }

    /// <summary>
    /// Builds the V2 guided workspace without creating or mutating a legacy default plan.
    /// All summary values for a session come from the same plan/allocation snapshot.
    /// </summary>
    public async Task<SeatingPlanWorkspaceDto> GetSeatingPlanWorkspaceAsync(
        Guid examId,
        Guid? seatingPlanId = null)
    {
        var exam = await GetScopedExamAsync(examId);
        var isExaminationPublished = await FindSeatingPublicationAsync(examId) != null;
        var standard = await standardRepository.FindAsync(exam.StandardId);
        var subjectDtos = await GetExamSubjectDtosAsync(exam);
        var subjectDtoById = subjectDtos.ToDictionary(subject => subject.ExamSubjectId);
        var examSubjectsById = exam.ExamSubjects.ToDictionary(subject => subject.Id);

        var plans = await seatingPlanRepository.GetListAsync(plan => plan.ExamId == examId);
        if (seatingPlanId.HasValue && plans.All(plan => plan.Id != seatingPlanId.Value))
            throw new UserFriendlyException("Seating plan not found for this exam.");

        var planIds = plans.Select(plan => plan.Id).ToHashSet();
        var planSubjects = planIds.Count == 0
            ? []
            : await seatingPlanSubjectRepository.GetListAsync(subject =>
                subject.ExamId == examId && planIds.Contains(subject.SeatingPlanId));
        var subjectIdsByPlan = planSubjects
            .GroupBy(subject => subject.SeatingPlanId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(subject => subject.ExamSubjectId).ToHashSet());

        var allocations = planIds.Count == 0
            ? []
            : await seatAllocationRepository.GetListAsync(allocation =>
                allocation.ExamId == examId && planIds.Contains(allocation.SeatingPlanId));
        var allocationsByPlan = allocations
            .GroupBy(allocation => allocation.SeatingPlanId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var allDivisionIds = exam.ExamDivisions.Select(division => division.DivisionId)
            .Concat(exam.ExamSubjects.SelectMany(subject => subject.RoomAssignments).Select(row => row.DivisionId))
            .Distinct()
            .ToList();
        var divisions = allDivisionIds.Count == 0
            ? []
            : await divisionRepository.GetListAsync(division => allDivisionIds.Contains(division.Id));
        var divisionsById = divisions.ToDictionary(division => division.Id);
        var students = allDivisionIds.Count == 0
            ? []
            : await studentRepository.GetListAsync(student =>
                student.StandardId == exam.StandardId &&
                student.DivisionId.HasValue &&
                allDivisionIds.Contains(student.DivisionId.Value));

        var availableRoomIds = exam.ExamSubjects
            .SelectMany(subject => subject.RoomAssignments)
            .SelectMany(row => row.RoomIds)
            .Concat(plans.SelectMany(plan => ParseGuidCsv(plan.RoomIdsCsv)))
            .Distinct()
            .ToList();
        var rooms = availableRoomIds.Count == 0
            ? []
            : await roomRepository.GetListAsync(room => availableRoomIds.Contains(room.Id));
        var roomsById = rooms.ToDictionary(room => room.Id);

        var sessions = new List<SeatingPlanSessionDto>();
        var sessionGroups = exam.ExamSubjects
            .GroupBy(BuildSessionKey)
            .OrderBy(group => group.Min(subject => subject.ExamDate ?? DateTime.MaxValue))
            .ThenBy(group => group.Min(subject => subject.StartTime ?? DateTime.MaxValue))
            .ToList();

        foreach (var group in sessionGroups)
        {
            var sessionSubjects = group.ToList();
            var sessionSubjectIds = sessionSubjects.Select(subject => subject.Id).ToHashSet();
            var exactPlans = plans
                .Where(plan => subjectIdsByPlan.TryGetValue(plan.Id, out var ids) && ids.SetEquals(sessionSubjectIds))
                .OrderBy(plan => plan.ScopeType == SeatingPlanScopeType.AllSubjects ? 1 : 0)
                .ThenBy(plan => plan.CreationTime)
                .ToList();
            var sessionPlan = exactPlans.FirstOrDefault();

            var overlappingPlans = plans
                .Where(plan => subjectIdsByPlan.TryGetValue(plan.Id, out var ids) &&
                    PlanSchedulesOverlap(sessionSubjectIds, ids, examSubjectsById))
                .ToList();
            var requiresMigration = exactPlans.Count > 1 || overlappingPlans.Any(plan =>
                plan.Id != sessionPlan?.Id &&
                (plan.ScopeType != SeatingPlanScopeType.AllSubjects ||
                 allocationsByPlan.GetValueOrDefault(plan.Id)?.Count > 0));

            var sessionDivisionIds = sessionSubjects
                .SelectMany(subject => subject.RoomAssignments)
                .Select(row => row.DivisionId)
                .Distinct()
                .ToList();
            if (sessionDivisionIds.Count == 0)
                sessionDivisionIds = exam.ExamDivisions.Select(division => division.DivisionId).Distinct().ToList();

            var totalStudents = students.Count(student =>
                student.DivisionId.HasValue && sessionDivisionIds.Contains(student.DivisionId.Value));
            var planAllocations = sessionPlan == null
                ? []
                : allocationsByPlan.GetValueOrDefault(sessionPlan.Id) ?? [];
            if (sessionPlan != null)
                totalStudents = (await GetEligibleStudentsForPlanAsync(exam, sessionPlan)).Count;

            var selectedRoomIds = sessionPlan == null
                ? []
                : ParseGuidCsv(sessionPlan.RoomIdsCsv).Distinct().ToList();
            var configuredRoomIds = sessionSubjects
                .SelectMany(subject => subject.RoomAssignments)
                .SelectMany(row => row.RoomIds)
                .Distinct()
                .ToList();
            var capacityRoomIds = selectedRoomIds.Count > 0 ? selectedRoomIds : configuredRoomIds;
            var usableCapacity = capacityRoomIds
                .Where(roomsById.ContainsKey)
                .Sum(roomId => UsableCapacity(roomsById[roomId]));
            var roomsUsed = selectedRoomIds.Count > 0
                ? selectedRoomIds.Count
                : planAllocations.Select(allocation => allocation.RoomId).Distinct().Count();
            var allocatedStudents = planAllocations.Select(allocation => allocation.StudentId).Distinct().Count();
            var hasCompleteSchedule = sessionSubjects.All(subject =>
                subject.ExamDate.HasValue && subject.StartTime.HasValue && subject.EndTime.HasValue);

            var issues = new List<SeatingWorkspaceIssueDto>();
            if (!hasCompleteSchedule)
                issues.Add(new SeatingWorkspaceIssueDto
                {
                    Code = "INCOMPLETE_SCHEDULE",
                    Severity = "blocker",
                    Title = "Complete the examination schedule",
                    Message = "Every subject in this session needs a date, start time, and end time before seating can be prepared.",
                    ActionCode = "OPEN_EXAM_SCHEDULE",
                    ActionLabel = "Complete schedule"
                });
            if (totalStudents == 0)
                issues.Add(new SeatingWorkspaceIssueDto
                {
                    Code = "NO_ELIGIBLE_STUDENTS",
                    Severity = "blocker",
                    Title = "No eligible students found",
                    Message = "Check the selected divisions and subject eligibility before preparing seating.",
                    ActionCode = "REVIEW_ELIGIBILITY",
                    ActionLabel = "Review students"
                });
            if (configuredRoomIds.Count == 0)
                issues.Add(new SeatingWorkspaceIssueDto
                {
                    Code = "NO_ROOMS_CONFIGURED",
                    Severity = "blocker",
                    Title = "Configure rooms for this session",
                    Message = "The scheduled subjects do not have any examination rooms available.",
                    ActionCode = "OPEN_ROOM_ASSIGNMENTS",
                    ActionLabel = "Configure rooms"
                });
            if (usableCapacity > 0 && usableCapacity < totalStudents)
                issues.Add(new SeatingWorkspaceIssueDto
                {
                    Code = "ROOM_CAPACITY_SHORTAGE",
                    Severity = "blocker",
                    Title = "More room capacity is required",
                    Message = $"This session needs {totalStudents} seats but the configured rooms provide {usableCapacity} usable seats.",
                    ActionCode = "CHOOSE_ROOMS",
                    ActionLabel = "Choose more rooms"
                });
            if (requiresMigration)
                issues.Add(new SeatingWorkspaceIssueDto
                {
                    Code = "LEGACY_PLAN_REQUIRES_MIGRATION",
                    Severity = "blocker",
                    Title = "Review an overlapping legacy plan",
                    Message = "An existing plan covers this time together with another subject or session. Review it before preparing the guided session plan.",
                    ActionCode = "REVIEW_LEGACY_PLAN",
                    ActionLabel = "Review existing plan",
                    RelatedSeatingPlanId = overlappingPlans.FirstOrDefault(plan => plan.Id != sessionPlan?.Id)?.Id
                });

            sessions.Add(new SeatingPlanSessionDto
            {
                SessionKey = group.Key,
                ExamDate = sessionSubjects.FirstOrDefault()?.ExamDate,
                StartTime = sessionSubjects.FirstOrDefault()?.StartTime,
                EndTime = sessionSubjects.FirstOrDefault()?.EndTime,
                HasCompleteSchedule = hasCompleteSchedule,
                Subjects = sessionSubjects.Select(subject => subjectDtoById[subject.Id]).ToList(),
                Divisions = sessionDivisionIds
                    .Where(divisionsById.ContainsKey)
                    .Select(divisionId => new DivisionLiteDto
                    {
                        Id = divisionId,
                        Name = divisionsById[divisionId].Name
                    })
                    .OrderBy(division => division.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                AvailableRooms = configuredRoomIds
                    .Where(roomsById.ContainsKey)
                    .Select(roomId => roomsById[roomId])
                    .OrderBy(room => room.RoomName, StringComparer.OrdinalIgnoreCase)
                    .Select(room => new SeatingPlanRoomOptionDto
                    {
                        RoomId = room.Id,
                        RoomName = room.RoomName,
                        Capacity = room.Capacity,
                        UsableCapacity = UsableCapacity(room),
                        DisabledSeats = ParseIntCsv(room.DisabledSeatNumbersCsv).Count
                    })
                    .ToList(),
                SelectedRoomIds = selectedRoomIds,
                SeatingPlanId = sessionPlan?.Id,
                SeatingPlanName = sessionPlan?.Name,
                Status = sessionPlan == null
                    ? SeatingPlanStatus.NotStarted
                    : ComputeStatus(
                        totalStudents,
                        allocatedStudents,
                        isExaminationPublished,
                        isExaminationPublished),
                IsLocked = isExaminationPublished,
                RequiresMigration = requiresMigration,
                CanPrepare = !isExaminationPublished && hasCompleteSchedule && totalStudents > 0 &&
                    configuredRoomIds.Count > 0 && !requiresMigration,
                TotalStudents = totalStudents,
                AllocatedStudents = allocatedStudents,
                PendingStudents = Math.Max(0, totalStudents - allocatedStudents),
                RoomsUsed = roomsUsed,
                UsableCapacity = usableCapacity,
                EmptySeats = Math.Max(0, usableCapacity - allocatedStudents),
                Utilization = usableCapacity == 0
                    ? 0
                    : (int)Math.Round(allocatedStudents / (double)usableCapacity * 100),
                Issues = issues
            });
        }

        var subjectOwnerCounts = planSubjects
            .GroupBy(subject => subject.ExamSubjectId)
            .ToDictionary(group => group.Key, group => group.Select(subject => subject.SeatingPlanId).Distinct().Count());
        var planGroups = new List<SeatingPlanGroupDto>();
        foreach (var plan in plans.OrderBy(plan => plan.CreationTime))
        {
            var planSubjectIds = subjectIdsByPlan.GetValueOrDefault(plan.Id) ?? [];
            if (plan.ScopeType == SeatingPlanScopeType.AllSubjects && planSubjectIds.Count == 0)
                planSubjectIds = exam.ExamSubjects.Select(subject => subject.Id).ToHashSet();

            var planExamSubjects = exam.ExamSubjects
                .Where(subject => planSubjectIds.Contains(subject.Id))
                .ToList();
            var planSubjectDtos = planExamSubjects
                .Where(subject => subjectDtoById.ContainsKey(subject.Id))
                .Select(subject => subjectDtoById[subject.Id])
                .OrderBy(subject => subject.ExamDate ?? DateTime.MaxValue)
                .ThenBy(subject => subject.StartTime ?? DateTime.MaxValue)
                .ThenBy(subject => subject.SubjectName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var planDivisionIds = planExamSubjects
                .SelectMany(subject => subject.RoomAssignments)
                .Select(row => row.DivisionId)
                .Distinct()
                .ToList();
            if (planDivisionIds.Count == 0)
                planDivisionIds = exam.ExamDivisions.Select(division => division.DivisionId).Distinct().ToList();

            var planAllocations = allocationsByPlan.GetValueOrDefault(plan.Id) ?? [];
            var totalStudents = (await GetEligibleStudentsForPlanAsync(exam, plan)).Count;
            var allocatedStudents = planAllocations.Select(allocation => allocation.StudentId).Distinct().Count();
            var selectedRoomIds = ParseGuidCsv(plan.RoomIdsCsv).Distinct().ToList();
            var configuredRoomIds = planExamSubjects
                .SelectMany(subject => subject.RoomAssignments)
                .SelectMany(row => row.RoomIds)
                .Distinct()
                .ToList();
            var availablePlanRoomIds = configuredRoomIds.Concat(selectedRoomIds).Distinct().ToList();
            var capacityRoomIds = selectedRoomIds.Count > 0 ? selectedRoomIds : configuredRoomIds;
            var usableCapacity = capacityRoomIds
                .Where(roomsById.ContainsKey)
                .Sum(roomId => UsableCapacity(roomsById[roomId]));
            var roomsUsed = selectedRoomIds.Count > 0
                ? selectedRoomIds.Count
                : planAllocations.Select(allocation => allocation.RoomId).Distinct().Count();
            var hasCompleteSchedule = planExamSubjects.Count > 0 && planExamSubjects.All(subject =>
                subject.ExamDate.HasValue && subject.StartTime.HasValue && subject.EndTime.HasValue);
            var hasDuplicateOwnership = planSubjectIds.Any(subjectId =>
                subjectOwnerCounts.GetValueOrDefault(subjectId, 0) > 1);

            var issues = new List<SeatingWorkspaceIssueDto>();
            if (!hasCompleteSchedule)
                issues.Add(new SeatingWorkspaceIssueDto
                {
                    Code = "INCOMPLETE_SCHEDULE",
                    Severity = "blocker",
                    Title = "Complete the examination schedule",
                    Message = "Every selected subject needs a date, start time, and end time before seating can be configured.",
                    ActionCode = "OPEN_EXAM_SCHEDULE",
                    ActionLabel = "Complete schedule"
                });
            if (totalStudents == 0)
                issues.Add(new SeatingWorkspaceIssueDto
                {
                    Code = "NO_ELIGIBLE_STUDENTS",
                    Severity = "blocker",
                    Title = "No eligible students found",
                    Message = "Check the divisions and subject eligibility for this plan.",
                    ActionCode = "REVIEW_ELIGIBILITY",
                    ActionLabel = "Review students"
                });
            if (availablePlanRoomIds.Count == 0)
                issues.Add(new SeatingWorkspaceIssueDto
                {
                    Code = "NO_ROOMS_CONFIGURED",
                    Severity = "blocker",
                    Title = "Configure examination rooms",
                    Message = "None of the selected subjects has an examination room available.",
                    ActionCode = "OPEN_ROOM_ASSIGNMENTS",
                    ActionLabel = "Configure rooms"
                });
            if (usableCapacity > 0 && usableCapacity < totalStudents)
                issues.Add(new SeatingWorkspaceIssueDto
                {
                    Code = "ROOM_CAPACITY_SHORTAGE",
                    Severity = "blocker",
                    Title = "More room capacity is required",
                    Message = $"This plan needs {totalStudents} seats but the selected rooms provide {usableCapacity} usable seats.",
                    ActionCode = "CHOOSE_ROOMS",
                    ActionLabel = "Choose more rooms"
                });
            if (hasDuplicateOwnership)
                issues.Add(new SeatingWorkspaceIssueDto
                {
                    Code = "DUPLICATE_SUBJECT_OWNERSHIP",
                    Severity = "blocker",
                    Title = "A subject belongs to more than one plan",
                    Message = "Move each subject into exactly one active seating plan before continuing.",
                    ActionCode = "REVIEW_PLAN_SUBJECTS",
                    ActionLabel = "Review plans"
                });

            var status = ComputeStatus(
                totalStudents,
                allocatedStudents,
                isExaminationPublished,
                isExaminationPublished);
            if (status != SeatingPlanStatus.Published && issues.Any(issue => issue.Severity == "blocker"))
                status = SeatingPlanStatus.NeedsAttention;

            planGroups.Add(new SeatingPlanGroupDto
            {
                SeatingPlanId = plan.Id,
                Name = plan.Name,
                ScopeType = plan.ScopeType,
                Status = status,
                IsLocked = isExaminationPublished,
                HasCompleteSchedule = hasCompleteSchedule,
                CanConfigure = !isExaminationPublished && hasCompleteSchedule && totalStudents > 0 &&
                    availablePlanRoomIds.Count > 0 && !hasDuplicateOwnership,
                ScheduleCount = planExamSubjects.Select(BuildSessionKey).Distinct().Count(),
                Subjects = planSubjectDtos,
                Divisions = planDivisionIds
                    .Where(divisionsById.ContainsKey)
                    .Select(divisionId => new DivisionLiteDto
                    {
                        Id = divisionId,
                        Name = divisionsById[divisionId].Name
                    })
                    .OrderBy(division => division.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                AvailableRooms = availablePlanRoomIds
                    .Where(roomsById.ContainsKey)
                    .Select(roomId => roomsById[roomId])
                    .OrderBy(room => room.RoomName, StringComparer.OrdinalIgnoreCase)
                    .Select(room => new SeatingPlanRoomOptionDto
                    {
                        RoomId = room.Id,
                        RoomName = room.RoomName,
                        Capacity = room.Capacity,
                        UsableCapacity = UsableCapacity(room),
                        DisabledSeats = ParseIntCsv(room.DisabledSeatNumbersCsv).Count
                    })
                    .ToList(),
                SelectedRoomIds = selectedRoomIds,
                TotalStudents = totalStudents,
                AllocatedStudents = allocatedStudents,
                PendingStudents = Math.Max(0, totalStudents - allocatedStudents),
                RoomsUsed = roomsUsed,
                UsableCapacity = usableCapacity,
                EmptySeats = Math.Max(0, usableCapacity - allocatedStudents),
                Utilization = usableCapacity == 0
                    ? 0
                    : (int)Math.Round(allocatedStudents / (double)usableCapacity * 100),
                Issues = issues
            });
        }

        var selectedPlan = seatingPlanId.HasValue
            ? plans.First(plan => plan.Id == seatingPlanId.Value)
            : null;

        return new SeatingPlanWorkspaceDto
        {
            ExamId = exam.Id,
            ExamName = exam.ExamName,
            StandardId = exam.StandardId,
            StandardName = standard?.Name ?? string.Empty,
            SelectedSeatingPlanId = selectedPlan?.Id,
            Version = selectedPlan?.ConcurrencyStamp,
            AvailableSubjects = subjectDtos,
            Plans = planGroups,
            Sessions = sessions,
            Readiness = await BuildExaminationSeatingReadinessAsync(exam)
        };
    }

    [UnitOfWork]
    public async Task<SeatingPlanWorkspaceDto> PrepareSeatingPlanAsync(
        Guid examId,
        PrepareSeatingPlanInputDto input)
    {
        return await PrepareSeatingPlanCoreAsync(examId, input.Name, input.ExamSubjectIds);
    }

    /// <summary>
    /// Compatibility entry point for the former session-first client. The subject
    /// selection is intentionally no longer expanded to the complete time slot.
    /// </summary>
    [UnitOfWork]
    public async Task<SeatingPlanWorkspaceDto> PrepareSessionSeatingPlanAsync(
        Guid examId,
        PrepareSessionSeatingPlanInputDto input)
    {
        var workspace = await PrepareSeatingPlanCoreAsync(examId, input.Name, input.ExamSubjectIds);
        var exam = await GetScopedExamAsync(examId);
        var selectedSubjects = exam.ExamSubjects
            .Where(subject => input.ExamSubjectIds.Contains(subject.Id))
            .ToList();
        var sessionKeys = selectedSubjects.Select(BuildSessionKey).Distinct().ToList();
        if (workspace.SelectedSeatingPlanId.HasValue && sessionKeys.Count == 1 &&
            !sessionKeys[0].StartsWith("unscheduled:", StringComparison.Ordinal))
        {
            var plan = await seatingPlanRepository.GetAsync(workspace.SelectedSeatingPlanId.Value);
            if (string.IsNullOrWhiteSpace(plan.SessionKey))
            {
                plan.AssignSessionKey(sessionKeys[0]);
                await seatingPlanRepository.UpdateAsync(plan, autoSave: true);
            }
            return await GetSeatingPlanWorkspaceAsync(examId, plan.Id);
        }

        return workspace;
    }

    private async Task<SeatingPlanWorkspaceDto> PrepareSeatingPlanCoreAsync(
        Guid examId,
        string? requestedName,
        IReadOnlyCollection<Guid> requestedExamSubjectIds)
    {
        var exam = await GetScopedExamAsync(examId);
        await ThrowIfExaminationSeatingPublishedAsync(examId);
        var requestedIds = requestedExamSubjectIds.Distinct().ToHashSet();
        var requestedSubjects = exam.ExamSubjects.Where(subject => requestedIds.Contains(subject.Id)).ToList();
        if (requestedSubjects.Count != requestedIds.Count)
            throw new UserFriendlyException("One or more selected subjects do not belong to this exam.");
        if (requestedSubjects.Count == 0)
            throw new UserFriendlyException("Select at least one subject for the seating plan.");
        if (requestedSubjects.Any(subject =>
            !subject.ExamDate.HasValue || !subject.StartTime.HasValue || !subject.EndTime.HasValue))
            throw new UserFriendlyException("Complete the examination date and time for every selected subject before preparing seating.");

        var plans = await seatingPlanRepository.GetListAsync(plan => plan.ExamId == examId);
        var planIds = plans.Select(plan => plan.Id).ToHashSet();
        var planSubjects = planIds.Count == 0
            ? []
            : await seatingPlanSubjectRepository.GetListAsync(subject =>
                subject.ExamId == examId && planIds.Contains(subject.SeatingPlanId));
        var subjectIdsByPlan = planSubjects
            .GroupBy(subject => subject.SeatingPlanId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(subject => subject.ExamSubjectId).ToHashSet());

        var existing = plans.FirstOrDefault(plan =>
            subjectIdsByPlan.TryGetValue(plan.Id, out var ids) && ids.SetEquals(requestedIds));
        if (existing != null)
            return await GetSeatingPlanWorkspaceAsync(examId, existing.Id);

        var scopeType = requestedIds.Count == 1
            ? SeatingPlanScopeType.SingleSubject
            : SeatingPlanScopeType.SelectedSubjects;
        await EnsureSubjectWiseSubjectsAreAvailableAsync(examId, null, scopeType, requestedIds);

        var subjectDtos = await GetExamSubjectDtosAsync(exam);
        var selectedSubjectNames = subjectDtos
            .Where(subject => requestedIds.Contains(subject.ExamSubjectId))
            .Select(subject => subject.SubjectName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        var defaultName = selectedSubjectNames.Count == 1
            ? selectedSubjectNames[0]
            : $"{selectedSubjectNames.Count} subjects · Shared plan";
        var planName = string.IsNullOrWhiteSpace(requestedName) ? defaultName : requestedName.Trim();
        if (planName.Length > 128)
            throw new UserFriendlyException("The seating plan name cannot exceed 128 characters.");

        var plan = new ExamSeatingPlan(
            guidGenerator.Create(),
            CurrentTenant.Id,
            exam.Id,
            planName,
            scopeType);
        await seatingPlanRepository.InsertAsync(plan, autoSave: true);
        await EnsurePlanSubjectsAsync(exam, plan, requestedIds.ToList());
        await auditLogRepository.InsertAsync(new SeatingPlanAuditLog(
            guidGenerator.Create(),
            CurrentTenant.Id,
            examId,
            plan.Id,
            SeatingPlanAuditAction.SavedDraft,
            CurrentUser.Id ?? Guid.Empty,
            CurrentUser.Name,
            requestedIds.Count == 1
                ? "Prepared an individual-subject examination seating plan."
                : $"Prepared a shared examination seating plan for {requestedIds.Count} subjects.",
            null), autoSave: true);

        return await GetSeatingPlanWorkspaceAsync(examId, plan.Id);
    }

    private async Task<ExaminationSeatingReadinessDto> BuildExaminationSeatingReadinessAsync(Exam exam)
    {
        var publication = await FindSeatingPublicationAsync(exam.Id);
        var plans = await seatingPlanRepository.GetListAsync(plan => plan.ExamId == exam.Id);
        var planIds = plans.Select(plan => plan.Id).ToHashSet();
        var planSubjects = planIds.Count == 0
            ? []
            : await seatingPlanSubjectRepository.GetListAsync(subject =>
                subject.ExamId == exam.Id && planIds.Contains(subject.SeatingPlanId));
        var subjectDtos = await GetExamSubjectDtosAsync(exam);
        var subjectNamesById = subjectDtos.ToDictionary(subject => subject.ExamSubjectId, subject => subject.SubjectName);
        var readiness = new ExaminationSeatingReadinessDto
        {
            ExamId = exam.Id,
            IsPublished = publication != null,
            CanResetDraft = publication == null && plans.Count > 0,
            TotalSubjectCount = exam.ExamSubjects.Count,
            PlannedSubjectCount = planSubjects.Select(subject => subject.ExamSubjectId).Distinct().Count(),
            TotalPlanCount = plans.Count,
            PublishedAt = publication?.PublishedAt,
            PublishedByUserId = publication?.PublishedByUserId,
            PublicationVersion = publication?.ConcurrencyStamp
        };

        foreach (var plan in plans)
        {
            var (total, allocated) = await ComputeCountsAsync(exam, plan.Id);
            readiness.TotalStudentSeats += total;
            readiness.AllocatedStudentSeats += allocated;
        }

        if (publication != null)
        {
            readiness.ReadyPlanCount = plans.Count;
            readiness.IsReadyToPublish = true;
            return readiness;
        }

        if (exam.ExamSubjects.Count == 0)
        {
            readiness.Blockers.Add(new ExaminationSeatingIssueDto
            {
                Code = "NO_EXAM_SUBJECTS",
                Message = "Add at least one scheduled subject before preparing examination seating."
            });
        }

        if (plans.Count == 0)
        {
            readiness.Blockers.Add(new ExaminationSeatingIssueDto
            {
                Code = "NO_SEATING_PLANS",
                Message = "Create seating plans for every scheduled subject before publishing."
            });
        }

        var duplicateOwnership = planSubjects
            .GroupBy(subject => subject.ExamSubjectId)
            .Where(group => group.Select(subject => subject.SeatingPlanId).Distinct().Count() > 1)
            .ToList();
        foreach (var duplicate in duplicateOwnership)
        {
            readiness.Blockers.Add(new ExaminationSeatingIssueDto
            {
                Code = "DUPLICATE_SUBJECT_OWNERSHIP",
                Message = $"{subjectNamesById.GetValueOrDefault(duplicate.Key, "A subject")} belongs to more than one seating plan.",
                ExamSubjectIds = [duplicate.Key]
            });
        }

        var plannedSubjectIds = planSubjects.Select(subject => subject.ExamSubjectId).ToHashSet();
        var unplannedSubjectIds = exam.ExamSubjects
            .Select(subject => subject.Id)
            .Where(subjectId => !plannedSubjectIds.Contains(subjectId))
            .ToList();
        if (unplannedSubjectIds.Count > 0)
        {
            var names = unplannedSubjectIds
                .Select(subjectId => subjectNamesById.GetValueOrDefault(subjectId, "Subject"));
            readiness.Blockers.Add(new ExaminationSeatingIssueDto
            {
                Code = "UNPLANNED_SUBJECTS",
                Message = $"Assign every subject before publishing. Still unplanned: {string.Join(", ", names)}.",
                ExamSubjectIds = unplannedSubjectIds
            });
        }

        foreach (var plan in plans)
        {
            var ownedSubjectIds = planSubjects
                .Where(subject => subject.SeatingPlanId == plan.Id)
                .Select(subject => subject.ExamSubjectId)
                .Distinct()
                .ToList();
            if (ownedSubjectIds.Count == 0)
            {
                readiness.Blockers.Add(new ExaminationSeatingIssueDto
                {
                    Code = "PLAN_WITHOUT_SUBJECTS",
                    Message = $"{plan.Name} does not contain any examination subject.",
                    SeatingPlanId = plan.Id,
                    SeatingPlanName = plan.Name
                });
                continue;
            }

            var validation = await ValidateSeatingPlanAsync(exam.Id, plan.Id);
            if (validation.IsValid)
                readiness.ReadyPlanCount++;

            foreach (var blocker in validation.Blockers
                .GroupBy(issue => new { issue.Code, issue.Message })
                .Select(group => group.First()))
            {
                readiness.Blockers.Add(new ExaminationSeatingIssueDto
                {
                    Code = blocker.Code,
                    Message = $"{plan.Name}: {blocker.Message}",
                    SeatingPlanId = plan.Id,
                    SeatingPlanName = plan.Name,
                    ExamSubjectIds = ownedSubjectIds
                });
            }

            foreach (var warning in validation.Warnings
                .GroupBy(issue => new { issue.Code, issue.Message })
                .Select(group => group.First()))
            {
                readiness.Warnings.Add(new ExaminationSeatingIssueDto
                {
                    Code = warning.Code,
                    Message = $"{plan.Name}: {warning.Message}",
                    SeatingPlanId = plan.Id,
                    SeatingPlanName = plan.Name,
                    ExamSubjectIds = ownedSubjectIds
                });
            }
        }

        readiness.IsReadyToPublish = readiness.Blockers.Count == 0 &&
            readiness.TotalSubjectCount > 0 &&
            readiness.PlannedSubjectCount == readiness.TotalSubjectCount &&
            readiness.ReadyPlanCount == readiness.TotalPlanCount;
        return readiness;
    }

    public async Task<ExaminationSeatingReadinessDto> ValidateExaminationSeatingAsync(Guid examId)
    {
        var exam = await GetScopedExamAsync(examId);
        return await BuildExaminationSeatingReadinessAsync(exam);
    }

    [UnitOfWork]
    public async Task<SeatingPlanWorkspaceDto> ResetExaminationSeatingDraftAsync(
        Guid examId,
        ResetExaminationSeatingDraftInputDto input)
    {
        await GetScopedExamAsync(examId);
        await ThrowIfExaminationSeatingPublishedAsync(examId);

        var plans = await seatingPlanRepository.GetListAsync(plan => plan.ExamId == examId);
        var planIds = plans.Select(plan => plan.Id).ToHashSet();
        if (planIds.Count > 0)
        {
            var allocations = await seatAllocationRepository.GetListAsync(allocation =>
                allocation.ExamId == examId && planIds.Contains(allocation.SeatingPlanId));
            var subjects = await seatingPlanSubjectRepository.GetListAsync(subject =>
                subject.ExamId == examId && planIds.Contains(subject.SeatingPlanId));

            foreach (var plan in plans)
            {
                await auditLogRepository.InsertAsync(new SeatingPlanAuditLog(
                    guidGenerator.Create(),
                    CurrentTenant.Id,
                    examId,
                    plan.Id,
                    SeatingPlanAuditAction.DraftReset,
                    CurrentUser.Id ?? Guid.Empty,
                    CurrentUser.Name,
                    input.Reason,
                    await SnapshotAssignmentsJsonAsync(examId, plan.Id)), autoSave: true);
            }

            if (allocations.Count > 0)
                await seatAllocationRepository.DeleteManyAsync(allocations, autoSave: true);
            if (subjects.Count > 0)
                await seatingPlanSubjectRepository.DeleteManyAsync(subjects, autoSave: true);
            await seatingPlanRepository.DeleteManyAsync(plans, autoSave: true);
        }

        await CurrentUnitOfWork!.SaveChangesAsync();
        return await GetSeatingPlanWorkspaceAsync(examId);
    }

    [UnitOfWork]
    public async Task<SeatingPlanWorkspaceDto> PublishExaminationSeatingAsync(
        Guid examId,
        PublishSeatingPlanInputDto input)
    {
        var exam = await GetScopedExamAsync(examId);
        if (await FindSeatingPublicationAsync(examId) != null)
            return await GetSeatingPlanWorkspaceAsync(examId);

        var readiness = await BuildExaminationSeatingReadinessAsync(exam);
        if (!readiness.IsReadyToPublish)
        {
            var blockerSummary = string.Join(
                " ",
                readiness.Blockers.Take(8).Select(issue => $"[{issue.Code}] {issue.Message}"));
            throw new UserFriendlyException(
                $"Cannot publish examination seating while blocking issues remain. {blockerSummary}")
            {
                Code = "EXAMINATION_SEATING_NOT_READY"
            };
        }

        var plans = await seatingPlanRepository.GetListAsync(plan => plan.ExamId == examId);
        var now = DateTime.UtcNow;
        var userId = CurrentUser.Id ?? Guid.Empty;
        foreach (var plan in plans)
        {
            if (!plan.IsLocked)
                plan.Finalize(userId, now);
            if (plan.Status != SeatingPlanStatus.Published)
                plan.Publish(userId, now);

            await auditLogRepository.InsertAsync(new SeatingPlanAuditLog(
                guidGenerator.Create(),
                CurrentTenant.Id,
                examId,
                plan.Id,
                SeatingPlanAuditAction.ExaminationPublished,
                userId,
                CurrentUser.Name,
                "Published with the complete examination seating arrangement.",
                await SnapshotAssignmentsJsonAsync(examId, plan.Id)), autoSave: true);
        }
        await seatingPlanRepository.UpdateManyAsync(plans, autoSave: true);

        await seatingPublicationRepository.InsertAsync(new ExamSeatingPublication(
            guidGenerator.Create(),
            CurrentTenant.Id,
            examId,
            now,
            userId), autoSave: true);

        await seatingPlanNotifier.NotifyAsync(examId, input);
        await CurrentUnitOfWork!.SaveChangesAsync();
        return await GetSeatingPlanWorkspaceAsync(examId);
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
            var relatedPlans = await seatingPlanRepository.GetListAsync(plan => overlappingPlanIds.Contains(plan.Id));
            var relatedPlansById = relatedPlans.ToDictionary(plan => plan.Id);
            var currentSeatKeys = allocations
                .GroupBy(a => (a.RoomId, a.SeatNumber))
                .ToDictionary(g => g.Key, g => g.First());
            var otherAllocationsBySeat = otherAllocations
                .GroupBy(allocation => (allocation.RoomId, allocation.SeatNumber))
                .ToDictionary(group => group.Key, group => group.ToList());
            var currentSchedule = currentPlanSubjectIds
                .Where(examSubjectsById.ContainsKey)
                .Select(subjectId => examSubjectsById[subjectId])
                .FirstOrDefault();

            foreach (var currentSeat in currentSeatKeys)
            {
                if (!otherAllocationsBySeat.TryGetValue(currentSeat.Key, out var conflictingAllocations))
                    continue;

                var relatedAllocation = conflictingAllocations.First();
                relatedPlansById.TryGetValue(relatedAllocation.SeatingPlanId, out var relatedPlan);
                roomsById.TryGetValue(currentSeat.Value.RoomId, out var room);

                blockers.Add(new SeatingValidationIssueDto
                {
                    Code = "ROOM_SEAT_TIME_CONFLICT",
                    Message = $"{room?.RoomName ?? "This room"} seat {currentSeat.Value.SeatNumber} is already used by {relatedPlan?.Name ?? "another seating plan"} at the same examination time.",
                    StudentId = currentSeat.Value.StudentId,
                    RoomId = currentSeat.Value.RoomId,
                    RoomName = room?.RoomName,
                    SeatId = $"{currentSeat.Value.RoomId}:{currentSeat.Value.SeatNumber}",
                    SeatNumber = currentSeat.Value.SeatNumber,
                    RelatedSeatingPlanId = relatedPlan?.Id,
                    RelatedSeatingPlanName = relatedPlan?.Name,
                    ExamDate = currentSchedule?.ExamDate,
                    StartTime = currentSchedule?.StartTime,
                    EndTime = currentSchedule?.EndTime,
                    SuggestedActionCode = "MOVE_TO_NEXT_AVAILABLE_SEAT",
                    SuggestedActionLabel = "Choose the next available seat"
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

        // A retried request must return the authoritative result without duplicating audit rows.
        if (plan.IsLocked)
            return await GetSeatingPlanDraftAsync(examId, plan.Id);

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
        await PublishExaminationSeatingAsync(examId, input);
        return await GetSeatingPlanDraftAsync(examId, plan.Id);
    }

    public async Task<SeatingPlanDraftResponseDto> UnlockSeatingPlanAsync(Guid examId, UnlockSeatingPlanInputDto input, Guid? seatingPlanId = null)
    {
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw new UserFriendlyException("A reason is required to unlock a finalized seating plan.");

        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, seatingPlanId);

        if (await FindSeatingPublicationAsync(examId) != null)
            throw new UserFriendlyException("Published examination seating cannot be unlocked. Create a new examination revision for corrections.")
            {
                Code = "EXAMINATION_SEATING_PUBLISHED"
            };

        plan.ReopenDraft();
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
        await ThrowIfExaminationSeatingPublishedAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);
        var eligibleStudents = await GetEligibleStudentsForPlanAsync(exam, plan);

        var excluded = input.ExcludedStudentIds.ToHashSet();
        var pending = eligibleStudents
            .Where(s => !excluded.Contains(s.Id))
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
        var divisionIds = ordered
            .Where(student => student.DivisionId.HasValue)
            .Select(student => student.DivisionId!.Value)
            .Distinct()
            .ToList();
        var divisions = divisionIds.Count == 0
            ? []
            : await divisionRepository.GetListAsync(division => divisionIds.Contains(division.Id));
        var divisionNamesById = divisions.ToDictionary(division => division.Id, division => division.Name);

        var overlappingPlanOccupied = await GetOtherOverlappingPlanAllocationsAsync(
            examId,
            plan.Id,
            input.RoomIds);

        // Automatic mode rebuilds this plan's proposal from every eligible student. Existing
        // assignments owned by this plan are replaceable draft state; only seats belonging to
        // another overlapping plan must be treated as occupied.
        var occupiedSeatNumbersByRoom = overlappingPlanOccupied
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
                    StudentName = $"{student.FirstName} {student.LastName}".Trim(),
                    RollNo = student.RollNo,
                    DivisionId = student.DivisionId,
                    DivisionName = student.DivisionId.HasValue
                        ? divisionNamesById.GetValueOrDefault(student.DivisionId.Value)
                        : null,
                    RoomId = room.Id,
                    RoomName = room.RoomName,
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

    [UnitOfWork]
    public async Task<SeatingPlanDraftResponseDto> ApplyAutoAllocatePreviewAsync(
        Guid examId,
        ApplyAutoAllocatePreviewInputDto input)
    {
        var exam = await GetScopedExamAsync(examId);
        await ThrowIfExaminationSeatingPublishedAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);
        await ReopenLegacyPlanDraftAsync(plan);

        var planSubjectIds = (await GetPlanSubjectsAsync(plan.Id))
            .Select(row => row.ExamSubjectId)
            .ToHashSet();
        var allowedRoomIds = exam.ExamSubjects
            .Where(subject => planSubjectIds.Contains(subject.Id))
            .SelectMany(subject => subject.RoomAssignments)
            .SelectMany(assignment => assignment.RoomIds)
            .ToHashSet();
        if (input.RoomIds.Any(roomId => !allowedRoomIds.Contains(roomId)) ||
            input.Assignments.Any(row => !input.RoomIds.Contains(row.RoomId)))
            throw new UserFriendlyException("The preview contains a room outside this session plan.");

        var rooms = await roomRepository.GetListAsync(room => input.RoomIds.Contains(room.Id));
        if (rooms.Count != input.RoomIds.Distinct().Count())
            throw new UserFriendlyException("One or more rooms in the preview no longer exist.");
        var seatsByRoom = rooms.ToDictionary(
            room => room.Id,
            room => BuildRoomSeats(room).ToDictionary(seat => seat.SeatNumber));

        var excludedStudentIds = input.ExcludedStudentIds.ToHashSet();
        var eligibleStudentIds = (await GetEligibleStudentsForPlanAsync(exam, plan))
            .Where(student => !excludedStudentIds.Contains(student.Id))
            .Select(student => student.Id)
            .ToHashSet();
        var proposedStudentIds = input.Assignments.Select(row => row.StudentId).ToHashSet();
        if (proposedStudentIds.Count != input.Assignments.Count)
            throw new UserFriendlyException("A student can appear only once in an allocation preview.");
        if (!proposedStudentIds.SetEquals(eligibleStudentIds))
            throw new UserFriendlyException("The automatic preview is incomplete or stale. Generate it again before applying.");

        var proposedSeats = new HashSet<(Guid RoomId, int SeatNumber)>();
        foreach (var proposed in input.Assignments)
        {
            if (!seatsByRoom[proposed.RoomId].TryGetValue(proposed.SeatNumber, out var seat) || seat.IsDisabled)
                throw new UserFriendlyException("The preview contains a seat that is unavailable.");
            if (!proposedSeats.Add((proposed.RoomId, proposed.SeatNumber)))
                throw new UserFriendlyException("A room seat can appear only once in an allocation preview.");
        }

        var overlappingPlanAllocations = await GetOtherOverlappingPlanAllocationsAsync(
            examId,
            plan.Id,
            input.RoomIds);
        var externallyOccupiedSeats = overlappingPlanAllocations
            .Select(allocation => (allocation.RoomId, allocation.SeatNumber))
            .ToHashSet();
        if (proposedSeats.Overlaps(externallyOccupiedSeats))
            throw new UserFriendlyException("A seat in this preview was occupied after the preview was generated. Refresh and try again.");

        List<ExamSeatAllocation> existing;
        using (dataFilter.Disable<ISoftDelete>())
        {
            existing = await seatAllocationRepository.GetListAsync(allocation =>
                allocation.ExamId == examId && allocation.SeatingPlanId == plan.Id);
        }

        var existingByStudent = existing.ToDictionary(row => row.StudentId);
        var activeExisting = existing.Where(row => !row.IsDeleted).ToList();
        var proposalByStudent = input.Assignments.ToDictionary(row => row.StudentId);
        var alreadyMatchesProposal = activeExisting.Count == input.Assignments.Count &&
            activeExisting.All(current =>
                proposalByStudent.TryGetValue(current.StudentId, out var proposed) &&
                current.RoomId == proposed.RoomId &&
                current.SeatNumber == proposed.SeatNumber &&
                current.BenchId == NormalizeBenchId(proposed.BenchId) &&
                current.IsRoomAssign);

        if (!alreadyMatchesProposal)
        {
            // Release every historical seat key first. The plan has unique indexes for both
            // student and room/seat, so this two-phase move also supports swaps and restores of
            // soft-deleted draft rows without transient unique-key failures.
            for (var index = 0; index < existing.Count; index++)
            {
                var current = existing[index];
                current.Update(plan.Id, current.RoomId, int.MinValue + index, current.BenchId, current.IsRoomAssign);
            }

            if (existing.Count > 0)
            {
                using (dataFilter.Disable<ISoftDelete>())
                {
                    await seatAllocationRepository.UpdateManyAsync(existing, autoSave: true);
                }
            }

            var toDelete = activeExisting
                .Where(current => !proposedStudentIds.Contains(current.StudentId))
                .ToList();
            var toUpdate = new List<ExamSeatAllocation>();
            var toInsert = new List<ExamSeatAllocation>();

            foreach (var proposed in input.Assignments)
            {
                var benchId = NormalizeBenchId(proposed.BenchId);
                if (existingByStudent.TryGetValue(proposed.StudentId, out var current))
                {
                    current.IsDeleted = false;
                    current.Update(plan.Id, proposed.RoomId, proposed.SeatNumber, benchId, isRoomAssign: true);
                    toUpdate.Add(current);
                    continue;
                }

                toInsert.Add(new ExamSeatAllocation(
                    guidGenerator.Create(),
                    CurrentTenant.Id,
                    plan.Id,
                    examId,
                    proposed.StudentId,
                    proposed.RoomId,
                    proposed.SeatNumber,
                    benchId,
                    isRoomAssign: true));
            }

            if (toDelete.Count > 0)
                await seatAllocationRepository.DeleteManyAsync(toDelete, autoSave: false);
            if (toUpdate.Count > 0)
                await seatAllocationRepository.UpdateManyAsync(toUpdate, autoSave: false);
            if (toInsert.Count > 0)
                await seatAllocationRepository.InsertManyAsync(toInsert, autoSave: false);
            await CurrentUnitOfWork!.SaveChangesAsync();
        }

        return await SaveSeatingPlanDraftAsync(examId, input);
    }

    private static Guid? NormalizeBenchId(Guid? benchId) => benchId == Guid.Empty ? null : benchId;

    public async Task<SeatLayoutResponseDto> GetSeatLayoutAsync(Guid examId, GetSeatLayoutInput input)
    {
        var exam = await GetScopedExamAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);
        var room = await roomRepository.FindAsync(input.RoomId)
            ?? throw new UserFriendlyException("Room not found.");

        var totalSeats = room.TotalBenches > 0 ? room.TotalBenches * room.BenchCapacity : room.Capacity;

        // Allocations owned by the plan being viewed.
        var internalAllocations = await seatAllocationRepository.GetListAsync(a =>
            a.ExamId == examId &&
            a.SeatingPlanId == plan.Id &&
            a.RoomId == input.RoomId);

        // Seats held by any other plan whose subject schedule overlaps this plan. This includes
        // sibling plans in the same exam as well as plans belonging to another exam.
        var overlappingPlanAllocations = await GetOtherOverlappingPlanAllocationsAsync(
            examId,
            plan.Id,
            [input.RoomId]);

        var allAllocations = internalAllocations.Concat(overlappingPlanAllocations).ToList();

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

        // Include plan and subject context in every occupied seat. The manual editor uses this
        // to explain why a seat is unavailable, and the published view uses it to label a merged
        // room containing students from multiple simultaneous plans.
        var allocationPlanIds = allAllocations
            .Select(allocation => allocation.SeatingPlanId)
            .Distinct()
            .ToList();
        var allocationPlans = allocationPlanIds.Count == 0
            ? []
            : await seatingPlanRepository.GetListAsync(candidate =>
                allocationPlanIds.Contains(candidate.Id));
        var allocationPlanNamesById = allocationPlans.ToDictionary(
            candidate => candidate.Id,
            candidate => candidate.Name);
        var allocationPlanSubjects = allocationPlanIds.Count == 0
            ? []
            : await seatingPlanSubjectRepository.GetListAsync(subject =>
                allocationPlanIds.Contains(subject.SeatingPlanId));
        var allocationSubjectIds = allocationPlanSubjects
            .Select(subject => subject.SubjectId)
            .Distinct()
            .ToList();
        var allocationSubjects = allocationSubjectIds.Count == 0
            ? []
            : await subjectRepository.GetListAsync(subject =>
                allocationSubjectIds.Contains(subject.Id));
        var allocationSubjectNamesById = allocationSubjects.ToDictionary(
            subject => subject.Id,
            subject => subject.Name);
        var allocationSubjectNamesByPlanId = allocationPlanSubjects
            .GroupBy(subject => subject.SeatingPlanId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(subject => allocationSubjectNamesById.GetValueOrDefault(subject.SubjectId))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .Distinct()
                    .OrderBy(name => name)
                    .ToList());

        // Build one item per physical seat. Current-plan allocations were concatenated first,
        // so they remain visible if legacy/inconsistent data contains a duplicate seat; grouping
        // also prevents that old data from turning this read-only endpoint into a 500 response.
        var allocBySeats = allAllocations
            .GroupBy(a => a.SeatNumber)
            .ToDictionary(g => g.Key, g => g.First());
        var allItems = new List<SeatLayoutItemDto>();

        for (var seat = 1; seat <= totalSeats; seat++)
        {
            allocBySeats.TryGetValue(seat, out var alloc);
            Student? student = alloc != null && studentsDict.TryGetValue(alloc.StudentId, out var s)
                ? s
                : null;
            var isExternalExamOccupied = alloc != null && alloc.ExamId != examId;
            var isOtherPlanOccupied = alloc != null && alloc.SeatingPlanId != plan.Id;
            var stdName = student?.StandardId != null && standardsDict.TryGetValue(student.StandardId!.Value, out var std)
                ? std.Name : string.Empty;
            var studentName = student == null
                ? null
                : $"{student.FirstName} {student.MiddleName} {student.LastName}"
                    .Replace("  ", " ")
                    .Trim();

            allItems.Add(new SeatLayoutItemDto
            {
                SeatNumber = seat,
                StudentId = alloc?.StudentId,
                StudentName = isExternalExamOccupied ? "Blocked (Other Exam)" : studentName,
                StandardName = isExternalExamOccupied ? "Reserved" : stdName,
                RollNo = isExternalExamOccupied ? null : student?.RollNo,
                RoomNumber = room.RoomName,
                IsRoomAssign = alloc?.IsRoomAssign ?? false,
                BenchId = alloc?.BenchId,
                IsExternalExamOccupied = isExternalExamOccupied,
                IsOtherPlanOccupied = isOtherPlanOccupied,
                SeatingPlanId = alloc?.SeatingPlanId,
                SeatingPlanName = alloc == null
                    ? null
                    : allocationPlanNamesById.GetValueOrDefault(alloc.SeatingPlanId),
                SubjectNames = alloc == null
                    ? []
                    : allocationSubjectNamesByPlanId.GetValueOrDefault(alloc.SeatingPlanId) ?? []
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
            AllocatedCount = allocBySeats.Count,
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
                var selections = (await studentElectiveSelectionRepository.GetQueryableAsync())
                    .Where(selection => selection.SelectedSubjectId == subjectIds[0]);
                // Preserve the legacy core-subject fallback when no selections exist anywhere in the tenant.
                if (await AsyncExecuter.AnyAsync(selections))
                    query = query.Where(student => selections.Any(selection => selection.StudentId == student.Id));
            }
        }

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            query = query.Where(s =>
                (s.FirstName != null && s.FirstName.ToLower().Contains(search)) ||
                (s.LastName != null && s.LastName.ToLower().Contains(search)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var page = Math.Max(1, input.Page);
        var totalPages = (int) Math.Ceiling(totalCount / (double) input.PageSize);
        var students = await AsyncExecuter.ToListAsync(query
            .Skip(Math.Max(0, (page - 1) * input.PageSize))
            .Take(Math.Max(0, input.PageSize))
            .Select(student => new
            {
                student.Id,
                student.StandardId,
                student.FirstName,
                student.MiddleName,
                student.LastName,
                student.RollNo
            }));

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

        return new SeatStudentPagedResponseDto
        {
            Items = items,
            PageNumber = page,
            PageSize = input.PageSize,
            TotalPages = totalPages,
            TotalCount = totalCount
        };
    }

    public async Task<SaveSeatAllocationResponseDto> SaveSeatAllocationAsync(Guid examId, SaveSeatAllocationRequestDto input)
    {
        var exam = await GetScopedExamAsync(examId);
        await ThrowIfExaminationSeatingPublishedAsync(examId);
        var plan = await ResolvePlanAsync(exam, input.SeatingPlanId);
        await ReopenLegacyPlanDraftAsync(plan);

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
        var totalSeats = room.TotalBenches > 0
            ? room.TotalBenches * room.BenchCapacity
            : room.Capacity;
        if (input.Students.Any(s =>
            s.SeatNumber > totalSeats ||
            disabledSeats.Contains(s.SeatNumber) ||
            reservedSeats.Contains(s.SeatNumber)))
            throw new UserFriendlyException("One or more selected seats are outside the room layout or unavailable.");

        var overlappingPlanAllocations = await GetOtherOverlappingPlanAllocationsAsync(
            examId,
            plan.Id,
            [input.RoomId]);
        var incomingSeatNumbers = input.Students.Select(student => student.SeatNumber).ToHashSet();
        var conflictingAllocation = overlappingPlanAllocations.FirstOrDefault(allocation =>
            incomingSeatNumbers.Contains(allocation.SeatNumber));
        if (conflictingAllocation != null)
        {
            var relatedPlan = await seatingPlanRepository.FindAsync(conflictingAllocation.SeatingPlanId);
            throw new UserFriendlyException(
                $"{room.RoomName} seat {conflictingAllocation.SeatNumber} is already used by {relatedPlan?.Name ?? "another seating plan"} at the same examination time. Choose another seat.")
            {
                Code = "ROOM_SEAT_TIME_CONFLICT"
            };
        }

        var incomingStudentIds = input.Students.Select(s => s.StudentId).ToHashSet();

        // Load by studentId (not roomId) to respect the unique index on {ExamId, StudentId}.
        // Disable the soft-delete filter so previously-deleted allocations are visible —
        // otherwise a soft-deleted row is hidden, the code tries to INSERT, and the
        // the unique seating-plan/student constraint fires.
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
        if (!CurrentUser.Id.HasValue)
            return new SeatAllocationDashboardResponseDto();

        var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
        var examQuery = (await examRepository.GetQueryableAsync())
            .Where(e => e.StandardId == input.StandardId && e.Status == ExamStatus.Published);
        if (!(scope.IsAdmin || scope.HasTenantWideScope() || scope.StandardIds.Contains(input.StandardId) ||
              scope.WildcardDivisionStandardIds.Contains(input.StandardId)))
        {
            var allowedDivisionIds = scope.DivisionIds.ToList();
            var primaryTeacherSubjectIds = await GetCurrentPrimaryTeacherSubjectIdsAsync(input.StandardId);
            examQuery = examQuery.Where(exam =>
                (exam.ExamDivisions.Any() && exam.ExamDivisions.All(division =>
                    Enumerable.Contains(allowedDivisionIds, division.DivisionId))) ||
                (exam.ExamSubjects.Any() && exam.ExamSubjects.All(subject =>
                    Enumerable.Contains(primaryTeacherSubjectIds, subject.SubjectId))));
        }

        var roomAssignedFilter = input.IsRoomAssigned == true;
        if (roomAssignedFilter)
        {
            var allocationQuery = await seatAllocationRepository.GetQueryableAsync();
            examQuery = examQuery.Where(exam => allocationQuery.Any(allocation =>
                allocation.ExamId == exam.Id && allocation.IsRoomAssign));
        }

        // Preserve the CLR search rules and the identifier order used by the detailed query,
        // while loading session graphs and supporting data only for the requested page.
        var exams = await AsyncExecuter.ToListAsync(examQuery.OrderBy(exam => exam.Id)
            .Select(exam => new { exam.Id, exam.ExamName }));

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var search = input.Search.ToLower();
            exams = exams.Where(e => e.ExamName.ToLower().Contains(search)).ToList();
        }

        var totalCount = exams.Count;
        var page = Math.Max(1, input.Page);
        var totalPages = (int)Math.Ceiling(totalCount / (double)input.PageSize);
        var candidateExamIds = exams.Skip((page - 1) * input.PageSize).Take(input.PageSize)
            .Select(exam => exam.Id).ToList();
        var allExamsWithDetails = await examRepository.GetListAsync(
            e => candidateExamIds.Contains(e.Id), includeDetails: true);
        var detailsById = allExamsWithDetails.ToDictionary(exam => exam.Id);
        allExamsWithDetails = candidateExamIds.Where(detailsById.ContainsKey).Select(id => detailsById[id]).ToList();

        var examIds = allExamsWithDetails.Select(e => e.Id).ToList();
        var examTypeIds = allExamsWithDetails.Select(e => e.ExamTypeId).Distinct().ToList();
        var examTypes = await AsyncExecuter.ToListAsync((await examTypeRepository.GetQueryableAsync())
            .Where(type => examTypeIds.Contains(type.Id)).Select(type => new { type.Id, type.Name }));
        var examTypesDict = examTypes.ToDictionary(t => t.Id);
        var subjectIds = allExamsWithDetails
            .SelectMany(exam => exam.ExamSubjects)
            .Select(subject => subject.SubjectId)
            .Distinct()
            .ToList();
        var subjects = await AsyncExecuter.ToListAsync((await subjectRepository.GetQueryableAsync())
            .Where(subject => subjectIds.Contains(subject.Id)).Select(subject => new { subject.Id, subject.Name }));
        var subjectNamesById = subjects.ToDictionary(subject => subject.Id, subject => subject.Name);

        var standardName = await AsyncExecuter.FirstOrDefaultAsync((await standardRepository.GetQueryableAsync())
            .Where(standard => standard.Id == input.StandardId).Select(standard => standard.Name));
        var allDivisionIds = allExamsWithDetails
            .SelectMany(exam => exam.ExamDivisions.Select(division => division.DivisionId)
                .Concat(exam.ExamSubjects
                    .SelectMany(subject => subject.RoomAssignments)
                    .Select(assignment => assignment.DivisionId)))
            .Distinct().ToList();
        var divisions = await AsyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync())
            .Where(division => allDivisionIds.Contains(division.Id)).Select(division => new { division.Id, division.Name }));
        var divisionsDict = divisions.ToDictionary(d => d.Id);

        var allocCounts = await seatAllocationRepository.GetListAsync(a => examIds.Contains(a.ExamId));
        var studentQuery = (await studentRepository.GetQueryableAsync()).Where(student =>
            student.StandardId == input.StandardId && student.DivisionId.HasValue &&
            allDivisionIds.Contains(student.DivisionId.Value));
        var studentCounts = await AsyncExecuter.ToListAsync(studentQuery.GroupBy(student => student.DivisionId!.Value)
            .Select(group => new { DivisionId = group.Key, Count = group.Count() }));
        var studentCountByDivision = studentCounts.ToDictionary(row => row.DivisionId, row => row.Count);
        // Progress needs individual division membership only for students already allocated on this page.
        var allocatedStudentIds = allocCounts.Select(allocation => allocation.StudentId).Distinct().ToList();
        var allocatedStudentsInDivisions = await AsyncExecuter.ToListAsync(studentQuery
            .Where(student => allocatedStudentIds.Contains(student.Id))
            .Select(student => new { student.Id, student.DivisionId }));
        var studentDivisionById = allocatedStudentsInDivisions.ToDictionary(student => student.Id, student => student.DivisionId!.Value);

        // Session-level source data. The legacy exam summary is derived from these same rows so
        // old consumers remain compatible while the new inbox can render one truthful time slot.
        var plans = await seatingPlanRepository.GetListAsync(p => examIds.Contains(p.ExamId));
        var planIds = plans.Select(plan => plan.Id).ToList();
        var planSubjects = planIds.Count == 0
            ? []
            : await seatingPlanSubjectRepository.GetListAsync(subject => planIds.Contains(subject.SeatingPlanId));
        var subjectIdsByPlan = planSubjects
            .GroupBy(subject => subject.SeatingPlanId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(subject => subject.ExamSubjectId).ToHashSet());
        var planCountByExam = plans.GroupBy(p => p.ExamId).ToDictionary(g => g.Key, g => g.Count());

        var configuredRoomIds = allExamsWithDetails
            .SelectMany(exam => exam.ExamSubjects)
            .SelectMany(subject => subject.RoomAssignments)
            .SelectMany(assignment => assignment.RoomIds);
        var selectedRoomIds = plans.SelectMany(plan => ParseGuidCsv(plan.RoomIdsCsv));
        var allRoomIds = configuredRoomIds.Concat(selectedRoomIds).Distinct().ToList();
        var rooms = allRoomIds.Count == 0
            ? []
            : await roomRepository.GetListAsync(room => allRoomIds.Contains(room.Id));
        var roomsById = rooms.ToDictionary(room => room.Id);

        // When IsRoomAssigned=true, restrict the exam list to only those with at least one
        // room-assigned allocation, and count only room-assigned allocations.
        var relevantAllocs = roomAssignedFilter
            ? allocCounts.Where(a => a.IsRoomAssign).ToList()
            : allocCounts;

        var allocationsByPlan = relevantAllocs
            .GroupBy(allocation => allocation.SeatingPlanId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var items = allExamsWithDetails.Select(exam =>
        {
            examTypesDict.TryGetValue(exam.ExamTypeId, out var examType);
            var examDivisionIds = exam.ExamDivisions.Select(division => division.DivisionId)
                .Concat(exam.ExamSubjects
                    .SelectMany(subject => subject.RoomAssignments)
                    .Select(assignment => assignment.DivisionId))
                .Distinct()
                .ToList();
            var orderedExamDivisions = examDivisionIds
                .Where(divisionsDict.ContainsKey)
                .Select(divisionId => divisionsDict[divisionId])
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.Id)
                .ToList();
            var divisionNames = orderedExamDivisions
                .Select(d => d.Name)
                .ToList();

            var examPlans = plans.Where(plan => plan.ExamId == exam.Id).ToList();
            var examSubjectsById = exam.ExamSubjects.ToDictionary(subject => subject.Id);
            var sessionSummaries = exam.ExamSubjects
                .GroupBy(BuildSessionKey)
                .OrderBy(group => group.Min(subject => subject.ExamDate ?? DateTime.MaxValue))
                .ThenBy(group => group.Min(subject => subject.StartTime ?? DateTime.MaxValue))
                .Select(group =>
                {
                    var sessionSubjects = group.ToList();
                    var sessionSubjectIds = sessionSubjects.Select(subject => subject.Id).ToHashSet();
                    var exactPlans = examPlans
                        .Where(plan => subjectIdsByPlan.TryGetValue(plan.Id, out var ids) &&
                            ids.SetEquals(sessionSubjectIds))
                        .OrderBy(plan => plan.ScopeType == SeatingPlanScopeType.AllSubjects ? 1 : 0)
                        .ThenBy(plan => plan.CreationTime)
                        .ToList();
                    var sessionPlan = exactPlans.FirstOrDefault();
                    var overlappingPlans = examPlans
                        .Where(plan => subjectIdsByPlan.TryGetValue(plan.Id, out var ids) &&
                            PlanSchedulesOverlap(sessionSubjectIds, ids, examSubjectsById))
                        .ToList();
                    var requiresMigration = exactPlans.Count > 1 || overlappingPlans.Any(plan =>
                        plan.Id != sessionPlan?.Id &&
                        (plan.ScopeType != SeatingPlanScopeType.AllSubjects ||
                         allocationsByPlan.GetValueOrDefault(plan.Id)?.Count > 0));

                    var sessionDivisionIds = sessionSubjects
                        .SelectMany(subject => subject.RoomAssignments)
                        .Select(assignment => assignment.DivisionId)
                        .Distinct()
                        .ToList();
                    if (sessionDivisionIds.Count == 0)
                        sessionDivisionIds = exam.ExamDivisions
                            .Select(division => division.DivisionId)
                            .Distinct()
                            .ToList();

                    var totalStudents = sessionDivisionIds
                        .Sum(divisionId => studentCountByDivision.GetValueOrDefault(divisionId, 0));
                    var planAllocations = sessionPlan == null
                        ? []
                        : allocationsByPlan.GetValueOrDefault(sessionPlan.Id) ?? [];
                    var allocatedStudents = planAllocations
                        .Select(allocation => allocation.StudentId)
                        .Distinct()
                        .Count();

                    var planRoomIds = sessionPlan == null
                        ? []
                        : ParseGuidCsv(sessionPlan.RoomIdsCsv).Distinct().ToList();
                    var sessionConfiguredRoomIds = sessionSubjects
                        .SelectMany(subject => subject.RoomAssignments)
                        .SelectMany(assignment => assignment.RoomIds)
                        .Distinct()
                        .ToList();
                    var capacityRoomIds = planRoomIds.Count > 0 ? planRoomIds : sessionConfiguredRoomIds;
                    var usableCapacity = capacityRoomIds
                        .Where(roomsById.ContainsKey)
                        .Sum(roomId => UsableCapacity(roomsById[roomId]));
                    var roomsUsed = planRoomIds.Count > 0
                        ? planRoomIds.Count
                        : planAllocations.Select(allocation => allocation.RoomId).Distinct().Count();
                    var hasCompleteSchedule = sessionSubjects.All(subject =>
                        subject.ExamDate.HasValue && subject.StartTime.HasValue && subject.EndTime.HasValue);
                    var blockingIssueCount = 0;
                    if (!hasCompleteSchedule) blockingIssueCount++;
                    if (totalStudents == 0) blockingIssueCount++;
                    if (sessionConfiguredRoomIds.Count == 0) blockingIssueCount++;
                    if (usableCapacity > 0 && usableCapacity < totalStudents) blockingIssueCount++;
                    if (requiresMigration) blockingIssueCount++;

                    var sessionStatus = sessionPlan == null
                        ? SeatingPlanStatus.NotStarted
                        : ComputeStatus(
                            totalStudents,
                            allocatedStudents,
                            sessionPlan.IsLocked,
                            sessionPlan.Status == SeatingPlanStatus.Published);
                    if (sessionStatus != SeatingPlanStatus.Published && blockingIssueCount > 0)
                        sessionStatus = SeatingPlanStatus.NeedsAttention;

                    var sessionDivisionProgress = sessionDivisionIds
                        .Where(divisionsDict.ContainsKey)
                        .Select(divisionId =>
                        {
                            var divisionTotal = studentCountByDivision.GetValueOrDefault(divisionId, 0);
                            var divisionAllocated = planAllocations
                                .Where(allocation =>
                                    studentDivisionById.GetValueOrDefault(allocation.StudentId) == divisionId)
                                .Select(allocation => allocation.StudentId)
                                .Distinct()
                                .Count();

                            return new SeatAllocationDivisionProgressDto
                            {
                                DivisionId = divisionId,
                                DivisionName = divisionsDict[divisionId].Name,
                                TotalStudents = divisionTotal,
                                AllocatedStudents = divisionAllocated,
                                PendingStudents = Math.Max(0, divisionTotal - divisionAllocated),
                                Progress = divisionTotal == 0
                                    ? 0
                                    : Math.Min(100, (int)Math.Round(divisionAllocated * 100d / divisionTotal))
                            };
                        })
                        .ToList();

                    return new SeatAllocationSessionSummaryDto
                    {
                        SessionKey = group.Key,
                        SeatingPlanId = sessionPlan?.Id,
                        SeatingPlanName = sessionPlan?.Name,
                        ExamDate = sessionSubjects.FirstOrDefault()?.ExamDate,
                        StartTime = sessionSubjects.FirstOrDefault()?.StartTime,
                        EndTime = sessionSubjects.FirstOrDefault()?.EndTime,
                        SubjectNames = sessionSubjects
                            .Select(subject => subjectNamesById.GetValueOrDefault(subject.SubjectId, string.Empty))
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        Divisions = sessionDivisionIds
                            .Where(divisionsDict.ContainsKey)
                            .Select(divisionId => divisionsDict[divisionId].Name)
                            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        DivisionProgress = sessionDivisionProgress,
                        TotalStudents = totalStudents,
                        AllocatedStudents = allocatedStudents,
                        PendingStudents = Math.Max(0, totalStudents - allocatedStudents),
                        RoomsUsed = roomsUsed,
                        UsableCapacity = usableCapacity,
                        EmptySeats = Math.Max(0, usableCapacity - allocatedStudents),
                        Utilization = usableCapacity == 0
                            ? 0
                            : (int)Math.Round(allocatedStudents / (double)usableCapacity * 100),
                        Status = (int)sessionStatus,
                        HasCompleteSchedule = hasCompleteSchedule,
                        CanPrepare = hasCompleteSchedule && totalStudents > 0 &&
                            sessionConfiguredRoomIds.Count > 0 && !requiresMigration,
                        RequiresMigration = requiresMigration,
                        BlockingIssueCount = blockingIssueCount
                    };
                })
                .ToList();

            // Keep the exam envelope backward compatible and free of double-counting when the
            // same student appears in multiple examination sessions. New clients use Sessions;
            // older clients still receive the unique exam-level totals here.
            var examAllocations = examPlans
                .SelectMany(plan => allocationsByPlan.GetValueOrDefault(plan.Id) ?? [])
                .ToList();
            var totalStudents = examDivisionIds
                .Sum(divisionId => studentCountByDivision.GetValueOrDefault(divisionId, 0));
            var allocated = examAllocations
                .Select(allocation => allocation.StudentId)
                .Distinct()
                .Count();
            var pending = Math.Max(0, totalStudents - allocated);
            var examPlanIds = examPlans.Select(plan => plan.Id).ToHashSet();
            var plannedSubjectIds = planSubjects
                .Where(subject => examPlanIds.Contains(subject.SeatingPlanId))
                .Select(subject => subject.ExamSubjectId)
                .Distinct()
                .ToHashSet();
            var hasDuplicateSubjectOwnership = planSubjects
                .Where(subject => examPlanIds.Contains(subject.SeatingPlanId))
                .GroupBy(subject => subject.ExamSubjectId)
                .Any(group => group.Select(subject => subject.SeatingPlanId).Distinct().Count() > 1);
            var hasIncompletePlannedSchedule = exam.ExamSubjects
                .Where(subject => plannedSubjectIds.Contains(subject.Id))
                .Any(subject => !subject.ExamDate.HasValue || !subject.StartTime.HasValue || !subject.EndTime.HasValue);
            var allSubjectsPlanned = exam.ExamSubjects.Count > 0 &&
                plannedSubjectIds.Count == exam.ExamSubjects.Count;
            var status = examPlans.Count == 0
                ? SeatingPlanStatus.NotStarted
                : hasDuplicateSubjectOwnership || hasIncompletePlannedSchedule
                    ? SeatingPlanStatus.NeedsAttention
                    : allSubjectsPlanned && examPlans.All(plan => plan.Status == SeatingPlanStatus.Published)
                        ? SeatingPlanStatus.Published
                        : allSubjectsPlanned && examPlans.All(plan =>
                            plan.Status is SeatingPlanStatus.Completed or SeatingPlanStatus.Published)
                            ? SeatingPlanStatus.Completed
                            : examAllocations.Count > 0 || plannedSubjectIds.Count > 0
                                ? SeatingPlanStatus.InProgress
                                : SeatingPlanStatus.NotStarted;
            var divisionProgress = orderedExamDivisions
                .Select(division =>
                {
                    var divisionTotal = studentCountByDivision.GetValueOrDefault(division.Id, 0);
                    var divisionAllocated = examAllocations
                        .Where(allocation =>
                            studentDivisionById.GetValueOrDefault(allocation.StudentId) == division.Id)
                        .Select(allocation => allocation.StudentId)
                        .Distinct()
                        .Count();
                    return new SeatAllocationDivisionProgressDto
                    {
                        DivisionId = division.Id,
                        DivisionName = division.Name,
                        TotalStudents = divisionTotal,
                        AllocatedStudents = divisionAllocated,
                        PendingStudents = Math.Max(0, divisionTotal - divisionAllocated),
                        Progress = divisionTotal == 0
                            ? 0
                            : Math.Min(100, (int)Math.Round(divisionAllocated * 100d / divisionTotal))
                    };
                })
                .ToList();

            return new SeatAllocationSummaryDto
            {
                ExamId = exam.Id,
                ExamName = exam.ExamName,
                ExamTypeName = examType?.Name ?? string.Empty,
                StandardName = standardName ?? string.Empty,
                Divisions = divisionNames,
                DivisionProgress = divisionProgress,
                TotalStudents = totalStudents,
                AllocatedStudents = allocated,
                PendingStudents = Math.Max(0, pending),
                RoomsUsed = examAllocations.Select(allocation => allocation.RoomId).Distinct().Count(),
                SeatingPlanCount = planCountByExam.GetValueOrDefault(exam.Id, 0),
                SubjectCount = exam.ExamSubjects.Count,
                PlannedSubjectCount = plannedSubjectIds.Count,
                SubjectNames = exam.ExamSubjects
                    .Select(subject => subjectNamesById.GetValueOrDefault(subject.SubjectId, string.Empty))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Sessions = sessionSummaries,
                Status = (int)status
            };
        }).ToList();

        return new SeatAllocationDashboardResponseDto
        {
            Items = items,
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

    private async Task<List<ExamSeatAllocation>> GetOtherOverlappingPlanAllocationsAsync(
        Guid examId,
        Guid planId,
        IReadOnlyCollection<Guid> roomIds)
    {
        var planSubjects = await seatingPlanSubjectRepository.GetListAsync(s => s.SeatingPlanId == planId);
        var currentPlanSubjectIds = planSubjects.Select(s => s.ExamSubjectId).ToHashSet();
        if (currentPlanSubjectIds.Count == 0 || roomIds.Count == 0)
            return [];

        var exam = await GetScopedExamAsync(examId);
        var otherExams = await examRepository.GetListAsync(
            candidate => candidate.Id != examId,
            includeDetails: true);
        var examSubjectsById = otherExams
            .SelectMany(candidate => candidate.ExamSubjects)
            .Concat(exam.ExamSubjects)
            .GroupBy(subject => subject.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var otherPlanSubjects = await seatingPlanSubjectRepository.GetListAsync(subject =>
            subject.SeatingPlanId != planId);
        var overlappingPlanIds = otherPlanSubjects
            .GroupBy(subject => subject.SeatingPlanId)
            .Where(group => PlanSchedulesOverlap(
                currentPlanSubjectIds,
                group.Select(subject => subject.ExamSubjectId).ToList(),
                examSubjectsById))
            .Select(group => group.Key)
            .ToHashSet();

        if (overlappingPlanIds.Count == 0)
            return [];

        return await seatAllocationRepository.GetListAsync(allocation =>
            roomIds.Contains(allocation.RoomId) &&
            overlappingPlanIds.Contains(allocation.SeatingPlanId));
    }
}
