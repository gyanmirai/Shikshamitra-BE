using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Attendance.Settings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Leave_grouped_status_counts_Postgres_match_canonical_filtered_counts_and_keep_scope_and_tenant()
	{
		var fixture = await InTenantAsync(async services =>
		{
			var students = services.GetRequiredService<IStudentRepository>();
			var leaves = services.GetRequiredService<IStudentLeaveRepository>();
			var type = await services.GetRequiredService<StudentLeaveTypeManager>().CreateAsync("Grouped provider", "#123456");
			await services.GetRequiredService<IRepository<StudentLeaveType, Guid>>().InsertAsync(type, true);
			var owners = new Guid[2];
			for (var index = 0; index < owners.Length; index++)
			{
				var student = new Student(Guid.NewGuid(), TenantId, "Grouped", null, $"Owner {index}",
					factory.SeededAcademicYearId, factory.SeededBoardId, factory.SeededMediumId,
					factory.SeededMediumStageId, factory.SeededStandardId);
				await students.InsertAsync(student, true);
				owners[index] = student.Id;
			}
			var start = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.FromHours(5.5));
			async Task Add(Guid studentId, StudentLeaveStatusEnum status, DateTimeOffset date,
				string description = "Group parity", bool longLeave = false)
			{
				var student = await students.GetAsync(studentId);
				var leave = await services.GetRequiredService<StudentLeaveManager>().CreateAsync(student, type.Id,
					false, date, longLeave ? date.AddDays(1) : date, description, new byte[65536],
					"grouped.pdf", "application/pdf");
				if (status == StudentLeaveStatusEnum.Approved) leave.Approve();
				if (status == StudentLeaveStatusEnum.Rejected) leave.Reject("Provider fixture");
				if (status == StudentLeaveStatusEnum.Cancelled) leave.Cancel();
				await leaves.InsertAsync(leave, true);
			}
			foreach (var status in new[] { StudentLeaveStatusEnum.Pending, StudentLeaveStatusEnum.Approved,
				StudentLeaveStatusEnum.Rejected, StudentLeaveStatusEnum.Cancelled })
				await Add(owners[0], status, start);
			await Add(owners[0], StudentLeaveStatusEnum.Pending, start.AddDays(1));
			await Add(owners[1], StudentLeaveStatusEnum.Pending, start);
			await Add(owners[0], StudentLeaveStatusEnum.Pending, start.AddDays(10));
			await Add(owners[0], StudentLeaveStatusEnum.Pending, start, "Different description");
			await Add(owners[0], StudentLeaveStatusEnum.Pending, start, longLeave: true);
			return (OwnerId: owners[0], Start: start);
		});

		await InTenantAsync(async services =>
		{
			var leaves = services.GetRequiredService<IStudentLeaveRepository>();
			var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			db.ChangeTracker.Clear();
			var filters = new[] { new StudentLeaveTableFilter("totalDays", "equals", "1", null, null) };
			var grouped = await leaves.GetStatusCountsAsync(filterText: "Group parity", studentIds: [fixture.OwnerId],
				tenantId: TenantId, startDate: fixture.Start, endDate: fixture.Start.AddDays(1), tableFilters: filters);
			var counts = grouped.ToDictionary(row => row.Status, row => row.Count);
			counts.Count.ShouldBe(4);
			counts[StudentLeaveStatusEnum.Pending].ShouldBe(2);
			counts[StudentLeaveStatusEnum.Approved].ShouldBe(1);
			counts[StudentLeaveStatusEnum.Rejected].ShouldBe(1);
			counts[StudentLeaveStatusEnum.Cancelled].ShouldBe(1);
			foreach (var pair in counts)
				pair.Value.ShouldBe(await leaves.GetCountAsync(filterText: "Group parity", status: pair.Key,
					studentIds: [fixture.OwnerId], tenantId: TenantId, startDate: fixture.Start,
					endDate: fixture.Start.AddDays(1), tableFilters: filters), "Grouped results must match the existing filtered count contract.");
			(await leaves.GetStatusCountsAsync(studentIds: [], tenantId: TenantId)).ShouldBeEmpty();
			(await leaves.GetStatusCountsAsync(studentIds: [fixture.OwnerId], tenantId: Guid.NewGuid())).ShouldBeEmpty();
			db.ChangeTracker.Entries<StudentLeave>().ShouldBeEmpty();
			db.ChangeTracker.Entries<Student>().ShouldBeEmpty();
			return true;
		});
	}

	[StudentRecordsApplicationPostgresFact]
	public async Task Leave_read_projections_Postgres_preserve_root_paging_required_parent_visibility_and_school_offset_boundaries()
	{
		// This fixture creates only synthetic rows in the factory's new disposable database.
		var fixture = await InTenantAsync(async services =>
		{
			var students = services.GetRequiredService<IStudentRepository>();
			var leaves = services.GetRequiredService<IStudentLeaveRepository>();
			var type = await services.GetRequiredService<StudentLeaveTypeManager>().CreateAsync("Provider metadata", "#123456");
			await services.GetRequiredService<IRepository<StudentLeaveType, Guid>>().InsertAsync(type, true);
			var manager = services.GetRequiredService<StudentLeaveManager>();
			var studentIds = new Guid[3];
			var leaveIds = new Guid[3];
			var schoolStart = new DateTimeOffset(DateTime.SpecifyKind(DateTime.UtcNow.AddHours(5.5).Date,
				DateTimeKind.Unspecified), TimeSpan.FromHours(5.5));
			for (var index = 0; index < studentIds.Length; index++)
			{
				var student = new Student(Guid.NewGuid(), TenantId, "Provider", null, $"Student {index}",
					factory.SeededAcademicYearId, factory.SeededBoardId, factory.SeededMediumId,
					factory.SeededMediumStageId, factory.SeededStandardId);
				await students.InsertAsync(student, true);
				studentIds[index] = student.Id;
				var leave = await manager.CreateAsync(student, type.Id, true, schoolStart.AddDays(index), null,
					"Synthetic metadata", new byte[65536], "fixture.pdf", "application/pdf");
				await leaves.InsertAsync(leave, true);
				leaveIds[index] = leave.Id;
			}
			// Portal boundary and overlap checks include a row exactly at each edge.
			var owner = await students.GetAsync(studentIds[2]);
			var before = await manager.CreateAsync(owner, type.Id, true, schoolStart.AddTicks(-1), null,
				"Before school year", null, null, null);
			var end = await manager.CreateAsync(owner, type.Id, true, schoolStart.AddYears(1), null,
				"Next school year", null, null, null);
			await leaves.InsertAsync(before, true);
			await leaves.InsertAsync(end, true);
			await students.DeleteAsync(studentIds[0], true);
			return (Students: studentIds, Leaves: leaveIds, Start: schoolStart, Before: before.Id, End: end.Id);
		});

		await InTenantAsync(async services =>
		{
			var leaves = services.GetRequiredService<IStudentLeaveRepository>();
			var db = await services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			var original = await leaves.GetListWithDetailsAsync(studentIds: fixture.Students, tenantId: TenantId,
				startDate: fixture.Start, endDate: fixture.Start.AddDays(3), sorting: "LeaveDate asc", maxResultCount: 2);
			// The original Include query pages root leaves before hiding the deleted required Student.
			original.Select(leave => leave.Id).ShouldBe([fixture.Leaves[1]]);
			db.ChangeTracker.Clear();
			var projected = await leaves.GetListProjectionAsync(studentIds: fixture.Students, tenantId: TenantId,
				startDate: fixture.Start, endDate: fixture.Start.AddDays(3), sorting: "LeaveDate asc", maxResultCount: 2);
			projected.Select(leave => leave.Id).ShouldBe(original.Select(leave => leave.Id));
			projected.Single().FirstName.ShouldBe("Provider");
			projected.Single().HasAttachment.ShouldBeTrue();
			db.ChangeTracker.Entries<StudentLeave>().ShouldBeEmpty();
			db.ChangeTracker.Entries<Student>().ShouldBeEmpty();

			var ownerId = fixture.Students[2];
			var page = await leaves.GetPortalPageAsync(TenantId, ownerId, fixture.Start, fixture.Start.AddYears(1),
				StudentLeaveStatusEnum.Pending, 0, 1);
			page.TotalCount.ShouldBe(1);
			page.Items.Single().Id.ShouldBe(fixture.Leaves[2]);
			page.Items.Single().LeaveDate.ToUniversalTime().ShouldBe(fixture.Start.AddDays(2).ToUniversalTime());
			(await leaves.FindPortalItemAsync(TenantId, ownerId, fixture.Leaves[2]))!.Id.ShouldBe(fixture.Leaves[2]);
			(await leaves.FindPortalItemAsync(Guid.NewGuid(), ownerId, fixture.Leaves[2])).ShouldBeNull();
			(await leaves.FindPortalItemAsync(TenantId, fixture.Students[1], fixture.Leaves[2])).ShouldBeNull();
			(await leaves.GetPortalCalendarAsync(TenantId, ownerId, fixture.Start, fixture.Start.AddYears(1)))
				.Select(leave => leave.Id).ShouldBe([fixture.Leaves[2]]);
			(await leaves.HasPortalOverlapAsync(TenantId, ownerId, null, fixture.Start.AddDays(2), fixture.Start.AddDays(2))).ShouldBeTrue();
			(await leaves.HasPortalOverlapAsync(TenantId, ownerId, fixture.Leaves[2], fixture.Start.AddDays(2), fixture.Start.AddDays(2))).ShouldBeFalse();
			db.ChangeTracker.Entries<StudentLeave>().ShouldBeEmpty();
			return true;
		});
	}
}
