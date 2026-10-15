using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.Dashboard;

public partial class StudentModuleDashboardApi_Tests
{
	[Fact]
	public async Task Batched_register_snapshot_matches_owner_rosters_and_query_count_stays_constant_for_1_10_50_divisions()
	{
		var id = await SeedAdminAsync();
		await WithTenantAsync(async services =>
		{
			using var identity = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
				new Claim(AbpClaimTypes.UserId, id.ToString()), new Claim(AbpClaimTypes.Role, "Admin")], "Test")));
			var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(Factory.SeededAcademicYearId);
			var date = year.StartDate.Date.AddDays(3); var tenant = Guid.Parse(TestAuthHandler.TestTenantId);
			var paths = new List<(Guid StandardId, Guid DivisionId)>(); var members = new List<StudentEnrollment>();
			for (var index = 0; index < 50; index++)
			{
				var division = await services.GetRequiredService<DivisionManager>().CreateAsync(Factory.SeededStandardId, "Batch " + Guid.NewGuid().ToString("N")[..8], 100, "BATCH");
				await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
				var item = await SeedCaseAsync(services, id, division.Id);
				var member = await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(item.StudentEnrollmentId!.Value);
				member.ConfirmAttendanceStart(date); await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().UpdateAsync(member, true);
				paths.Add((Factory.SeededStandardId, division.Id)); members.Add(member);
			}
			var batch = services.GetRequiredService<AttendanceDailyRegisterProjection>(); var rosters = services.GetRequiredService<AttendanceRosterService>();
			async Task AssertParity(DateTime day)
			{
				var states = await batch.ReadAsync(year.Id, paths.Take(3).ToArray(), day);
				foreach (var state in states)
				{
					var roster = await rosters.LoadAsync(year.Id, state.StandardId, state.DivisionId, day);
					state.ExpectedStudents.ShouldBe(roster.Members.Count(m => m.Expected)); state.Provisional.ShouldBe(roster.SchoolDay.IsProvisional);
					var revisionChanged = roster.Day?.IsSubmitted == true && roster.Day.SubmittedRosterRevision != roster.Revision;
					if (roster.Day?.IsSubmitted != true) state.Status.ShouldBe(roster.Day == null ? "missing" : "draft");
					else if (revisionChanged) state.Status.ShouldBe("review");
				}
			}
			await AssertParity(date); // Missing calendar/provisional uses the same owning evaluator.
			var calendar = new AcademicCalendar(Guid.NewGuid(), tenant, Factory.SeededMediumId, year.Id, "Dashboard batch parity", year.StartDate, year.EndDate, isDefault: true);
			calendar.Publish(id); var calendars = services.GetRequiredService<IRepository<AcademicCalendar, Guid>>();
			await calendars.InsertAsync(calendar, true);
			var calendarDays = services.GetRequiredService<IRepository<AcademicCalendarDay, Guid>>();
			await calendarDays.InsertAsync(new AcademicCalendarDay(Guid.NewGuid(), tenant, calendar.Id, date, DayType.WorkingDay, "Regular day"), true);
			await calendarDays.InsertAsync(new AcademicCalendarDay(Guid.NewGuid(), tenant, calendar.Id, date.AddDays(1), DayType.Holiday, "Holiday"), true);
			await AssertParity(date); await AssertParity(date.AddDays(1));
			(await batch.ReadAsync(year.Id, paths.Take(3).ToArray(), date)).ShouldAllBe(s => s.ExpectedStudents == 1);
			(await batch.ReadAsync(year.Id, paths.Take(3).ToArray(), date.AddDays(1))).ShouldAllBe(s => s.ExpectedStudents == 0);
			var manager = services.GetRequiredService<AttendanceManager>(); var dayHeader = await manager.GetOrCreateAttendanceDayAsync(year.Id, paths[0].StandardId, paths[0].DivisionId, date);
			await AssertParity(date); // Draft.
			var rosterBefore = await rosters.LoadAsync(year.Id, paths[0].StandardId, paths[0].DivisionId, date);
			await manager.MarkAttendanceAsync(dayHeader.Id, [new AttendanceManager.AttendanceEntryInput { StudentId = members[0].StudentId,
				EnrollmentId = members[0].Id, Date = date, Status = AttendanceStatus.Absent, PresentCredit = 0, PolicyJson = JsonSerializer.Serialize(rosterBefore.Options) }]);
			dayHeader.SaveRevision(Guid.NewGuid(), "submit", true, rosterBefore.Revision, JsonSerializer.Serialize(rosterBefore.Options));
			await services.GetRequiredService<IAttendanceDayRepository>().UpdateAsync(dayHeader, true);
			(await batch.ReadAsync(year.Id, paths.Take(1).ToArray(), date)).Single().Status.ShouldBe("submitted");
			var leaveType = await services.GetRequiredService<StudentLeaveTypeManager>().CreateAsync("Batch " + Guid.NewGuid().ToString("N")[..8], "#334455");
			await services.GetRequiredService<IStudentLeaveTypeRepository>().InsertAsync(leaveType, true);
			var student = (await services.GetRequiredService<IStudentRepository>().GetQueryableWithoutAutoIncludesAsync()).Single(s => s.Id == members[0].StudentId);
			var leave = await services.GetRequiredService<StudentLeaveManager>().CreateAsync(student, leaveType.Id, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), "Parity", null, null, null);
			var schoolMidnight = new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.FromHours(5.5));
			leave.UpdateLeaveRequest(leaveType.Id, false, schoolMidnight, schoolMidnight, "Historical parity fixture", null, null, null);
			leave.Approve(); await services.GetRequiredService<IStudentLeaveRepository>().InsertAsync(leave, true);
			await AssertParity(date);
			(await batch.ReadAsync(year.Id, paths.Take(1).ToArray(), date)).Single().Status.ShouldBe("review"); // Leave source/revision changed.
			var counter = ((StudentModuleDashboardApiTestFactory)Factory).Counter; var counts = new List<int>();
			foreach (var size in new[] { 1, 10, 50 })
			{
				counter.Begin(); var projected = await batch.ReadAsync(year.Id, paths.Take(size).ToArray(), date); var queries = counter.End();
				projected.Count.ShouldBe(size); counts.Add(queries.Count); _output.WriteLine($"Daily register projection: {size} divisions / {queries.Count} SQL reads");
			}
			counts.Distinct().Count().ShouldBe(1); counts[0].ShouldBeLessThanOrEqualTo(50);
			await calendarDays.DeleteAsync(d => d.AcademicCalendarId == calendar.Id, true); await calendars.DeleteAsync(calendar, true);
		});
	}
}
