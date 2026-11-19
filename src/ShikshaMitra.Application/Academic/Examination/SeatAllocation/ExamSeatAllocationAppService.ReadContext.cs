using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

public partial class ExamSeatAllocationAppService
{
    private async Task<SeatingReadContext> LoadSeatingReadContextAsync(Exam exam,
        IReadOnlyList<ExamSeatingPlan>? plans = null,
        IReadOnlyList<ExamSeatingPlanSubject>? subjects = null,
        IReadOnlyList<ExamSeatAllocation>? allocations = null)
    {
        plans ??= await seatingPlanRepository.GetListAsync(plan => plan.ExamId == exam.Id);
        subjects ??= await seatingPlanSubjectRepository.GetListAsync(subject => subject.ExamId == exam.Id);
        allocations ??= await seatAllocationRepository.GetListAsync(allocation => allocation.ExamId == exam.Id);
        var divisionIds = exam.ExamDivisions.Select(division => division.DivisionId)
            .Concat(exam.ExamSubjects.SelectMany(subject => subject.RoomAssignments).Select(assignment => assignment.DivisionId))
            .Distinct().ToList();
        var students = await AsyncExecuter.ToListAsync((await studentRepository.GetQueryableAsync())
            .Where(student => student.StandardId == exam.StandardId && student.DivisionId.HasValue && divisionIds.Contains(student.DivisionId.Value))
            .Select(student => new SeatingStudentKey(student.Id, student.DivisionId)));
        var studentIds = students.Select(student => student.Id).ToList();
        var subjectIds = subjects.Select(subject => subject.SubjectId).Distinct().ToList();
        var selections = await AsyncExecuter.ToListAsync((await studentElectiveSelectionRepository.GetQueryableAsync())
            .Where(selection => studentIds.Contains(selection.StudentId) && subjectIds.Contains(selection.SelectedSubjectId))
            .Select(selection => new SeatingSelectionKey(selection.StudentId, selection.SelectedSubjectId)));
        var roomIds = exam.ExamSubjects.SelectMany(subject => subject.RoomAssignments).SelectMany(assignment => assignment.RoomIds)
            .Concat(plans.SelectMany(plan => ParseGuidCsv(plan.RoomIdsCsv))).Distinct().ToList();
        var rooms = roomIds.Count == 0 ? new List<Room>() : await roomRepository.GetListAsync(room => roomIds.Contains(room.Id));
        return new SeatingReadContext(plans, subjects, allocations, students, selections, rooms);
    }

    private static HashSet<Guid> GetEligibleStudentIds(Exam exam, ExamSeatingPlan plan, SeatingReadContext context)
    {
        var planSubjects = context.Subjects.Where(subject => subject.SeatingPlanId == plan.Id).ToList();
        var examSubjectIds = planSubjects.Select(subject => subject.ExamSubjectId).ToHashSet();
        var divisionIds = exam.ExamSubjects.Where(subject => examSubjectIds.Contains(subject.Id))
            .SelectMany(subject => subject.RoomAssignments).Select(assignment => assignment.DivisionId).ToHashSet();
        if (divisionIds.Count == 0) divisionIds = exam.ExamDivisions.Select(division => division.DivisionId).ToHashSet();
        var excluded = ParseGuidCsv(plan.ExcludedStudentIdsCsv).ToHashSet();
        var candidates = context.Students.Where(student => student.DivisionId.HasValue && divisionIds.Contains(student.DivisionId.Value) && !excluded.Contains(student.Id))
            .Select(student => student.Id).ToHashSet();
        var subjectIds = planSubjects.Select(subject => subject.SubjectId).Distinct().ToHashSet();
        if (subjectIds.Count == 0) return candidates;
        var selections = context.Selections.Where(selection => candidates.Contains(selection.StudentId) && subjectIds.Contains(selection.SubjectId)).ToList();
        if (selections.Count == 0 || selections.Select(selection => selection.SubjectId).Distinct().Count() != subjectIds.Count) return candidates;
        var selected = selections.Select(selection => selection.StudentId).ToHashSet();
        return candidates.Where(selected.Contains).ToHashSet();
    }

    private sealed record SeatingStudentKey(Guid Id, Guid? DivisionId);
    private sealed record SeatingSelectionKey(Guid StudentId, Guid SubjectId);
    private sealed record SeatingReadContext(IReadOnlyList<ExamSeatingPlan> Plans,
        IReadOnlyList<ExamSeatingPlanSubject> Subjects, IReadOnlyList<ExamSeatAllocation> Allocations,
        IReadOnlyList<SeatingStudentKey> Students, IReadOnlyList<SeatingSelectionKey> Selections, IReadOnlyList<Room> Rooms);
}
