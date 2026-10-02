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
public class AttendanceReportService(AttendanceRosterService rosters, AttendanceApplicabilityResolver applicability,
	IRepository<StudentAttendancePolicyVersion, Guid> policies, IRepository<AcademicYear, Guid> years,
	IAttendanceDayRepository days, IAttendanceEntryRepository entries, IStudentRepository students,
	StudentScopeAccessService scope) : ApplicationService, ITransientDependency
{
	public async Task<(DateTime Start, DateTime End)> RangeAsync(Guid yearId, DateTime start, DateTime end)
	{
		var year = await AsyncExecuter.SingleOrDefaultAsync((await years.GetQueryableAsync()).Where(y => y.Id == yearId)
			.Select(y => new { y.TenantId, y.StartDate, y.EndDate })) ?? throw new BusinessException(AttendanceOperationErrorCodes.AcademicYearOutsideTenant);
		if (year.TenantId != CurrentTenant.Id) throw new BusinessException(AttendanceOperationErrorCodes.AcademicYearOutsideTenant);
		start = start == default ? year.StartDate.Date : start.Date;
		end = end == default ? (Clock.Now.Date < year.EndDate.Date ? Clock.Now.Date : year.EndDate.Date) : end.Date;
		if (start < year.StartDate.Date || end > year.EndDate.Date || start > end || (end - start).TotalDays > 1096)
			throw new BusinessException(AttendanceOperationErrorCodes.InvalidDateRange);
		return (start, end);
	}

	public async Task<List<AttendanceSummaryDto>> BuildAsync(Guid yearId, Guid standardId, Guid divisionId,
		DateTime start, DateTime end, IReadOnlyCollection<StudentEnrollment> members, bool includeDaily)
	{
		await scope.EnsureCanAccessAttendanceScopeAsync(yearId, standardId, divisionId, "attendance report");
		var context = await applicability.LoadAsync(yearId, standardId, divisionId, start, end, members.Select(e => e.Id).ToArray());
		var versions = await policies.GetListAsync(v => v.TenantId == CurrentTenant.Id && (v.AcademicYearId == yearId || v.AcademicYearId == null) && v.EffectiveFrom <= end);
		var options = new Dictionary<DateTime, AttendancePolicyOptions>();
		for (var date = start; date <= end; date = date.AddDays(1))
		{
			var version = versions.Where(v => v.EffectiveFrom <= date).OrderByDescending(v => v.AcademicYearId.HasValue)
				.ThenByDescending(v => v.EffectiveFrom).ThenByDescending(v => v.VersionNumber).FirstOrDefault();
			options[date] = (version == null ? new AttendancePolicy() : AttendancePolicy.FromJson(version.PolicyJson)).Resolve(context.Scope);
		}
		var ids = members.Select(e => e.StudentId).ToArray();
		var identities = await AsyncExecuter.ToListAsync((await students.GetQueryableWithoutAutoIncludesAsync()).Where(s => s.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, s.Id)));
		var dayQuery = await days.GetQueryableAsync(); var entryQuery = await entries.GetQueryableAsync();
		var records = await AsyncExecuter.ToListAsync(from entry in entryQuery
			join day in dayQuery on entry.AttendanceDayId equals day.Id
			where day.TenantId == CurrentTenant.Id && day.AcademicYearId == yearId && day.StandardId == standardId && day.DivisionId == divisionId &&
				day.Date >= start && day.Date <= end && Enumerable.Contains(ids, entry.StudentId)
			select new AttendanceReportRecord(entry, day.Date, day.IsSubmitted));
		var result = new List<AttendanceSummaryDto>();
		foreach (var member in members)
		{
			var student = identities.SingleOrDefault(s => s.Id == member.StudentId); if (student == null) continue;
			var row = new AttendanceSummaryDto { StudentId = student.Id, EnrollmentId = member.Id, AcademicYearId = yearId,
				LowAttendanceThreshold = options[end].LowAttendanceThreshold,
				FullName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))), RollNo = member.AttendancePlacementOn(end)?.RollNumber ?? member.RollNumber };
			var memberRecords = records.Where(r => r.Entry.StudentId == member.StudentId && (!r.Entry.EnrollmentId.HasValue || r.Entry.EnrollmentId == member.Id))
				.GroupBy(r => r.Date.Date).ToDictionary(g => g.Key, g => g.ToList());
			var daily = new List<AttendanceSummaryDayDto>();
			for (var date = start; date <= end; date = date.AddDays(1))
			{
				var placement = member.AttendancePlacementOn(date); var applicable = context.For(member.Id, date);
				var memberExpected = member.AttendanceStartDate.HasValue && member.AttendanceStartDate <= date && placement?.IsActive == true &&
					placement.StandardId == standardId && placement.DivisionId == divisionId;
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
					IsProvisional = ambiguous || expected && (record?.Submitted != true || entry?.PolicySnapshotJson == null) || !member.AttendanceStartDate.HasValue ||
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
internal sealed record AttendanceReportRecord(AttendanceEntry Entry, DateTime Date, bool Submitted);
