using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Timetable.Timetables;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using TimetableEntity = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

[RemoteService(false)]
public class StudentElectiveCorrectionImpactReader(
	IRepository<AssignedSubject, Guid> publishedSubjects,
	IRepository<Exam, Guid> exams, IRepository<ExamMark, Guid> marks,
	IRepository<ExamResultDetail, Guid> results, IRepository<ExamResultHeader, Guid> resultHeaders,
	IRepository<ExamSeatAllocation, Guid> seats, ITimetableRepository timetables,
	IRepository<HomeworkAssignmentEntity, Guid> homework, IRepository<AssignmentEntity, Guid> assignments,
	IRepository<StudentSubmissionEntity, Guid> submissions) : ApplicationService
{
	internal async Task<List<string>> ReadAsync(StudentElectiveCorrectionInput input, StudentElectiveSelection selection,
		List<StudentElectiveCorrectionImpactDto> impacts)
	{
		var evidence = new List<string>();
		if (input.PreferredSubjectId.HasValue && input.PreferredSubjectId != selection.PreferenceOverrideSubjectId)
			impacts.Add(new() { Area = "Preference", Severity = "info", Description = "The current preference will be corrected. The student's original submission will remain unchanged." });
		if (input.SelectedSubjectId == selection.SelectedSubjectId) return evidence;
		var subjectIds = new[] { selection.SelectedSubjectId, input.SelectedSubjectId };
		var allocations = await AsyncExecuter.ToListAsync((await publishedSubjects.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.StandardId == input.ClassId && Enumerable.Contains(subjectIds, x.SubjectId))
			.Select(x => new { x.Id, x.SubjectId, x.ElectiveGroupId, x.SubjectType, x.Status, x.ConcurrencyStamp }));
		evidence.AddRange(allocations.OrderBy(x => x.Id).Select(x => JsonSerializer.Serialize(x)));
		if (!allocations.Any(x => x.SubjectId == input.SelectedSubjectId && x.ElectiveGroupId == input.ElectiveGroupId &&
			x.SubjectType == SubjectType.Elective && x.Status == SubjectStatus.Published))
			impacts.Add(new() { Area = "Subject allocation",
				Description = "The new elective is not published for this class and elective group. The student portal will require school action and timetable access can be unavailable until the class subject allocation is published." });
		var examRows = await AsyncExecuter.ToListAsync((await exams.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.StandardId == input.ClassId && x.ExamDivisions.Any(d => d.DivisionId == input.DivisionId) &&
			x.ExamSubjects.Any(s => Enumerable.Contains(subjectIds, s.SubjectId)))
			.Select(x => new { x.Id, x.ExamName, x.Status, x.ConcurrencyStamp }));
		var examIds = examRows.Select(x => x.Id).ToArray();
		foreach (var exam in examRows.OrderBy(x => x.Id))
		{
			evidence.Add(JsonSerializer.Serialize(exam));
			impacts.Add(new() { Area = "Exam eligibility", RecordId = exam.Id,
				Description = $"{exam.ExamName} ({exam.Status}): subject eligibility and marks readiness will use the corrected elective. Existing exam records will be preserved." });
		}
		var markRows = await AsyncExecuter.ToListAsync((await marks.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.StudentId == input.StudentId && Enumerable.Contains(subjectIds, x.SubjectId))
			.Select(x => new { x.Id, x.ExamId, x.SubjectId, x.IsPublished, x.MarksObtained, x.AttendanceStatus, x.Feedback, x.LastModificationTime }));
		foreach (var mark in markRows.OrderBy(x => x.Id))
		{
			evidence.Add(JsonSerializer.Serialize(mark));
			impacts.Add(new() { Area = "Exam marks", RecordId = mark.Id,
				Description = $"{examRows.FirstOrDefault(x => x.Id == mark.ExamId)?.ExamName ?? "Existing exam"}: {(mark.IsPublished ? "published" : "saved")} marks will remain on their original subject. They will not transfer to the new subject." });
		}
		var relevantExamIds = examIds.Concat(markRows.Select(x => x.ExamId)).Distinct().ToArray();
		var resultRows = await AsyncExecuter.ToListAsync((await results.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.StudentId == input.StudentId && Enumerable.Contains(relevantExamIds, x.ExamId))
			.Select(x => new { x.Id, x.ResultHeaderId, x.ExamId, x.LastModificationTime }));
		var headerIds = resultRows.Select(x => x.ResultHeaderId).Distinct().ToArray();
		var headers = await resultHeaders.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(headerIds, x.Id));
		foreach (var result in resultRows.OrderBy(x => x.Id))
		{
			var header = headers.FirstOrDefault(x => x.Id == result.ResultHeaderId);
			evidence.Add(JsonSerializer.Serialize(new { result, header?.Status, header?.ConcurrencyStamp }));
			impacts.Add(new() { Area = "Exam results", RecordId = result.Id,
				Description = $"{examRows.FirstOrDefault(x => x.Id == result.ExamId)?.ExamName ?? "Existing exam"}: the {header?.Status.ToString().ToLowerInvariant() ?? "existing"} result snapshot will remain unchanged. Review it separately before regenerating or republishing." });
		}
		var seatRows = await AsyncExecuter.ToListAsync((await seats.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.StudentId == input.StudentId && Enumerable.Contains(relevantExamIds, x.ExamId))
			.Select(x => new { x.Id, x.ExamId, x.RoomId, x.SeatNumber, x.ConcurrencyStamp }));
		foreach (var seat in seatRows.OrderBy(x => x.Id))
		{
			evidence.Add(JsonSerializer.Serialize(seat));
			impacts.Add(new() { Area = "Exam seating", RecordId = seat.Id,
				Description = $"{examRows.FirstOrDefault(x => x.Id == seat.ExamId)?.ExamName ?? "Existing exam"}: seat {seat.SeatNumber} will remain assigned. Review seating and any previously issued hall ticket for the changed subject." });
		}
		await AddTimetableImpactsAsync(input, impacts, evidence);
		await AddLearningImpactsAsync(input, subjectIds, impacts, evidence);
		impacts.Add(new() { Area = "Student portal", Severity = "info",
			Description = "The student's timetable, elective subject access, and mobile learning-task visibility will follow the corrected subject. Existing submissions and evaluations will remain unchanged." });
		return evidence;
	}

	private async Task AddTimetableImpactsAsync(StudentElectiveCorrectionInput input, List<StudentElectiveCorrectionImpactDto> impacts, List<string> evidence)
	{
		TimetableEntity? timetable = await timetables.FindByStandardAndDivisionAsync(input.ClassId, input.DivisionId);
		if (timetable == null || timetable.TenantId != CurrentTenant.Id)
		{
			impacts.Add(new() { Area = "Timetable", Description = "No class timetable exists. The new elective will need a timetable before students can use it." });
			return;
		}
		evidence.Add(JsonSerializer.Serialize(new { timetable.Id, timetable.Status, timetable.ConcurrencyStamp }));
		var slots = timetable.Slots.Where(x => x.ElectiveGroupId == input.ElectiveGroupId).OrderBy(x => x.Id).ToList();
		impacts.Add(new() { Area = "Timetable", RecordId = timetable.Id,
			Description = $"The {timetable.Status.ToString().ToLowerInvariant()} timetable has {slots.Count} slots for this elective group. The student's schedule and subject counts will change; saved teacher and room allocations will be preserved." });
		foreach (var slot in slots)
		{
			var options = slot.Subjects.Where(x => x.SubjectId == input.SelectedSubjectId).ToList();
			evidence.Add(JsonSerializer.Serialize(new { slot.Id, slot.Day, slot.Sequence, Options = options.Select(x => new { x.SubjectId, x.TeacherId, x.RoomId }) }));
			if (options.Count != 1 || !options[0].TeacherId.HasValue || !options[0].RoomId.HasValue)
				impacts.Add(new() { Area = "Timetable setup", RecordId = slot.Id,
					Description = $"{slot.Day}, period {slot.Sequence}: the new subject is missing or has incomplete teacher/room allocation. Review this slot separately." });
		}
	}

	private async Task AddLearningImpactsAsync(StudentElectiveCorrectionInput input, Guid[] subjectIds,
		List<StudentElectiveCorrectionImpactDto> impacts, List<string> evidence)
	{
		var homeworkRows = await AsyncExecuter.ToListAsync((await homework.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.StandardId == input.ClassId && Enumerable.Contains(subjectIds, x.SubjectId) &&
			(x.DivisionId == input.DivisionId || x.DivisionSchedules.Any(d => d.DivisionId == input.DivisionId)))
			.Select(x => new { x.Id, x.Title, x.SubjectId, x.Status, x.ConcurrencyStamp }));
		var assignmentRows = await AsyncExecuter.ToListAsync((await assignments.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.StandardId == input.ClassId && Enumerable.Contains(subjectIds, x.SubjectId) &&
			(x.DivisionId == input.DivisionId || x.DivisionSchedules.Any(d => d.DivisionId == input.DivisionId)))
			.Select(x => new { x.Id, x.Title, x.SubjectId, x.Status, x.ConcurrencyStamp }));
		var homeworkIds = homeworkRows.Select(x => x.Id).ToArray();
		var assignmentIds = assignmentRows.Select(x => x.Id).ToArray();
		var submissionRows = await AsyncExecuter.ToListAsync((await submissions.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.StudentId == input.StudentId && (x.HomeworkAssignmentId.HasValue && Enumerable.Contains(homeworkIds, x.HomeworkAssignmentId.Value) ||
				x.AssignmentId.HasValue && Enumerable.Contains(assignmentIds, x.AssignmentId.Value)))
			.Select(x => new { x.Id, x.HomeworkAssignmentId, x.AssignmentId, x.LastModificationTime }));
		foreach (var task in homeworkRows.Select(x => (x.Id, x.Title, x.SubjectId, State: JsonSerializer.Serialize(x), Area: "Homework"))
			.Concat(assignmentRows.Select(x => (x.Id, x.Title, x.SubjectId, State: JsonSerializer.Serialize(x), Area: "Assignments"))).OrderBy(x => x.Id))
		{
			evidence.Add(task.State);
			impacts.Add(new() { Area = task.Area, RecordId = task.Id,
				Description = $"{task.Title}: {(task.SubjectId == input.SelectedSubjectId ? "new-subject" : "previous-subject")} task access will follow the corrected elective. {submissionRows.Count(x => x.HomeworkAssignmentId == task.Id || x.AssignmentId == task.Id)} existing submission(s) will be preserved." });
		}
		evidence.AddRange(submissionRows.OrderBy(x => x.Id).Select(x => JsonSerializer.Serialize(x)));
	}
}
