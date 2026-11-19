using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
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
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Students.Attendance;

public partial class AttendanceReadPerformance_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Leave_list_returns_identical_paged_details_without_loading_attachments_or_student_aggregates(bool dateFilter)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
			var fixture = await SeedAsync(3);
			var repository = GetRequiredService<IStudentLeaveRepository>();
			var day = new DateTimeOffset(2026, 9, 1, 18, 30, 0, TimeSpan.Zero);
			var ids = new List<Guid>();
			for (var index = 0; index < 3; index++)
			{
				var item = new StudentLeave(Guid.NewGuid(), fixture.TenantId, fixture.Members[index].StudentId, fixture.MediumId,
					fixture.StandardId, fixture.DivisionId, fixture.LeaveTypeId, index == 2, day, index == 2 ? null : day.AddDays(1),
					$"Description {index}", index == 2 ? null : new byte[16000], index == 2 ? null : $"note-{index}.pdf", "application/pdf");
				if (index == 0) item.Approve("Preserved approval remarks");
				if (index == 1) item.Reject("Preserved reason", "Preserved rejection remarks");
				await repository.InsertAsync(item, true); ids.Add(item.Id);
			}
			var service = LeaveService(fixture.Scope);
			var db = await DatabaseAsync();
			// Compare two persisted reads. SQLite round-trips decimal scale and DateTime
			// kind differently from the newly inserted, still-tracked CLR entity.
			db.ChangeTracker.Clear();
			var expected = JsonSerializer.Serialize(await service.GetAsync(ids[1]));
			db.ChangeTracker.Clear();
			using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
			var page = await service.GetListAsync(new() {
				AcademicYearId = fixture.YearId, OrganizationId = fixture.StageId, Sorting = "studentName asc", SkipCount = 1, MaxResultCount = 1,
				StartDate = dateFilter ? day : null, EndDate = dateFilter ? day.AddDays(2) : null });
			page.TotalCount.ShouldBe(3); page.Items.Count.ShouldBe(1);
			JsonSerializer.Serialize(page.Items.Single()).ShouldBe(expected);
			page.Items.Single().HasAttachment.ShouldBeTrue();
			page.Items.Single().AttachmentFileName.ShouldBe("note-1.pdf");
			var reads = capture.ReadCommands.Where(sql => sql.Contains("AppStudentLeaves", StringComparison.Ordinal)).ToList();
			output.WriteLine($"Synthetic SQLite leave page=1/3; date filter={dateFilter}; leave reads={reads.Count}.");
			reads.ShouldAllBe(sql => !sql.Contains("AttachmentContentType", StringComparison.Ordinal) &&
				!sql.Contains("MobileAttachmentsJson", StringComparison.Ordinal));
			db.ChangeTracker.Entries<StudentLeave>().ShouldBeEmpty();
			db.ChangeTracker.Entries<Student>().ShouldBeEmpty();
		});
	}

	[Fact]
	public async Task Register_batches_report_reads_when_the_page_spans_multiple_divisions()
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
			var fixture = await SeedAsync(4); await SplitDivisionsAsync(fixture);
			var db = await DatabaseAsync(); db.ChangeTracker.Clear();
			var register = Register(fixture.Scope);
			var date = new DateTime(2026, 9, 1);
			int singleEntries; int singleStudents;
			using (var capture = new AcademicQueryCounter(db.Database.GetDbConnection()))
			{
				(await register.BuildRowsAsync(fixture.YearId, date, date, fixture.Members.Take(1).ToArray())).Count.ShouldBe(1);
				singleEntries = capture.ReadCommands.Count(sql => sql.Contains("AppAttendanceEntries", StringComparison.Ordinal));
				singleStudents = capture.ReadCommands.Count(sql => sql.Contains("AppStudents", StringComparison.Ordinal));
			}
			db.ChangeTracker.Clear();
			using var pageCapture = new AcademicQueryCounter(db.Database.GetDbConnection());
			var rows = await register.BuildRowsAsync(fixture.YearId, date, date, fixture.Members, includeDaily: true);
			rows.Count.ShouldBe(4);
			rows.ShouldAllBe(row => row.ExpectedDays == 0 && row.RecordedDays == 0 && row.Percentage == null && row.IsProvisional);
			rows.Select(row => row.FullName).Order().ShouldBe(Enumerable.Range(0, 4).Select(index => $"Scalar Middle Student {index}"));
			var pageEntries = pageCapture.ReadCommands.Count(sql => sql.Contains("AppAttendanceEntries", StringComparison.Ordinal));
			var pageStudents = pageCapture.ReadCommands.Count(sql => sql.Contains("AppStudents", StringComparison.Ordinal));
			output.WriteLine($"Synthetic SQLite register paths=1/4; entry reads={singleEntries}/{pageEntries}; student reads={singleStudents}/{pageStudents}.");
			pageEntries.ShouldBe(singleEntries); pageStudents.ShouldBe(singleStudents);
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Register_preserves_moved_enrollment_snapshot_credits_and_partial_scope(bool partialScope)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
			var fixture = await SeedAsync(1);
			var nextDivision = await GetRequiredService<DivisionManager>().CreateAsync(fixture.StandardId, "B", 100);
			await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(nextDivision, true);
			var member = fixture.Members.Single(); var start = new DateTime(2026, 9, 1);
			member.ChangePlacement(fixture.StandardId, nextDivision.Id, 8, start.AddDays(1));
			await GetRequiredService<IRepository<StudentEnrollment, Guid>>().UpdateAsync(member, true);
			await SaveSnapshotAsync(fixture, member, fixture.DivisionId, start, AttendanceStatus.Present, .5, .5);
			await SaveSnapshotAsync(fixture, member, nextDivision.Id, start.AddDays(1), AttendanceStatus.Absent, 0, 1);
			var scope = partialScope ? Scope(new UserScopeContext { TenantId = fixture.TenantId,
				AcademicYearIds = [fixture.YearId], DivisionIds = [fixture.DivisionId] }) : fixture.Scope;
			(await DatabaseAsync()).ChangeTracker.Clear();
			var row = (await Register(scope).BuildRowsAsync(fixture.YearId, start, start.AddDays(1), fixture.Members, includeDaily: true)).Single();
			row.ExpectedDays.ShouldBe(partialScope ? 1 : 2); row.RecordedDays.ShouldBe(partialScope ? 1 : 2);
			row.PresentCredit.ShouldBe(.5); row.DenominatorDays.ShouldBe(partialScope ? .5 : 1.5);
			row.Percentage.ShouldBe(partialScope ? 100d : 33.33d); row.IsProvisional.ShouldBe(partialScope);
			row.Daily[0].Status.ShouldBe(AttendanceStatus.Present); row.Daily[0].HalfDay.ShouldBeTrue();
			if (!partialScope) row.Daily[1].Status.ShouldBe(AttendanceStatus.Absent);
		});
	}

	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	public async Task Cumulative_dashboard_batches_report_reads_across_divisions(int divisions)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
			var fixture = await SeedAsync(divisions); await SplitDivisionsAsync(fixture);
			var service = Dashboard(fixture.Scope);
			var db = await DatabaseAsync(); db.ChangeTracker.Clear();
			using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await service.GetAttendanceDashboardSummaryAsync(new() { AcademicYearId = fixture.YearId,
				StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 1) });
			result.Classes.ShouldHaveSingleItem().Divisions.Count.ShouldBe(divisions);
			result.SchoolAttendancePercentage.ShouldBeNull(); result.ExpectedDays.ShouldBe(0); result.IsProvisional.ShouldBeTrue();
			var entries = capture.ReadCommands.Count(sql => sql.Contains("AppAttendanceEntries", StringComparison.Ordinal));
			output.WriteLine($"Synthetic SQLite cumulative dashboard divisions={divisions}; entry reads={entries}.");
			entries.ShouldBe(2);
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Cumulative_dashboard_preserves_raw_legacy_ties_and_nonmidnight_history_paths(bool nonMidnight)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			using var tenant = GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
			var fixture = await SeedAsync(1);
			var b = await GetRequiredService<DivisionManager>().CreateAsync(fixture.StandardId, "Legacy B", 100);
			var c = await GetRequiredService<DivisionManager>().CreateAsync(fixture.StandardId, "Legacy C", 100);
			await GetRequiredService<IRepository<Division, Guid>>().InsertManyAsync([b, c], true);
			var start = new DateTime(2026, 9, 1); var member = fixture.Members.Single();
			var currentDivision = nonMidnight ? c.Id : b.Id;
			member.ChangePlacement(fixture.StandardId, currentDivision, 2, start.AddDays(2));
			AttendanceEnrollmentPlacement[] history = nonMidnight
				? new[] { new AttendanceEnrollmentPlacement(start, fixture.StandardId, fixture.DivisionId, 1, true),
					new AttendanceEnrollmentPlacement(start.AddHours(1), fixture.StandardId, b.Id, 2, true),
					new AttendanceEnrollmentPlacement(start.AddHours(2), fixture.StandardId, c.Id, 3, true) }
				: [new AttendanceEnrollmentPlacement(start, fixture.StandardId, fixture.DivisionId, 1, true),
					new AttendanceEnrollmentPlacement(start, fixture.StandardId, b.Id, 2, true)];
			typeof(StudentEnrollment).GetProperty(nameof(StudentEnrollment.AttendancePlacementHistoryJson))!.SetValue(member, JsonSerializer.Serialize(history));
			await GetRequiredService<IRepository<StudentEnrollment, Guid>>().UpdateAsync(member, true);
			(await DatabaseAsync()).ChangeTracker.Clear();
			// No attendance entries exist to recover an omitted historical path. This
			// exercises the public dashboard caller, not only the domain helper.
			var result = await Dashboard(fixture.Scope).GetAttendanceDashboardSummaryAsync(new() {
				AcademicYearId = fixture.YearId, StartDate = start, EndDate = nonMidnight ? start.AddDays(1) : start });
			result.Classes.ShouldHaveSingleItem().Divisions.Select(row => row.DivisionId!.Value).Order()
				.ShouldBe(new[] { fixture.DivisionId, currentDivision }.Order());
		});
	}

	private async Task SplitDivisionsAsync(Fixture fixture)
	{
		for (var index = 1; index < fixture.Members.Length; index++)
		{
			var division = await GetRequiredService<DivisionManager>().CreateAsync(fixture.StandardId, $"D{index}", 100);
			await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
			fixture.Members[index].ChangePlacement(fixture.StandardId, division.Id, index + 1, new DateTime(2026, 9, 1));
			await GetRequiredService<IRepository<StudentEnrollment, Guid>>().UpdateAsync(fixture.Members[index], true);
		}
	}

	private async Task SaveSnapshotAsync(Fixture fixture, StudentEnrollment member, Guid divisionId, DateTime date,
		AttendanceStatus status, double credit, double denominator)
	{
		var day = new AttendanceDay(Guid.NewGuid(), fixture.TenantId, fixture.YearId, fixture.StandardId, divisionId, date, Guid.NewGuid());
		day.SaveRevision(Guid.NewGuid(), "fixture", true, "fixture", "{}");
		await GetRequiredService<IAttendanceDayRepository>().InsertAsync(day, true);
		var entry = new AttendanceEntry(Guid.NewGuid(), fixture.TenantId, day.Id, member.StudentId, status, Guid.NewGuid());
		entry.SetDailyDetails(member.Id, date, false, status == AttendanceStatus.Present, credit, false, null, "{}", denominatorCredit: denominator);
		await GetRequiredService<IAttendanceEntryRepository>().InsertAsync(entry, true);
	}

	private AttendanceReportService Reports(StudentScopeAccessService scope) => new(null!, GetRequiredService<AttendanceApplicabilityResolver>(),
		GetRequiredService<IRepository<AcademicYear, Guid>>(), GetRequiredService<IAttendanceDayRepository>(),
		GetRequiredService<IAttendanceEntryRepository>(), GetRequiredService<IStudentRepository>(), scope) {
		LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>() };
	private StudentListAppService Dashboard(StudentScopeAccessService scope) => new(GetRequiredService<IStudentRepository>(), GetRequiredService<IRepository<AttendanceEntry, Guid>>(),
		GetRequiredService<IAttendanceDayRepository>(), GetRequiredService<IRepository<StudentEnrollment, Guid>>(),
		GetRequiredService<IRepository<AcademicYear, Guid>>(), Reports(scope), null!, null!, null!, null!,
		GetRequiredService<IRepository<Standard, Guid>>(), GetRequiredService<IRepository<Division, Guid>>(), null!, null!, scope) {
		LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>() };
	private AttendanceRegisterQueryService Register(StudentScopeAccessService scope) => new(GetRequiredService<IRepository<StudentEnrollment, Guid>>(),
		null!, scope, GetRequiredService<IAttendanceDayRepository>(), GetRequiredService<IAttendanceEntryRepository>(), Reports(scope),
		GetRequiredService<IRepository<Division, Guid>>()) { LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>() };
	private StudentScopeAccessService Scope(UserScopeContext context)
	{
		var scopes = Substitute.For<IUserScopeAccessService>(); scopes.GetCurrentUserScopeAsync().Returns(Task.FromResult(context));
		return new(scopes, GetRequiredService<IStudentRepository>(), GetRequiredService<IRepository<Standard, Guid>>(),
			GetRequiredService<IRepository<Division, Guid>>(), GetRequiredService<IRepository<EducationMediumStage, Guid>>(),
			GetRequiredService<IRepository<EducationMedium, Guid>>(), GetRequiredService<IRepository<EducationBoard, Guid>>(),
			GetRequiredService<IRepository<InquiryPreference, Guid>>()) { LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>() };
	}
}
