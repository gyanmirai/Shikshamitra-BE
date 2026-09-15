using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using TimetableAggregate = ShikshaMitra.Academic.Timetable.Timetables.Timetable;

namespace ShikshaMitra.Academic.Reports;

public class TimetableDynamicReportRowReader(
	IAsyncQueryableExecuter asyncExecuter,
	ITimetableRepository timetableRepository,
	IRepository<Subject, Guid> subjectRepository,
	IEmployeeRepository employeeRepository,
	IRepository<Room, Guid> roomRepository,
	IUserScopeAccessService userScopeAccessService) : ITransientDependency
{
	public async Task<List<Dictionary<string, string?>>> ReadAsync(
		AcademicDynamicReportSpecDto plan,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportCatalogSnapshot snapshot,
		int maximumRows)
	{
		if (!string.Equals(plan.Dataset, AcademicDynamicDatasets.TimetableSlots,
			StringComparison.OrdinalIgnoreCase))
		{
			throw AcademicDynamicReportGuard.DatasetNotAllowed(plan.Dataset);
		}

		var allowedClassIds = snapshot.Classes.Select(item => item.Id).ToList();
		var allowedDivisionIds = snapshot.Divisions.Select(item => item.Id).ToList();
		var query = (await timetableRepository.WithDetailsAsync())
			.Where(timetable => allowedClassIds.Contains(timetable.StandardId) &&
				allowedDivisionIds.Contains(timetable.DivisionId));
		query = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);

		var classIds = Resolve(plan.Filters.ClassTokens, snapshot.Classes, item => item.Token, item => item.Id)
			.Concat(Resolve(plan.Filters.DivisionTokens, snapshot.Divisions, item => item.Token, item => item.StandardId))
			.Distinct().ToList();
		var divisionIds = Resolve(
			plan.Filters.DivisionTokens, snapshot.Divisions, item => item.Token, item => item.Id).ToList();
		var subjectIds = Resolve(
			plan.Filters.SubjectTokens, snapshot.Subjects, item => item.Token, item => item.Id).ToList();
		if (scope.ClassIds is { Count: > 0 }) query = query.Where(item => scope.ClassIds.Contains(item.StandardId));
		if (scope.DivisionIds is { Count: > 0 }) query = query.Where(item => scope.DivisionIds.Contains(item.DivisionId));
		if (classIds.Count > 0) query = query.Where(item => classIds.Contains(item.StandardId));
		if (divisionIds.Count > 0) query = query.Where(item => divisionIds.Contains(item.DivisionId));

		var timetables = await asyncExecuter.ToListAsync(query);
		var scheduledSubjects = timetables.SelectMany(item => item.Slots).SelectMany(item => item.Subjects)
			.Where(item => subjectIds.Count == 0 || subjectIds.Contains(item.SubjectId)).ToList();
		var lookupSubjectIds = scheduledSubjects.Select(item => item.SubjectId).Distinct().ToList();
		var teacherIds = scheduledSubjects.Where(item => item.TeacherId.HasValue)
			.Select(item => item.TeacherId!.Value).Distinct().ToList();
		var roomIds = scheduledSubjects.Where(item => item.RoomId.HasValue)
			.Select(item => item.RoomId!.Value).Distinct().ToList();
		var subjects = lookupSubjectIds.Count == 0 ? [] :
			await subjectRepository.GetListAsync(item => lookupSubjectIds.Contains(item.Id));
		var teachers = teacherIds.Count == 0 ? [] :
			await employeeRepository.GetListAsync(item => teacherIds.Contains(item.Id));
		var rooms = roomIds.Count == 0 ? [] :
			await roomRepository.GetListAsync(item => roomIds.Contains(item.Id));
		var subjectNames = subjects.ToDictionary(item => item.Id, item => item.Name);
		var teacherNames = teachers.ToDictionary(item => item.Id, FullEmployeeName);
		var teacherCodes = teachers.ToDictionary(item => item.Id, item => item.EmployeeCode);
		var roomNames = rooms.ToDictionary(item => item.Id, item => item.RoomName);
		var classNames = snapshot.Classes.ToDictionary(item => item.Id, item => item.Name);
		var divisionNames = snapshot.Divisions.ToDictionary(item => item.Id, item => item.Name);

		var rows = new List<Dictionary<string, string?>>();
		foreach (var timetable in timetables)
		{
			foreach (var slot in timetable.Slots)
			{
				var slotSubjects = slot.Subjects
					.Where(item => subjectIds.Count == 0 || subjectIds.Contains(item.SubjectId)).ToList();
				if (subjectIds.Count > 0 && slotSubjects.Count == 0) continue;
				if (slotSubjects.Count == 0)
				{
					rows.Add(BuildRow(timetable, slot, null));
				}
				else
				{
					rows.AddRange(slotSubjects.Select(subject => BuildRow(timetable, slot, subject)));
				}
				if (rows.Count > maximumRows) return rows;
			}
		}
		return rows;

		Dictionary<string, string?> BuildRow(
			TimetableAggregate timetable,
			TimetableSlot slot,
			TimetableSlotSubject? scheduledSubject)
		{
			var duration = (slot.EndTime - slot.StartTime).TotalMinutes;
			return Row(
				("class", classNames.GetValueOrDefault(timetable.StandardId)),
				("division", divisionNames.GetValueOrDefault(timetable.DivisionId)),
				("day", slot.Day.ToString()), ("sequence", Number(slot.Sequence)),
				("startTime", slot.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)),
				("endTime", slot.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)),
				("durationMinutes", duration.ToString(CultureInfo.InvariantCulture)),
				("slotType", slot.SlotType.ToString()), ("breakType", slot.BreakType?.ToString()),
				("subject", scheduledSubject == null ? null : subjectNames.GetValueOrDefault(scheduledSubject.SubjectId)),
				("teacherName", scheduledSubject?.TeacherId is { } teacherId ? teacherNames.GetValueOrDefault(teacherId) : null),
				("teacherCode", scheduledSubject?.TeacherId is { } codeTeacherId ? teacherCodes.GetValueOrDefault(codeTeacherId) : null),
				("room", scheduledSubject?.RoomId is { } roomId ? roomNames.GetValueOrDefault(roomId) : null),
				("timetableStatus", timetable.Status.ToString()));
		}
	}

	private static IEnumerable<TResult> Resolve<T, TResult>(IEnumerable<string> tokens, IEnumerable<T> catalog,
		Func<T, string> tokenSelector, Func<T, TResult> valueSelector)
	{
		var tokenSet = tokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
		return catalog.Where(item => tokenSet.Contains(tokenSelector(item))).Select(valueSelector);
	}

	private static Dictionary<string, string?> Row(params (string Key, string? Value)[] values) =>
		values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
	private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
	private static string FullEmployeeName(Employee employee) => string.IsNullOrWhiteSpace(employee.EmployeeName)
		? employee.EmployeeCode
		: employee.EmployeeName;
}
