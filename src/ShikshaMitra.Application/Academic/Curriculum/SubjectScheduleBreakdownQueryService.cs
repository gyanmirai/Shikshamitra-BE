using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectScheduleBreakdownQueryService(
	ITimetableRepository timetableRepository,
	IWeeklyLectureSlotRepository weeklyLectureSlotRepository,
	IRepository<AssignedSubject, Guid> assignmentRepository,
	IRepository<Subject, Guid> subjectRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<Room, Guid> roomRepository
) : ITransientDependency
{
	public async Task<SubjectScheduleBreakdownDto> GetAsync(
		AssignedSubject assignment,
		IReadOnlyCollection<Guid>? allowedDivisionIds = null,
		IReadOnlyCollection<Guid>? allowedTeacherIds = null)
	{
		var groupedAssignments = assignment.ElectiveGroupId.HasValue
			? await assignmentRepository.GetListAsync(candidate =>
				candidate.StandardId == assignment.StandardId &&
				candidate.ElectiveGroupId == assignment.ElectiveGroupId)
			: [assignment];
		var targetSubjectIds = groupedAssignments.Select(candidate => candidate.SubjectId).ToHashSet();
		var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(assignment.StandardId);
		var allocatedPeriods = assignment.ElectiveGroupId.HasValue
			? weeklySlot?.Subjects.FirstOrDefault(item =>
				item.ElectiveGroupId == assignment.ElectiveGroupId)?.WeeklyLectures ?? 0
			: weeklySlot?.Subjects.FirstOrDefault(item =>
				item.SubjectId == assignment.SubjectId)?.WeeklyLectures ?? 0;
		var hasUnrestrictedAccess = allowedDivisionIds is null && allowedTeacherIds is null;
		var timetables = await timetableRepository.GetByStandardIdAsync(assignment.StandardId);
		var matchingSlots = timetables
			.Where(timetable => timetable.Status != TimetableStatus.NotCreated)
			.SelectMany(timetable => timetable.Slots
				.Select(slot => new
				{
					Timetable = timetable,
					Slot = slot,
					Subjects = slot.Subjects
						.Where(subject =>
							targetSubjectIds.Contains(subject.SubjectId) &&
							(hasUnrestrictedAccess ||
							 allowedDivisionIds?.Contains(timetable.DivisionId) == true ||
							 (subject.TeacherId.HasValue &&
							  allowedTeacherIds?.Contains(subject.TeacherId.Value) == true)))
						.ToList()
				})
				.Where(item => item.Subjects.Count > 0))
			.ToList();

		var divisionIds = matchingSlots.Select(item => item.Timetable.DivisionId).Distinct().ToList();
		var subjectIds = matchingSlots.SelectMany(item => item.Subjects).Select(item => item.SubjectId).Distinct().ToList();
		var teacherIds = matchingSlots.SelectMany(item => item.Subjects)
			.Where(item => item.TeacherId.HasValue)
			.Select(item => item.TeacherId!.Value)
			.Distinct()
			.ToList();
		var roomIds = matchingSlots.SelectMany(item => item.Subjects)
			.Where(item => item.RoomId.HasValue)
			.Select(item => item.RoomId!.Value)
			.Distinct()
			.ToList();

		var divisions = divisionIds.Count == 0
			? []
			: await divisionRepository.GetListAsync(item => divisionIds.Contains(item.Id));
		var subjects = subjectIds.Count == 0
			? []
			: await subjectRepository.GetListAsync(item => subjectIds.Contains(item.Id));
		var teachers = teacherIds.Count == 0
			? []
			: await employeeRepository.GetListAsync(item => teacherIds.Contains(item.Id));
		var rooms = roomIds.Count == 0
			? []
			: await roomRepository.GetListAsync(item => roomIds.Contains(item.Id));
		var divisionNames = divisions.ToDictionary(item => item.Id, item => item.Name);
		var subjectsById = subjects.ToDictionary(item => item.Id);
		var teacherNames = teachers.ToDictionary(item => item.Id, item => item.EmployeeName);
		var roomNames = rooms.ToDictionary(item => item.Id, item => item.RoomName);

		var lectures = matchingSlots
			.Select(item => new SubjectScheduleLectureDto
			{
				TimetableId = item.Timetable.Id,
				TimetableSlotId = item.Slot.Id,
				TimetableStatus = item.Timetable.Status,
				DivisionId = item.Timetable.DivisionId,
				DivisionName = divisionNames.GetValueOrDefault(item.Timetable.DivisionId, "Unknown division"),
				Day = item.Slot.Day,
				PeriodNumber = item.Slot.Sequence,
				StartTime = item.Slot.StartTime.ToString("HH:mm"),
				EndTime = item.Slot.EndTime.ToString("HH:mm"),
				Subjects = item.Subjects.Select(subject =>
				{
					subjectsById.TryGetValue(subject.SubjectId, out var subjectDetails);
					return new SubjectScheduleLectureSubjectDto
					{
						SubjectId = subject.SubjectId,
						SubjectName = subjectDetails?.Name ?? "Unknown subject",
						SubjectColor = subjectDetails?.Color,
						TeacherId = subject.TeacherId,
						TeacherName = subject.TeacherId.HasValue
							? teacherNames.GetValueOrDefault(subject.TeacherId.Value)
							: null,
						RoomId = subject.RoomId,
						RoomName = subject.RoomId.HasValue
							? roomNames.GetValueOrDefault(subject.RoomId.Value)
							: null
					};
				}).ToList()
			})
			.OrderBy(item => item.Day)
			.ThenBy(item => item.PeriodNumber)
			.ThenBy(item => item.DivisionName, StringComparer.OrdinalIgnoreCase)
			.ToList();

		return new SubjectScheduleBreakdownDto
		{
			SubjectAssignmentId = assignment.Id,
			StandardId = assignment.StandardId,
			AllocatedPeriodsPerWeek = allocatedPeriods,
			ScheduledPeriods = lectures.Count,
			DivisionCount = lectures.Select(item => item.DivisionId).Distinct().Count(),
			Lectures = lectures
		};
	}
}
