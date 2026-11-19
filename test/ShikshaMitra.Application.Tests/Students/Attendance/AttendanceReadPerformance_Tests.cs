using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.Students.Attendance;

public partial class AttendanceReadPerformance_Tests(ITestOutputHelper output)
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(1, false)]
	[InlineData(24, false)]
	[InlineData(24, true)]
	public async Task Leave_status_counts_read_filtered_metadata_once_and_preserve_all_three_statuses(int perStatus, bool dateFilter)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
			var fixture = await SeedAsync(2);
			var repository = GetRequiredService<IStudentLeaveRepository>();
			var day = new DateTimeOffset(2026, 9, 1, 18, 30, 0, TimeSpan.Zero);
			StudentLeave Leave(Guid studentId, StudentLeaveStatusEnum status, DateTimeOffset? date = null)
			{
				var item = new StudentLeave(Guid.NewGuid(), fixture.TenantId, studentId, fixture.MediumId, fixture.StandardId,
					fixture.DivisionId, fixture.LeaveTypeId, false, date ?? day, date ?? day, "Matching leave", new byte[4000], "note", "text/plain");
				if (status == StudentLeaveStatusEnum.Approved) item.Approve();
				if (status == StudentLeaveStatusEnum.Rejected) item.Reject("Rejected fixture");
				if (status == StudentLeaveStatusEnum.Cancelled) item.Cancel();
				return item;
			}
			foreach (var status in new[] { StudentLeaveStatusEnum.Pending, StudentLeaveStatusEnum.Approved, StudentLeaveStatusEnum.Rejected })
				await repository.InsertManyAsync(Enumerable.Range(0, perStatus).Select(_ => Leave(fixture.Members[0].StudentId, status)), true);
			await repository.InsertManyAsync([
				Leave(fixture.Members[1].StudentId, StudentLeaveStatusEnum.Pending),
				Leave(fixture.Members[0].StudentId, StudentLeaveStatusEnum.Cancelled),
				Leave(fixture.Members[0].StudentId, StudentLeaveStatusEnum.Pending, day.AddDays(-3))], true);
			var db = await DatabaseAsync(); db.ChangeTracker.Clear();
			using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await LeaveService(fixture.Scope).GetStatusCountsAsync(new() {
				AcademicYearId = fixture.YearId, StudentId = fixture.Members[0].StudentId, OrganizationId = fixture.StageId,
				Status = StudentLeaveStatusEnum.Rejected, StartDate = dateFilter ? day : null, EndDate = dateFilter ? day : null,
				TableFilters = JsonSerializer.Serialize((dateFilter ? new[] {
					new StudentLeaveTableFilter("status", "equals", "2", null, null),
					new StudentLeaveTableFilter("totalDays", "equals", "1", null, null) } : new[] {
					new StudentLeaveTableFilter("status", "equals", "2", null, null) })) });
			result.PendingCount.ShouldBe(perStatus + (dateFilter ? 0 : 1));
			result.ApprovedCount.ShouldBe(perStatus); result.RejectedCount.ShouldBe(perStatus);
			var reads = capture.ReadCommands.Where(sql => sql.Contains("AppStudentLeaves", StringComparison.Ordinal)).ToList();
			output.WriteLine($"Synthetic SQLite leaves/status={perStatus}; date filter={dateFilter}; leave reads={reads.Count}.");
			reads.Count.ShouldBe(1);
			reads.ShouldAllBe(sql => !sql.Contains("Attachment", StringComparison.Ordinal));
			db.ChangeTracker.Entries<StudentLeave>().ShouldBeEmpty();
			db.ChangeTracker.Entries<StudentEnrollment>().ShouldBeEmpty();
		});
	}

	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	public async Task Report_reads_scalar_snapshots_without_tracking_student_or_entry_aggregates(int memberCount)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
			var fixture = await SeedAsync(memberCount);
			var start = new DateTime(2026, 9, 1);
			var day = new AttendanceDay(Guid.NewGuid(), fixture.TenantId, fixture.YearId, fixture.StandardId, fixture.DivisionId, start, Guid.NewGuid());
			day.SaveRevision(Guid.NewGuid(), "fixture", true, "fixture", "{}");
			await GetRequiredService<IAttendanceDayRepository>().InsertAsync(day, true);
			foreach (var member in fixture.Members)
			{
				var entry = new AttendanceEntry(Guid.NewGuid(), fixture.TenantId, day.Id, member.StudentId, AttendanceStatus.Present, Guid.NewGuid(), "Preserved remark");
				entry.SetDailyDetails(member.Id, start, true, true, .5, false, null, new string('x', 16000), 3, .5);
				await GetRequiredService<IAttendanceEntryRepository>().InsertAsync(entry, true);
			}
			var db = await DatabaseAsync(); db.ChangeTracker.Clear();
			using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
			var service = new AttendanceReportService(null!, GetRequiredService<AttendanceApplicabilityResolver>(),
				GetRequiredService<IRepository<AcademicYear, Guid>>(), GetRequiredService<IAttendanceDayRepository>(),
				GetRequiredService<IAttendanceEntryRepository>(), GetRequiredService<IStudentRepository>(), fixture.Scope) {
				LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>() };
			var rows = await service.BuildAsync(fixture.YearId, fixture.StandardId, fixture.DivisionId, start, start.AddDays(1), fixture.Members, true);
			rows.Count.ShouldBe(memberCount);
			rows.ShouldAllBe(row => row.ExpectedDays == 1 && row.RecordedDays == 1 && row.MissingDays == 0 &&
				row.PresentCredit == .5 && row.DenominatorDays == .5 && row.Percentage == 100 && row.IsProvisional);
			rows.ShouldAllBe(row => row.Daily[0].Late && row.Daily[0].HalfDay && row.Daily[0].Remarks == "Preserved remark" &&
				row.Daily[0].Expected && row.Daily[0].Recorded && !row.Daily[0].IsProvisional && !row.Daily[1].Expected);
			var entrySql = capture.ReadCommands.Single(sql => sql.Contains("AppAttendanceEntries", StringComparison.Ordinal));
			output.WriteLine($"Synthetic SQLite report members={memberCount}; reads={capture.ReadCount}; entry reads=1.");
			entrySql.ShouldNotContain("MarkedByUserId");
			entrySql.ShouldNotContain("GuardianAbsenceReason");
			db.ChangeTracker.Entries<Student>().ShouldBeEmpty();
			db.ChangeTracker.Entries<AttendanceEntry>().ShouldBeEmpty();
		});
	}

	private StudentLeaveAppService LeaveService(StudentScopeAccessService scope) => new(
		GetRequiredService<IStudentLeaveRepository>(), GetRequiredService<StudentLeaveManager>(), GetRequiredService<IStudentRepository>(),
		scope, GetRequiredService<IRepository<AcademicYear, Guid>>(), GetRequiredService<IRepository<StudentEnrollment, Guid>>(),
		GetRequiredService<AttendancePolicyResolver>()) { LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>() };

	private async Task<Fixture> SeedAsync(int members)
	{
		var tenantId = GetRequiredService<ICurrentTenant>().Id!.Value;
		var stageId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().GetAsync(stageId);
		var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().GetAsync(stage.EducationMediumId);
		var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(medium.EducationBoardId);
		var standard = await GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Attendance read class", 7, null);
		await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
		var division = await GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "A", 100);
		await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
		var type = new StudentLeaveType(Guid.NewGuid(), tenantId, "Read fixture", "#000000");
		await GetRequiredService<IRepository<StudentLeaveType, Guid>>().InsertAsync(type, true);
		var enrollments = new StudentEnrollment[members];
		for (var index = 0; index < members; index++)
		{
			var student = new Student(Guid.NewGuid(), tenantId, "Scalar", "Middle", $"Student {index}", board.AcademicYearId,
				board.Id, medium.Id, stage.Id, standard.Id, division.Id, status: StudentLifeCycleStatusEnum.Enrolled);
			await GetRequiredService<IStudentRepository>().InsertAsync(student, true);
			var member = StudentEnrollment.Create(Guid.NewGuid(), tenantId, student.Id, board.AcademicYearId, standard.Id, division.Id, index + 1);
			member.ConfirmAttendanceStart(new DateTime(2026, 9, 1));
			await GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(member, true);
			enrollments[index] = member;
		}
		var scopes = Substitute.For<IUserScopeAccessService>();
		scopes.GetCurrentUserScopeAsync().Returns(Task.FromResult(new UserScopeContext { TenantId = tenantId, HasWildcardAcademicYear = true }));
		var scope = new StudentScopeAccessService(scopes, GetRequiredService<IStudentRepository>(),
			GetRequiredService<IRepository<Standard, Guid>>(), GetRequiredService<IRepository<Division, Guid>>(),
			GetRequiredService<IRepository<EducationMediumStage, Guid>>(), GetRequiredService<IRepository<EducationMedium, Guid>>(),
			GetRequiredService<IRepository<EducationBoard, Guid>>(), GetRequiredService<IRepository<InquiryPreference, Guid>>()) {
			LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>() };
		return new(tenantId, board.AcademicYearId, medium.Id, stage.Id, standard.Id, division.Id, type.Id, enrollments, scope);
	}

	private Task<ShikshaMitraDbContext> DatabaseAsync() => GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
	private sealed record Fixture(Guid TenantId, Guid YearId, Guid MediumId, Guid StageId, Guid StandardId, Guid DivisionId,
		Guid LeaveTypeId, StudentEnrollment[] Members, StudentScopeAccessService Scope);
}
