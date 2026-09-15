using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Reports;

public class StudentAttendanceDynamicReportRowReader(
	IAsyncQueryableExecuter asyncExecuter,
	IAttendanceDayRepository attendanceDayRepository,
	IAttendanceEntryRepository attendanceEntryRepository,
	IStudentRepository studentRepository) : ITransientDependency
{
	public async Task<List<Dictionary<string, string?>>> ReadAsync(
		AcademicDynamicReportSpecDto plan,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportCatalogSnapshot snapshot,
		int maximumRows)
	{
		var days = await BuildScopedDaysAsync(plan, scope, snapshot);
		return plan.Dataset switch
		{
			AcademicDynamicDatasets.StudentAttendanceEntries =>
				await ReadEntriesAsync(days, snapshot, maximumRows),
			AcademicDynamicDatasets.StudentAttendanceDailySummary =>
				await ReadDailySummaryAsync(days, snapshot, maximumRows),
			_ => throw AcademicDynamicReportGuard.DatasetNotAllowed(plan.Dataset)
		};
	}

	private async Task<IQueryable<AttendanceDay>> BuildScopedDaysAsync(
		AcademicDynamicReportSpecDto plan,
		AcademicAiReportCatalogScope scope,
		AcademicDynamicReportCatalogSnapshot snapshot)
	{
		var query = (await attendanceDayRepository.GetQueryableAsync())
			.Where(day => day.AcademicYearId == scope.AcademicYearId);
		var classIds = Resolve(plan.Filters.ClassTokens, snapshot.Classes, item => item.Token, item => item.Id)
			.Concat(Resolve(plan.Filters.DivisionTokens, snapshot.Divisions, item => item.Token, item => item.StandardId))
			.Distinct().ToList();
		var divisionIds = Resolve(
			plan.Filters.DivisionTokens, snapshot.Divisions, item => item.Token, item => item.Id).ToList();

		if (scope.ClassIds is { Count: > 0 }) query = query.Where(day => scope.ClassIds.Contains(day.StandardId));
		if (scope.DivisionIds is { Count: > 0 }) query = query.Where(day => scope.DivisionIds.Contains(day.DivisionId));
		if (classIds.Count > 0) query = query.Where(day => classIds.Contains(day.StandardId));
		if (divisionIds.Count > 0) query = query.Where(day => divisionIds.Contains(day.DivisionId));
		return ApplyDatePushdown(query, plan.Where);
	}

	private async Task<List<Dictionary<string, string?>>> ReadEntriesAsync(
		IQueryable<AttendanceDay> days,
		AcademicDynamicReportCatalogSnapshot snapshot,
		int maximumRows)
	{
		var entries = await attendanceEntryRepository.GetQueryableAsync();
		var students = await studentRepository.GetQueryableWithoutAutoIncludesAsync();
		var records = await asyncExecuter.ToListAsync((
			from entry in entries
			join day in days on entry.AttendanceDayId equals day.Id
			join student in students on entry.StudentId equals student.Id
			select new AttendanceEntryProjection
			{
				Date = day.Date, StandardId = day.StandardId, DivisionId = day.DivisionId,
				GrNumber = student.GrNumber, GrNo = student.GrNo, RollNo = student.RollNo,
				FirstName = student.FirstName, MiddleName = student.MiddleName, LastName = student.LastName,
				Status = entry.Status, Remarks = entry.Remarks, IsLocked = day.IsLocked
			}).Take(maximumRows + 1));

		var classNames = snapshot.Classes.ToDictionary(item => item.Id, item => item.Name);
		var divisionNames = snapshot.Divisions.ToDictionary(item => item.Id, item => item.Name);
		return records.Select(record => Row(
			("date", record.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
			("class", classNames.GetValueOrDefault(record.StandardId)),
			("division", divisionNames.GetValueOrDefault(record.DivisionId)),
			("grNumber", record.GrNumber ?? record.GrNo?.ToString(CultureInfo.InvariantCulture)),
			("rollNumber", record.RollNo?.ToString(CultureInfo.InvariantCulture)),
			("studentName", FullName(record.FirstName, record.MiddleName, record.LastName)),
			("status", record.Status.ToString()), ("remarks", record.Remarks),
			("isLocked", record.IsLocked ? "Yes" : "No"))).ToList();
	}

	private async Task<List<Dictionary<string, string?>>> ReadDailySummaryAsync(
		IQueryable<AttendanceDay> days,
		AcademicDynamicReportCatalogSnapshot snapshot,
		int maximumRows)
	{
		var entries = await attendanceEntryRepository.GetQueryableAsync();
		var records = await asyncExecuter.ToListAsync((
			from entry in entries
			join day in days on entry.AttendanceDayId equals day.Id
			group entry by new { day.Date, day.StandardId, day.DivisionId, day.IsLocked } into grouped
			select new AttendanceDailyProjection
			{
				Date = grouped.Key.Date, StandardId = grouped.Key.StandardId,
				DivisionId = grouped.Key.DivisionId, IsLocked = grouped.Key.IsLocked,
				MarkedCount = grouped.Count(),
				PresentCount = grouped.Count(entry => entry.Status == AttendanceStatus.Present),
				AbsentCount = grouped.Count(entry => entry.Status == AttendanceStatus.Absent)
			}).Take(maximumRows + 1));

		var classNames = snapshot.Classes.ToDictionary(item => item.Id, item => item.Name);
		var divisionNames = snapshot.Divisions.ToDictionary(item => item.Id, item => item.Name);
		return records.Select(record =>
		{
			var decided = record.PresentCount + record.AbsentCount;
			var percentage = decided == 0 ? 0 : Math.Round(record.PresentCount * 100m / decided, 2);
			return Row(
				("date", record.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
				("class", classNames.GetValueOrDefault(record.StandardId)),
				("division", divisionNames.GetValueOrDefault(record.DivisionId)),
				("markedCount", Number(record.MarkedCount)), ("presentCount", Number(record.PresentCount)),
				("absentCount", Number(record.AbsentCount)),
				("attendancePercentage", percentage.ToString(CultureInfo.InvariantCulture)),
				("isLocked", record.IsLocked ? "Yes" : "No"));
		}).ToList();
	}

	internal static IQueryable<AttendanceDay> ApplyDatePushdown(
		IQueryable<AttendanceDay> query,
		AcademicDynamicReportWhereDto where)
	{
		if (!string.Equals(where.Match, AcademicDynamicMatchModes.All, StringComparison.OrdinalIgnoreCase)) return query;
		foreach (var condition in where.Conditions.Where(item =>
			string.Equals(item.Field, "date", StringComparison.OrdinalIgnoreCase)))
		{
			var dates = condition.Values.Select(value => DateTime.Parse(
				value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal).Date).ToList();
			query = condition.Operator switch
			{
				AcademicDynamicFilterOperators.Equal => query.Where(day => day.Date == dates[0]),
				AcademicDynamicFilterOperators.NotEquals => query.Where(day => day.Date != dates[0]),
				AcademicDynamicFilterOperators.In => query.Where(day => dates.Contains(day.Date)),
				AcademicDynamicFilterOperators.GreaterThan => query.Where(day => day.Date > dates[0]),
				AcademicDynamicFilterOperators.GreaterThanOrEqual => query.Where(day => day.Date >= dates[0]),
				AcademicDynamicFilterOperators.LessThan => query.Where(day => day.Date < dates[0]),
				AcademicDynamicFilterOperators.LessThanOrEqual => query.Where(day => day.Date <= dates[0]),
				AcademicDynamicFilterOperators.Between => query.Where(day => day.Date >= dates[0] && day.Date <= dates[1]),
				_ => query
			};
		}
		return query;
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
	private static string FullName(params string?[] parts) => string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

	private sealed class AttendanceEntryProjection
	{
		public DateTime Date { get; set; }
		public Guid StandardId { get; set; }
		public Guid DivisionId { get; set; }
		public string? GrNumber { get; set; }
		public int? GrNo { get; set; }
		public int? RollNo { get; set; }
		public string? FirstName { get; set; }
		public string? MiddleName { get; set; }
		public string? LastName { get; set; }
		public AttendanceStatus Status { get; set; }
		public string? Remarks { get; set; }
		public bool IsLocked { get; set; }
	}

	private sealed class AttendanceDailyProjection
	{
		public DateTime Date { get; set; }
		public Guid StandardId { get; set; }
		public Guid DivisionId { get; set; }
		public bool IsLocked { get; set; }
		public int MarkedCount { get; set; }
		public int PresentCount { get; set; }
		public int AbsentCount { get; set; }
	}
}
