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
		var query = (await timetableRepository.GetQueryableAsync())
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

		var flattened = from timetable in query
						from slot in timetable.Slots
						from subject in slot.Subjects.DefaultIfEmpty()
						select new
						{
							TimetableId = timetable.Id, timetable.StandardId, timetable.DivisionId, timetable.Status,
							SlotId = slot.Id, slot.Day, slot.Sequence, slot.StartTime, slot.EndTime, slot.SlotType, slot.BreakType,
							EntryId = subject == null ? Guid.Empty : subject.Id,
							SubjectId = subject == null ? (Guid?)null : subject.SubjectId,
							TeacherId = subject == null ? null : subject.TeacherId,
							RoomId = subject == null ? null : subject.RoomId
						};
		if (subjectIds.Count > 0) flattened = flattened.Where(item => item.SubjectId.HasValue && subjectIds.Contains(item.SubjectId.Value));
		// Bound expanded slot/subject rows before materialization, including a single huge timetable.
		var slots = await asyncExecuter.ToListAsync(flattened.OrderBy(item => item.TimetableId)
			.ThenBy(item => item.SlotId).ThenBy(item => item.EntryId).Take(maximumRows + 1));
		var lookupSubjectIds = slots.Where(item => item.SubjectId.HasValue).Select(item => item.SubjectId!.Value).Distinct().ToList();
		var teacherIds = slots.Where(item => item.TeacherId.HasValue)
			.Select(item => item.TeacherId!.Value).Distinct().ToList();
		var roomIds = slots.Where(item => item.RoomId.HasValue)
			.Select(item => item.RoomId!.Value).Distinct().ToList();
		var subjects = await asyncExecuter.ToListAsync((await subjectRepository.GetQueryableAsync())
			.Where(item => lookupSubjectIds.Contains(item.Id)).Select(item => new { item.Id, item.Name }));
		var teachers = await asyncExecuter.ToListAsync((await employeeRepository.GetQueryableAsync())
			.Where(item => teacherIds.Contains(item.Id)).Select(item => new { item.Id, item.FirstName, item.LastName, item.EmployeeCode }));
		var rooms = await asyncExecuter.ToListAsync((await roomRepository.GetQueryableAsync())
			.Where(item => roomIds.Contains(item.Id)).Select(item => new { item.Id, item.RoomName }));
		var subjectNames = subjects.ToDictionary(item => item.Id, item => item.Name);
		var teacherNames = teachers.ToDictionary(item => item.Id, item =>
		{
			var name = string.Join(" ", new[] { item.FirstName, item.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
			return string.IsNullOrWhiteSpace(name) ? item.EmployeeCode : name;
		});
		var teacherCodes = teachers.ToDictionary(item => item.Id, item => item.EmployeeCode);
		var roomNames = rooms.ToDictionary(item => item.Id, item => item.RoomName);
		var classNames = snapshot.Classes.ToDictionary(item => item.Id, item => item.Name);
		var divisionNames = snapshot.Divisions.ToDictionary(item => item.Id, item => item.Name);

		return slots.Select(slot =>
		{
			var duration = (slot.EndTime - slot.StartTime).TotalMinutes;
			return Row(
				("class", classNames.GetValueOrDefault(slot.StandardId)),
				("division", divisionNames.GetValueOrDefault(slot.DivisionId)),
				("day", slot.Day.ToString()), ("sequence", Number(slot.Sequence)),
				("startTime", slot.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)),
				("endTime", slot.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)),
				("durationMinutes", duration.ToString(CultureInfo.InvariantCulture)),
				("slotType", slot.SlotType.ToString()), ("breakType", slot.BreakType?.ToString()),
				("subject", slot.SubjectId is { } subjectId ? subjectNames.GetValueOrDefault(subjectId) : null),
				("teacherName", slot.TeacherId is { } teacherId ? teacherNames.GetValueOrDefault(teacherId) : null),
				("teacherCode", slot.TeacherId is { } codeTeacherId ? teacherCodes.GetValueOrDefault(codeTeacherId) : null),
				("room", slot.RoomId is { } roomId ? roomNames.GetValueOrDefault(roomId) : null),
				("timetableStatus", slot.Status.ToString()));
		}).ToList();
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
}
