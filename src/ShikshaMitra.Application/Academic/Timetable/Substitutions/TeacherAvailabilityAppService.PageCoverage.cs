using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public partial class TeacherAvailabilityAppService
{
	private async Task<List<Employee>> LoadPageCoverageEmployeesAsync(IReadOnlyCollection<TeacherAvailability> rows)
	{
		var relevant = rows.Where(row => row.AffectedLectureCount > 0 || row.SourceType == TeacherAvailabilitySource.Attendance).ToList();
		var ids = relevant.Where(row => row.TeacherId.HasValue).Select(row => row.TeacherId!.Value).Distinct().ToArray();
		var names = relevant.Where(row => !row.TeacherId.HasValue && !string.IsNullOrWhiteSpace(row.TeacherName))
			.Select(row => row.TeacherName).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var namedIds = Array.Empty<Guid>();
		if (names.Count > 0)
		{
			var query = await employeeRepository.GetQueryableAsync();
			var nameRows = await AsyncExecuter.ToListAsync(query.Select(employee => new { employee.Id, employee.FirstName, employee.LastName }));
			namedIds = nameRows.Where(employee => names.Contains(string.Join(" ", new[] { employee.FirstName, employee.LastName }
				.Where(part => !string.IsNullOrWhiteSpace(part))))).Select(employee => employee.Id).ToArray();
		}
		if (ids.Length == 0 && namedIds.Length == 0) return [];
		return await employeeRepository.GetListAsync(employee => Enumerable.Contains(ids, employee.Id) ||
			(employee.UserId.HasValue && Enumerable.Contains(ids, employee.UserId.Value)) || Enumerable.Contains(namedIds, employee.Id));
	}

	private async Task ApplyPageCoverageAsync(IReadOnlyList<TeacherAvailability> rows,
		IReadOnlyList<TeacherAvailabilityDto> dtos, List<Employee> employees)
	{
		var requests = new List<PageCoverageRequest>();
		for (var index = 0; index < rows.Count; index++)
		{
			var row = rows[index];
			if (row.AffectedLectureCount <= 0 && row.SourceType != TeacherAvailabilitySource.Attendance) continue;
			TeacherAvailabilityCoverage.Apply(dtos[index], []);
			var employee = row.TeacherId.HasValue
				? employees.FirstOrDefault(item => item.Id == row.TeacherId.Value || item.UserId == row.TeacherId.Value)
				: employees.FirstOrDefault(item => string.Equals(item.EmployeeName, row.TeacherName, StringComparison.OrdinalIgnoreCase));
			var teacherId = row.TeacherId ?? employee?.Id;
			var date = NormalizeDate(row.AvailabilityDate);
			var day = ToTimetableDay(date.DayOfWeek);
			if (!teacherId.HasValue || !day.HasValue) continue;
			requests.Add(new PageCoverageRequest(index, employee, date, day.Value,
				ParseTime(dtos[index].StartTime, "startTime"), ParseTime(dtos[index].EndTime, "endTime"),
				GetTeacherReferenceIds(employee, teacherId).ToArray(), row.AcademicYearId));
		}
		if (requests.Count == 0) return;
		if (requests.Any(request => !request.AcademicYearId.HasValue))
		{
			var years = await academicYearRepository.GetQueryableAsync();
			var yearRows = await AsyncExecuter.ToListAsync(years.OrderByDescending(year => year.StartDate)
				.Select(year => new { year.Id, year.StartDate, year.EndDate }));
			foreach (var request in requests.Where(request => !request.AcademicYearId.HasValue))
				request.AcademicYearId = yearRows.FirstOrDefault(year => year.StartDate.Date <= request.Date.Date &&
					year.EndDate.Date >= request.Date.Date)?.Id;
		}
		var teacherIds = requests.SelectMany(request => request.TeacherIds).Distinct().ToArray();
		var days = requests.Select(request => request.Day).Distinct().ToArray();
		var start = requests.Min(request => request.Start);
		var end = requests.Max(request => request.End);
		var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
		var query = TimetableScopeQuery.Apply(await timetableRepository.GetQueryableAsync(), scope)
			.Where(timetable => timetable.Status == TimetableStatus.Published);
		var standards = await standardRepository.GetQueryableAsync();
		if (requests.All(request => request.AcademicYearId.HasValue))
		{
			var yearIds = requests.Select(request => request.AcademicYearId!.Value).Distinct().ToArray();
			var standardIds = standards.Where(standard => Enumerable.Contains(yearIds,
				standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId)).Select(standard => standard.Id);
			query = query.Where(timetable => standardIds.Contains(timetable.StandardId));
		}
		var lectures = await AsyncExecuter.ToListAsync(
			from timetable in query
			from slot in timetable.Slots
			where Enumerable.Contains(days, slot.Day) && slot.StartTime < end && slot.EndTime > start
			from subject in slot.Subjects
			where subject.TeacherId.HasValue && Enumerable.Contains(teacherIds, subject.TeacherId.Value)
			orderby timetable.Id, slot.Id, subject.Id
			select new PageLecture(timetable.Id, timetable.StandardId, timetable.DivisionId, slot.Id, subject.Id,
				subject.SubjectId, subject.TeacherId!.Value, subject.RoomId, slot.Day, slot.Sequence, slot.StartTime, slot.EndTime));
		if (lectures.Count == 0) return;
		var timetableIds = lectures.Select(lecture => lecture.TimetableId).Distinct().ToArray();
		var rootReferenceQuery = from timetable in query
			where Enumerable.Contains(timetableIds, timetable.Id)
			from slot in timetable.Slots
			from subject in slot.Subjects
			where subject.TeacherId.HasValue && Enumerable.Contains(teacherIds, subject.TeacherId.Value)
			select new { TimetableId = timetable.Id, TeacherId = subject.TeacherId!.Value };
		var rootReferences = await AsyncExecuter.ToListAsync(rootReferenceQuery.Distinct());
		var rootTeacherIds = rootReferences.ToLookup(reference => reference.TimetableId, reference => reference.TeacherId);
		var classIds = lectures.Select(lecture => lecture.StandardId).Distinct().ToArray();
		var divisionIds = lectures.Select(lecture => lecture.DivisionId).Distinct().ToArray();
		var subjectIds = lectures.Select(lecture => lecture.SubjectId).Distinct().ToArray();
		var roomIds = lectures.Where(lecture => lecture.RoomId.HasValue).Select(lecture => lecture.RoomId!.Value).Distinct().ToArray();
		var standardRows = await AsyncExecuter.ToListAsync(standards.Where(standard => Enumerable.Contains(classIds, standard.Id))
			.Select(standard => new { standard.Id, standard.Name, AcademicYearId = standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId }));
		var standardLookup = standardRows.ToDictionary(standard => standard.Id);
		var divisionRows = await AsyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync())
			.Where(division => Enumerable.Contains(divisionIds, division.Id)).Select(division => new { division.Id, division.Name }));
		var divisionNames = divisionRows.ToDictionary(division => division.Id, division => division.Name);
		var subjectRows = await AsyncExecuter.ToListAsync((await subjectRepository.GetQueryableAsync())
			.Where(subject => Enumerable.Contains(subjectIds, subject.Id)).Select(subject => new { subject.Id, subject.Name }));
		var subjectNames = subjectRows.ToDictionary(subject => subject.Id, subject => subject.Name);
		var roomNames = new Dictionary<Guid, string>();
		if (roomIds.Length > 0)
		{
			var rooms = await AsyncExecuter.ToListAsync((await roomRepository.GetQueryableAsync())
				.Where(room => Enumerable.Contains(roomIds, room.Id)).Select(room => new { room.Id, room.RoomName }));
			roomNames = rooms.ToDictionary(room => room.Id, room => room.RoomName);
		}
		var dates = requests.Select(request => request.Date).Distinct().ToArray();
		var existing = await AsyncExecuter.ToListAsync((await substitutionRepository.GetQueryableAsync()).Where(substitution =>
			Enumerable.Contains(dates, substitution.Date) && substitution.OriginalTeacherId.HasValue &&
			Enumerable.Contains(teacherIds, substitution.OriginalTeacherId.Value) && substitution.Status != SubstitutionStatus.Cancelled &&
			substitution.Status != SubstitutionStatus.Rejected && substitution.Status != SubstitutionStatus.Expired)
			.Select(substitution => new PageExistingSubstitution(substitution.Id, substitution.Date, substitution.OriginalTeacherId!.Value,
				substitution.TimetableId, substitution.TimetableSlotId, substitution.TimetableSlotSubjectId, substitution.SubjectId,
				substitution.StartTime, substitution.EndTime, substitution.Status)));
		var substitutionsByDate = existing.ToLookup(substitution => substitution.Date);
		foreach (var request in requests)
		{
			var candidateLectures = lectures
				.Where(lecture => request.TeacherIds.Contains(lecture.TeacherId) && lecture.Day == request.Day && lecture.Start < request.End && lecture.End > request.Start &&
					(!request.AcademicYearId.HasValue || (standardLookup.TryGetValue(lecture.StandardId, out var standard) &&
						standard.AcademicYearId == request.AcademicYearId.Value)))
				.OrderBy(lecture => Array.FindIndex(request.TeacherIds, id => rootTeacherIds[lecture.TimetableId].Contains(id)))
				.ThenBy(lecture => lecture.TimetableId).ThenBy(lecture => lecture.SlotId).ThenBy(lecture => lecture.SlotSubjectId);
			var substitutions = substitutionsByDate[request.Date].Where(substitution => request.TeacherIds.Contains(substitution.TeacherId)).ToList();
			var items = candidateLectures.Select(lecture =>
			{
				var match = substitutions.FirstOrDefault(substitution => substitution.SlotSubjectId == lecture.SlotSubjectId ||
					(!substitution.SlotSubjectId.HasValue && substitution.TimetableId == lecture.TimetableId && substitution.SlotId == lecture.SlotId &&
						substitution.SubjectId == lecture.SubjectId && substitution.Start < lecture.End && substitution.End > lecture.Start));
				var past = IsPastWindow(request.Date, lecture.End);
				return new AffectedLectureDto
				{
					TimetableId = lecture.TimetableId, TimetableSlotId = lecture.SlotId, SlotSubjectId = lecture.SlotSubjectId,
					Period = $"Period {lecture.Sequence}", StartTime = lecture.Start.ToString("HH:mm"), EndTime = lecture.End.ToString("HH:mm"),
					Time = $"{lecture.Start:HH:mm} - {lecture.End:HH:mm}", StandardId = lecture.StandardId,
					ClassName = standardLookup.TryGetValue(lecture.StandardId, out var standard) ? standard.Name : string.Empty,
					DivisionId = lecture.DivisionId, DivisionName = divisionNames.GetValueOrDefault(lecture.DivisionId, string.Empty),
					SubjectId = lecture.SubjectId, SubjectName = subjectNames.GetValueOrDefault(lecture.SubjectId, string.Empty),
					RoomId = lecture.RoomId, RoomName = lecture.RoomId.HasValue ? roomNames.GetValueOrDefault(lecture.RoomId.Value, string.Empty) : string.Empty,
					TeacherId = request.Employee?.Id ?? lecture.TeacherId, TeacherName = request.Employee?.EmployeeName ?? string.Empty,
					ExistingSubstitutionId = match?.Id, ExistingSubstitutionStatus = match?.Status.ToString(),
					CanCreateSubstitution = match == null && !past,
					CreateSubstitutionBlockReason = match != null ? "Already assigned" : past ? "Lecture ended" : null
				};
			}).OrderBy(item => item.StartTime).ToList();
			TeacherAvailabilityCoverage.Apply(dtos[request.Index], items);
		}
	}

	private sealed class PageCoverageRequest(int index, Employee? employee, DateTime date,
		TimetableDayOfWeek day, TimeOnly start, TimeOnly end, Guid[] teacherIds, Guid? academicYearId)
	{
		public int Index { get; } = index;
		public Employee? Employee { get; } = employee;
		public DateTime Date { get; } = date;
		public TimetableDayOfWeek Day { get; } = day;
		public TimeOnly Start { get; } = start;
		public TimeOnly End { get; } = end;
		public Guid[] TeacherIds { get; } = teacherIds;
		public Guid? AcademicYearId { get; set; } = academicYearId;
	}
	private sealed record PageLecture(Guid TimetableId, Guid StandardId, Guid DivisionId, Guid SlotId, Guid SlotSubjectId,
		Guid SubjectId, Guid TeacherId, Guid? RoomId, TimetableDayOfWeek Day, int Sequence, TimeOnly Start, TimeOnly End);
	private sealed record PageExistingSubstitution(Guid Id, DateTime Date, Guid TeacherId, Guid? TimetableId, Guid? SlotId,
		Guid? SlotSubjectId, Guid? SubjectId, TimeOnly Start, TimeOnly End, SubstitutionStatus Status);
}
