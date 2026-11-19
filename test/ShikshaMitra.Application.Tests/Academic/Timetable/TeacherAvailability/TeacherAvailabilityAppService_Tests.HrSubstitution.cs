using System;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Identity;
using Volo.Abp.Users;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Security.UserScope;
using Xunit;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public partial class TeacherAvailabilityAppService_Tests
{
	private async Task GrantCurrentActorScopeAsync(Guid tenantId)
	{
		var actorId = GetRequiredService<ICurrentUser>().Id!.Value;
		if (await _identityUserRepository.FindAsync(actorId) == null)
		{
			await _identityUserRepository.InsertAsync(new IdentityUser(actorId,
				$"scoped-{actorId:N}", $"{actorId:N}@test.local", tenantId), autoSave: true);
		}
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
			new UserScopeAssignment(Guid.NewGuid(), tenantId, actorId), autoSave: true);
	}
	[Fact]
	public async Task AttendanceSync_Should_Allow_NonAdmin_And_Preserve_Other_Source_Availability()
	{
		var employee = await CreateEmployeeAsync(Guid.NewGuid(), "HR", "Teacher");
		var date = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
		var manual = await _availabilityRepository.InsertAsync(new TeacherAvailability(
			Guid.NewGuid(), null, employee.EmployeeName, date,
			new TimeOnly(10, 0), new TimeOnly(11, 0), TeacherAvailabilityStatus.Meeting,
			TeacherAvailabilitySource.Manual, "Scheduled meeting", false, false,
			teacherId: employee.Id), autoSave: true);
		var attendance = await _employeeAttendanceRepository.InsertAsync(new EmployeeAttendance
		{
			EmployeeId = employee.Id, AttendanceDate = DateOnly.FromDateTime(date),
			StatusType = EmployeeAttendanceStatusType.FullDayLeave
		}, autoSave: true);
		using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "hr-officer")))
		{
			var sync = GetRequiredService<TakeAttendanceTeacherAvailabilitySyncService>();
			await sync.SyncAsync(new TakeAttendanceSavedEntry(employee, attendance));
			await sync.SyncAsync(new TakeAttendanceSavedEntry(employee, attendance));
			var rows = await _availabilityRepository.GetListAsync(x => !x.IsCancelled);
			rows.Count.ShouldBe(2);
			rows.ShouldContain(x => x.Id == manual.Id);
			rows.ShouldContain(x => x.SourceType == TeacherAvailabilitySource.Attendance && x.SourceEntityId == attendance.Id);
			attendance.StatusType = EmployeeAttendanceStatusType.Present;
			await sync.SyncAsync(new TakeAttendanceSavedEntry(employee, attendance));
			(await _availabilityRepository.GetListAsync(x => !x.IsCancelled)).ShouldHaveSingleItem().Id.ShouldBe(manual.Id);
		}
	}

	[Fact]
	public async Task AffectedLectures_Should_Not_Expose_Timetable_To_User_Without_Scope()
	{
		var tenantId = Guid.NewGuid();
		using (GetRequiredService<ICurrentTenant>().Change(tenantId))
		{
			var employee = await CreateEmployeeAsync(Guid.NewGuid(), "Scoped", "Teacher");
			var context = await SeedTimetableContextAsync(tenantId, employee.Id, "HR scope year",
				new DateTime(2026, 4, 1), new DateTime(2027, 3, 31), "6", new TimeOnly(10, 0), new TimeOnly(11, 0));
			using (_currentPrincipalAccessor.Change(BuildPrincipal(Guid.NewGuid(), "hr-officer")))
			{
				var result = await _teacherAvailabilityAppService.GetAffectedLecturesAsync(new CreateTeacherAvailabilityDto
				{
					TeacherId = employee.Id, AcademicYearId = context.AcademicYearId,
					AvailabilityDate = new DateTime(2026, 10, 5), StartTime = "08:00", EndTime = "15:00"
				});
				result.Items.ShouldBeEmpty();
			}
		}
	}
}
