using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance;

[RemoteService(false)]
public partial class AttendanceReportService : ApplicationService, ITransientDependency
{
	private readonly AttendanceApplicabilityResolver applicability;
	private readonly IRepository<AcademicYear, Guid> years;
	private readonly IAttendanceDayRepository days;
	private readonly IAttendanceEntryRepository entries;
	private readonly IStudentRepository students;
	private readonly StudentScopeAccessService scope;

	public AttendanceReportService(
		AttendanceRosterService rosters,
		AttendanceApplicabilityResolver applicability,
		IRepository<AcademicYear, Guid> years,
		IAttendanceDayRepository days,
		IAttendanceEntryRepository entries,
		IStudentRepository students,
		StudentScopeAccessService scope
	)
	{
		_ = rosters;
		this.applicability = applicability;
		this.years = years;
		this.days = days;
		this.entries = entries;
		this.students = students;
		this.scope = scope;
	}

	public async Task<(DateTime Start, DateTime End)> RangeAsync(Guid yearId, DateTime start, DateTime end)
	{
		var year = await AsyncExecuter.SingleOrDefaultAsync((await years.GetQueryableAsync()).Where(y => y.Id == yearId)
			.Select(y => new { y.TenantId, y.StartDate, y.EndDate })) ?? throw new BusinessException(AttendanceOperationErrorCodes.AcademicYearOutsideTenant);
		if (year.TenantId != CurrentTenant.Id) throw new BusinessException(AttendanceOperationErrorCodes.AcademicYearOutsideTenant);
		start = start == default ? year.StartDate.Date : start.Date;
		var today = AttendanceSchoolDate.Today(new AttendancePolicyOptions(), Clock.Now);
		end = end == default ? (today < year.EndDate.Date ? today : year.EndDate.Date) : end.Date;
		if (start < year.StartDate.Date || end > year.EndDate.Date || start > end || (end - start).TotalDays > 1096)
			throw new BusinessException(AttendanceOperationErrorCodes.InvalidDateRange);
		return (start, end);
	}

	public async Task<List<AttendanceSummaryDto>> BuildAsync(Guid yearId, Guid standardId, Guid divisionId,
		DateTime start, DateTime end, IReadOnlyCollection<StudentEnrollment> members, bool includeDaily)
	{
		await scope.EnsureCanAccessAttendanceScopeAsync(yearId, standardId, divisionId, "attendance report");
		return (await BuildAuthorizedAsync(yearId, start, end, [new(standardId, divisionId, members)], includeDaily)).Single().Rows;
	}

	private async Task<List<AttendanceReportSlice>> BuildAuthorizedAsync(Guid yearId, DateTime start, DateTime end,
		IReadOnlyCollection<AttendanceReportSliceRequest> requests, bool includeDaily)
	{
		var contexts = await applicability.LoadManyAsync(yearId, requests.Select(request => (request.StandardId, request.DivisionId)).Distinct().ToArray(), start, end);
		var ids = requests.SelectMany(request => request.Members).Select(member => member.StudentId).Distinct().ToArray();
		var identities = (await AsyncExecuter.ToListAsync((await students.GetQueryableWithoutAutoIncludesAsync())
			.Where(s => s.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, s.Id))
			.Select(s => new AttendanceReportIdentity(s.Id, s.FirstName, s.MiddleName, s.LastName)))).ToDictionary(s => s.Id);
		var dayQuery = FilterPaths(await days.GetQueryableAsync(), requests);
		var entryQuery = await entries.GetQueryableAsync();
		var records = await AsyncExecuter.ToListAsync(from entry in entryQuery
			join day in dayQuery on entry.AttendanceDayId equals day.Id
			where day.TenantId == CurrentTenant.Id && day.AcademicYearId == yearId &&
				day.Date >= start && day.Date <= end && Enumerable.Contains(ids, entry.StudentId)
			select new AttendanceReportRecord(new AttendanceReportEntry(entry.StudentId, entry.EnrollmentId, entry.Status,
				entry.Late, entry.HalfDay, entry.Remarks, entry.ExpectedAtSubmission, entry.ExcludedFromPercentage,
				entry.PresentCredit, entry.DenominatorCredit, entry.PolicySnapshotJson != null), day.Date, day.IsSubmitted, day.StandardId, day.DivisionId));
		var recordsByPath = records.ToLookup(record => (record.StandardId, record.DivisionId));
		return requests.Select(request => new AttendanceReportSlice(request.StandardId, request.DivisionId,
			BuildRows(yearId, request.StandardId, request.DivisionId, start, end, request.Members, includeDaily,
				contexts[request.DivisionId], identities, recordsByPath[(request.StandardId, request.DivisionId)]))).ToList();
	}

	private List<AttendanceSummaryDto> BuildRows(Guid yearId, Guid standardId, Guid divisionId, DateTime start, DateTime end,
		IReadOnlyCollection<StudentEnrollment> members, bool includeDaily, AttendanceApplicabilityContext context,
		IReadOnlyDictionary<Guid, AttendanceReportIdentity> identities, IEnumerable<AttendanceReportRecord> records)
	{
		var options = new Dictionary<DateTime, AttendancePolicyOptions>();
		for (var date = start; date <= end; date = date.AddDays(1))
		{
			options[date] = new AttendancePolicyOptions();
		}
		var recordsByStudent = records.ToLookup(record => record.Entry.StudentId);
		var result = new List<AttendanceSummaryDto>();
		foreach (var member in members)
		{
			if (!identities.TryGetValue(member.StudentId, out var student)) continue;
			var row = new AttendanceSummaryDto { StudentId = student.Id, EnrollmentId = member.Id, AcademicYearId = yearId,
				LowAttendanceThreshold = options[end].LowAttendanceThreshold,
				FullName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))), RollNo = member.AttendancePlacementOn(end)?.RollNumber ?? member.RollNumber };
			var memberRecords = recordsByStudent[member.StudentId].Where(r => !r.Entry.EnrollmentId.HasValue || r.Entry.EnrollmentId == member.Id)
				.GroupBy(r => r.Date.Date).ToDictionary(g => g.Key, g => g.ToList());
			var daily = new List<AttendanceSummaryDayDto>();
			for (var date = start; date <= end; date = date.AddDays(1))
			{
				var placement = member.AttendancePlacementOn(date); var currentSchoolDate = AttendanceSchoolDate.Today(options[date], Clock.Now);
				var applicable = context.For(member.Id, date, currentSchoolDate);
				var memberExpected = (member.AttendanceStartDate.HasValue && member.AttendanceStartDate <= date && placement?.IsActive == true &&
					placement.StandardId == standardId && placement.DivisionId == divisionId) ||
					(date == currentSchoolDate && !member.AttendanceStartDate.HasValue && member.IsActive &&
					member.StandardId == standardId && member.DivisionId == divisionId);
				var dateRecords = memberRecords.GetValueOrDefault(date);
				var ambiguous = dateRecords?.Count > 1;
				var record = ambiguous ? null : dateRecords?.SingleOrDefault(); var entry = record?.Entry;
				var recordedApplicability = record?.Submitted == true && entry?.ExpectedAtSubmission.HasValue == true;
				var expected = (recordedApplicability ? entry!.ExpectedAtSubmission!.Value : memberExpected && applicable.IsExpected &&
					date <= AttendanceSchoolDate.Today(options[date], Clock.Now)) && !(entry?.ExcludedFromPercentage ?? false);
				daily.Add(new() { Date = date, Status = entry?.Status ?? AttendanceStatus.Pending, Late = entry?.Late ?? false,
					HalfDay = entry?.HalfDay ?? false, Remarks = entry?.Remarks, Expected = expected,
					Recorded = !ambiguous && expected && entry?.Status is AttendanceStatus.Present or AttendanceStatus.Absent,
					PresentCredit = entry?.PresentCredit ?? (entry?.Status == AttendanceStatus.Present ? 1 : 0), DenominatorCredit = entry?.DenominatorCredit ?? 1,
					IsProvisional = ambiguous || expected && (record?.Submitted != true || entry?.HasPolicySnapshot != true) || !member.AttendanceStartDate.HasValue ||
						!recordedApplicability && memberExpected && date <= AttendanceSchoolDate.Today(options[date], Clock.Now) && applicable.IsProvisional,
					ExclusionReason = ambiguous ? "Conflicting historical attendance records require review" : expected ? null : entry?.ExcludedFromPercentage == true ? "Approved leave excluded by recorded policy" : !memberExpected ? "No confirmed attendance membership" : date > AttendanceSchoolDate.Today(options[date], Clock.Now) ? "Future date" : applicable.Reason });
			}
			ApplyCalculation(row, daily);
			if (includeDaily) row.Daily = daily;
			result.Add(row);
		}
		return result;
	}
	public static void ApplyCalculation(AttendanceSummaryDto row, IEnumerable<AttendanceSummaryDayDto> daily)
	{
		var result = AttendanceCalculation.Calculate(daily.Select(d => new AttendanceCalculationDay(d.Date, d.Expected, d.Recorded,
			d.PresentCredit, d.DenominatorCredit, d.IsProvisional, d.Status)));
		row.ExpectedDays = result.ExpectedDays; row.RecordedDays = result.RecordedDays; row.MissingDays = result.MissingDays;
		row.PresentDays = result.PresentDays; row.PresentCredit = result.PresentCredit; row.DenominatorDays = result.DenominatorDays;
		row.Percentage = result.Percentage; row.IsProvisional = result.IsProvisional;
		row.IsBelowThreshold = result.Percentage.HasValue && (decimal)result.Percentage.Value < row.LowAttendanceThreshold;
	}
}
internal sealed record AttendanceReportEntry(Guid StudentId, Guid? EnrollmentId, AttendanceStatus Status, bool Late, bool HalfDay,
	string? Remarks, bool? ExpectedAtSubmission, bool ExcludedFromPercentage, double? PresentCredit, double DenominatorCredit, bool HasPolicySnapshot);
internal sealed record AttendanceReportIdentity(Guid Id, string? FirstName, string? MiddleName, string? LastName);
internal sealed record AttendanceReportRecord(AttendanceReportEntry Entry, DateTime Date, bool Submitted, Guid StandardId, Guid DivisionId);
