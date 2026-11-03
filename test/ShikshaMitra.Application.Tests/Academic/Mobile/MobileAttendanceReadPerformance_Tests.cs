using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Attendance.Settings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;
using Volo.Abp;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	[Theory]
	[InlineData("detail")]
	[InlineData("calendar")]
	[InlineData("overlap")]
	public Task Mobile_leave_metadata_and_overlap_checks_preserve_results_without_reading_file_content(string operation) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var fixture = await Seed(tenantId, true);
		using var identity = SignIn(fixture.User.Id);
		var type = await Insert(await GetRequiredService<StudentLeaveTypeManager>().CreateAsync("Read metadata", "#123456"));
		var date = MobileAcademicContextAppService.SchoolToday;
		var leave = await Insert(await GetRequiredService<StudentLeaveManager>().CreateAsync(fixture.Student, type.Id, true,
			new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.FromHours(5.5)), null,
			"Synthetic leave", new byte[65536], "fixture.pdf", "application/pdf"));
		if (operation == "calendar")
		{
			leave.Approve("School decision");
			await GetRequiredService<IRepository<StudentLeave, Guid>>().UpdateAsync(leave, true);
		}
		var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		db.ChangeTracker.Clear();
		var service = GetRequiredService<IMobileAttendanceAppService>();
		using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
		if (operation == "detail")
		{
			var result = await service.GetLeaveAsync(leave.Id, new() { StudentId = fixture.Student.Id, AcademicYearId = fixture.Year.Id });
			result.Id.ShouldBe(leave.Id);
			result.Status.ShouldBe("pending");
			result.Reason.ShouldBe("Read metadata");
			result.CanCancel.ShouldBeTrue();
		}
		else if (operation == "calendar")
		{
			var result = await service.GetCalendarAsync(new() { StudentId = fixture.Student.Id, AcademicYearId = fixture.Year.Id, Year = date.Year, Month = date.Month });
			var day = result.Single(item => item.Date == DateOnly.FromDateTime(date));
			day.Status.ShouldBe("approvedLeave");
			day.LeaveRequestId.ShouldBe(leave.Id);
		}
		else
		{
			await Should.ThrowAsync<BusinessException>(() => service.SaveLeaveAsync(null, new()
			{
				StudentId = fixture.Student.Id, AcademicYearId = fixture.Year.Id, LeaveTypeId = type.Id,
				StartDate = DateOnly.FromDateTime(date), EndDate = DateOnly.FromDateTime(date), IsHalfDay = true, Description = "Conflicting synthetic leave"
			}));
			(await GetRequiredService<IRepository<StudentLeave, Guid>>().CountAsync(item => item.StudentId == fixture.Student.Id)).ShouldBe(1);
		}
		var commands = capture.ReadCommands.Where(sql => sql.Contains("FROM \"AppStudentLeaves\"", StringComparison.Ordinal)).ToArray();
		commands.ShouldNotBeEmpty();
		commands.ShouldAllBe(sql => !sql.Contains("\"Attachment\"", StringComparison.Ordinal), "Metadata and overlap checks must keep binary content on the download path.");
		db.ChangeTracker.Entries<StudentLeave>().ShouldBeEmpty();
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Mobile_leave_pages_keep_school_year_status_and_paging_without_loading_attachment_bytes(bool parent) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var fixture = await Seed(tenantId, parent);
		using var identity = SignIn(fixture.User.Id);
		var type = await Insert(await GetRequiredService<StudentLeaveTypeManager>().CreateAsync("Read metadata", "#123456"));
		var manager = GetRequiredService<StudentLeaveManager>();
		async Task<StudentLeave> Add(DateTime date) => await Insert(await manager.CreateAsync(fixture.Student, type.Id, true,
			new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.FromHours(5.5)), null,
			"Synthetic leave", new byte[65536], "fixture.pdf", "application/pdf"));
		for (var index = 1; index <= 12; index++) await Add(fixture.Year.StartDate.Date.AddDays(-index));
		var approved = await Add(MobileAcademicContextAppService.SchoolToday);
		approved.Approve("School decision");
		await GetRequiredService<IRepository<StudentLeave, Guid>>().UpdateAsync(approved, true);
		var pending = await Add(MobileAcademicContextAppService.SchoolToday.AddDays(1));
		var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		db.ChangeTracker.Clear();
		var service = GetRequiredService<IMobileAttendanceAppService>();
		var input = new MobileListInput { StudentId = fixture.Student.Id, AcademicYearId = fixture.Year.Id, PageNumber = 1, PageSize = 1, Status = "pending" };
		using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
		var page = await service.GetLeavesAsync(input);
		page.TotalCount.ShouldBe(1);
		page.Items.Single().Id.ShouldBe(pending.Id);
		page.Items.Single().StartDate.ShouldBe(DateOnly.FromDateTime(MobileAcademicContextAppService.SchoolToday.AddDays(1)));
		page.Items.Single().CanEdit.ShouldBe(parent);
		page.Items.Single().CanCancel.ShouldBe(parent);
		page.Items.Single().Attachments.ShouldBeEmpty();
		var leaveSql = capture.ReadCommands.Where(sql => sql.Contains("FROM \"AppStudentLeaves\"", StringComparison.Ordinal)).ToArray();
		Console.WriteLine($"SQLite mobile leave history=14, page=1, role={(parent ? "guardian" : "student")}: {leaveSql.Length} leave reads; tracked leaves={db.ChangeTracker.Entries<StudentLeave>().Count()}.");
		leaveSql.ShouldNotBeEmpty();
		leaveSql.ShouldAllBe(sql => !sql.Contains("AttachmentContent", StringComparison.Ordinal), "Mobile metadata pages must not transfer historical attachment bytes.");
		db.ChangeTracker.Entries<StudentLeave>().ShouldBeEmpty();
		input.Status = "approved";
		(await service.GetLeavesAsync(input)).Items.Single().Id.ShouldBe(approved.Id);
		input.Status = "unknown-status";
		(await service.GetLeavesAsync(input)).TotalCount.ShouldBe(0);
	});
}
