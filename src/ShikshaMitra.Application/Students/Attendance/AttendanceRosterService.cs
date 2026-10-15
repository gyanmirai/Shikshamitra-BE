using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance;

[RemoteService(false)]
public class AttendanceRosterService(IRepository<StudentEnrollment, Guid> enrollments,
	IStudentRepository students, IRepository<AcademicYear, Guid> years,
	StudentScopeAccessService scope, AttendanceApplicabilityResolver applicability,
	AttendancePolicyResolver policies, IAttendanceDayRepository days, IAttendanceEntryRepository entries,
	IStudentLeaveRepository leaves, StudentDirectoryQueryService directory) : ApplicationService, ITransientDependency
{
	public async Task<AttendanceRoster> LoadAsync(Guid yearId, Guid standardId, Guid divisionId, DateTime date)
	{
		date = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
		await scope.EnsureCanAccessAttendanceScopeAsync(yearId, standardId, divisionId, "read daily attendance roster");
		var year = await AsyncExecuter.SingleOrDefaultAsync((await years.GetQueryableAsync()).Where(y => y.Id == yearId)
			.Select(y => new { y.TenantId, y.StartDate, y.EndDate })) ?? throw new BusinessException(AttendanceOperationErrorCodes.DateOutsideAcademicYear);
		if (year.TenantId != CurrentTenant.Id || date < year.StartDate.Date || date > year.EndDate.Date)
			throw new BusinessException(AttendanceOperationErrorCodes.DateOutsideAcademicYear);
		var day = await days.FindByDateAndDivisionAsync(yearId, standardId, divisionId, date, includeEntries: true);
		var members = await LoadMembersAsync(yearId, divisionId);
		var candidates = members.Where(e => IsRosterCandidate(e, standardId, divisionId, date,
			day?.IsSubmitted == true && day.Entries.Any(entry => entry.EnrollmentId == e.Id && entry.ExpectedAtSubmission == true))).ToList();
		var context = await applicability.LoadAsync(yearId, standardId, divisionId, date, date, candidates.Select(e => e.Id).ToArray());
		var policy = await policies.ResolveAsync(yearId, date, context.Scope);
		var options = day?.PolicySnapshotJson != null ? JsonSerializer.Deserialize<AttendancePolicyOptions>(day.PolicySnapshotJson)! : policy.Options;
		var currentSchoolDate = AttendanceSchoolDate.Today(options, Clock.Now);
		if (date == currentSchoolDate)
		{
			var population = await directory.GetPopulationAsync(new StudentDirectoryRequest {
				AcademicYearId = yearId, StandardId = standardId, DivisionId = divisionId, Population = "Active" });
			var visibleIds = (await AsyncExecuter.ToListAsync(population.Select(s => s.Id))).ToHashSet();
			// Submitted facts remain reviewable when a student leaves during the school day.
			candidates = candidates.Where(e => visibleIds.Contains(e.StudentId) ||
				day?.IsSubmitted == true && day.Entries.Any(entry => entry.EnrollmentId == e.Id && entry.ExpectedAtSubmission == true)).ToList();
		}
		var studentIds = candidates.Select(e => e.StudentId).ToArray();
		var identities = await AsyncExecuter.ToListAsync((await students.GetQueryableWithoutAutoIncludesAsync())
			.Where(s => s.TenantId == CurrentTenant.Id && Enumerable.Contains(studentIds, s.Id)));
		var schoolZone = TimeZoneInfo.FindSystemTimeZoneById(options.SchoolTimeZone);
		var schoolStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), schoolZone));
		var tomorrow = date.AddDays(1);
		var schoolEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(tomorrow, DateTimeKind.Unspecified), schoolZone));
		var approvedLeaves = await leaves.GetApprovedForAttendanceAsync(studentIds, schoolStart, schoolEnd, CurrentTenant.Id);
		var enrollmentIds = candidates.Select(e => (Guid?)e.Id).ToArray();
		var otherRecorded = await AsyncExecuter.ToListAsync((await entries.GetQueryableAsync()).Where(e => e.TenantId == CurrentTenant.Id &&
			Enumerable.Contains(enrollmentIds, e.EnrollmentId) && e.SchoolDate == date && e.AttendanceDayId != (day == null ? Guid.Empty : day.Id)));
		var rows = new List<AttendanceRosterMember>();
		foreach (var enrollment in candidates)
		{
			var identity = identities.SingleOrDefault(s => s.Id == enrollment.StudentId);
			if (identity == null) continue;
			var placement = enrollment.AttendancePlacementOn(date);
			var expected = context.For(enrollment.Id, date, currentSchoolDate);
			var startConfirmed = enrollment.AttendanceStartDate.HasValue && enrollment.AttendanceStartDate.Value.Date <= date;
			// Today's active class roster is sufficient to take today's observation while older
			// imported enrollments are still waiting for their historical start date to be confirmed.
			var currentEnrollment = date == currentSchoolDate && !enrollment.AttendanceStartDate.HasValue &&
				enrollment.IsActive && enrollment.StandardId == standardId && enrollment.DivisionId == divisionId;
			var matches = approvedLeaves.Where(l => l.StudentId == enrollment.StudentId).ToList();
			if (matches.Count > 1) throw new BusinessException(AttendanceOperationErrorCodes.OverlappingApprovedLeave);
			var entry = day?.Entries.SingleOrDefault(e => e.StudentId == identity.Id);
			var snapshotExpected = day?.IsSubmitted == true && entry?.ExpectedAtSubmission == true;
			var alreadyRecorded = otherRecorded.Any(e => e.EnrollmentId == enrollment.Id);
			rows.Add(new(enrollment, identity, snapshotExpected ? entry!.HistoricalRollNo : placement?.RollNumber ?? enrollment.RollNumber,
				IsExpected(enrollment, standardId, divisionId, date, currentSchoolDate, snapshotExpected, alreadyRecorded, expected.IsExpected),
				alreadyRecorded ? "Already recorded in another division for this date" : !startConfirmed && !currentEnrollment ? "Attendance start date not confirmed for this date" : expected.Reason,
				expected.ScheduleLabel, matches.SingleOrDefault(), entry));
		}
		var revision = RevisionFor(yearId, standardId, divisionId, date, options, rows.Select(r =>
			new AttendanceRosterRevisionRow(r.Enrollment, r.Expected, r.ScheduleLabel, r.Leave)));
		return new(yearId, standardId, divisionId, date, day, rows.OrderBy(r => r.RollNumber).ThenBy(r => r.Student.FirstName).ToList(),
			options, revision, day?.IsSubmitted == true ? new AttendanceApplicabilityResult(true, "Submitted school day") : context.For(Guid.Empty, date, currentSchoolDate));
	}

	public async Task<List<StudentEnrollment>> LoadMembersAsync(Guid yearId, Guid divisionId)
	{
		var recordedIds = from entry in await entries.GetQueryableAsync()
			join day in await days.GetQueryableAsync() on entry.AttendanceDayId equals day.Id
			where day.TenantId == CurrentTenant.Id && day.AcademicYearId == yearId && day.DivisionId == divisionId
			select entry.EnrollmentId;
		return await AsyncExecuter.ToListAsync((await enrollments.GetQueryableAsync()).Where(e => e.TenantId == CurrentTenant.Id && e.AcademicYearId == yearId &&
			(e.DivisionId == divisionId || e.AttendancePlacementHistoryJson.Contains(divisionId.ToString()) || recordedIds.Any(id => id == e.Id))));
	}

	internal static bool IsRosterCandidate(StudentEnrollment enrollment, Guid standardId, Guid divisionId, DateTime date, bool submittedMember)
	{
		if (submittedMember) return true;
		var placement = enrollment.AttendancePlacementOn(date);
		return placement != null ? placement.StandardId == standardId && placement.DivisionId == divisionId && placement.IsActive :
			!enrollment.AttendanceStartDate.HasValue && enrollment.StandardId == standardId && enrollment.DivisionId == divisionId && enrollment.IsActive;
	}

	internal static bool IsExpected(StudentEnrollment enrollment, Guid standardId, Guid divisionId, DateTime date, DateTime schoolToday,
		bool submittedMember, bool alreadyRecorded, bool calendarExpected) => !alreadyRecorded && (submittedMember ||
		(enrollment.AttendanceStartDate.HasValue && enrollment.AttendanceStartDate.Value.Date <= date ||
			date == schoolToday && !enrollment.AttendanceStartDate.HasValue && enrollment.IsActive && enrollment.StandardId == standardId && enrollment.DivisionId == divisionId) && calendarExpected);

	internal static string RevisionFor(Guid yearId, Guid standardId, Guid divisionId, DateTime date, AttendancePolicyOptions options,
		IEnumerable<AttendanceRosterRevisionRow> rows) => Hash(JsonSerializer.Serialize(new { yearId, standardId, divisionId, date, options,
			rows = rows.OrderBy(r => r.Enrollment.Id).Select(r => new { r.Enrollment.Id, r.Enrollment.AttendanceStartDate,
				r.Enrollment.AttendancePlacementHistoryJson, r.Expected, r.ScheduleLabel, leave = r.Leave?.Id, leaveRevision = r.Leave?.ConcurrencyStamp }) }));

	public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed record AttendanceRoster(Guid AcademicYearId, Guid StandardId, Guid DivisionId, DateTime Date, AttendanceDay? Day,
	List<AttendanceRosterMember> Members, AttendancePolicyOptions Options, string Revision, AttendanceApplicabilityResult SchoolDay);
public sealed record AttendanceRosterMember(StudentEnrollment Enrollment, Student Student, int? RollNumber, bool Expected,
	string? Reason, string? ScheduleLabel, AttendanceApprovedLeave? Leave, AttendanceEntry? Entry);

internal sealed record AttendanceRosterRevisionRow(StudentEnrollment Enrollment, bool Expected, string? ScheduleLabel, AttendanceApprovedLeave? Leave);
