using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	[Theory]
	[InlineData(false, false, 0)]
	[InlineData(true, false, 0)]
	[InlineData(false, false, 5)]
	[InlineData(true, false, 5)]
	[InlineData(false, true, 5)]
	[InlineData(true, true, 5)]
	public Task Homework_and_assignments_are_visible_on_every_school_date_in_their_range(bool parent, bool crossYear, int duration) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, parent); using var identity = SignIn(f.User.Id);
		// 20:00 UTC belongs to the following school day; due time also crosses UTC midnight.
		var published = crossYear ? new DateTime(2025, 12, 30, 20, 0, 0, DateTimeKind.Utc)
			: DateTime.UtcNow.Date.AddDays(-12).AddHours(20);
		var due = published.AddDays(duration).AddHours(2);
		var assignmentSchedule = f.Assignment.DivisionSchedules.Single();
		assignmentSchedule.PublishedAt = published;
		assignmentSchedule.DueDate = due;
		await GetRequiredService<IRepository<AssignmentEntity, Guid>>().UpdateAsync(f.Assignment, true);
		var type = await GetRequiredService<HomeworkTypeManager>().CreateAsync(f.Student.AcademicMediumId, "Range practice");
		var homework = new HomeworkAssignmentEntity(Guid.NewGuid(), f.Enrollment.StandardId!.Value,
			f.SubjectId, "Date range homework", "Published task", type.Id, SubmissionType.OnlineText, tenantId);
		var schedule = new HomeworkDivisionSchedule(Guid.NewGuid(), homework.Id, f.Enrollment.DivisionId!.Value, published, due, tenantId);
		schedule.Publish();
		schedule.PublishedAt = published;
		homework.AddDivisionSchedule(schedule);
		await Insert(homework);
		var service = GetRequiredService<IMobileHomeworkAppService>();
		var firstDay = DateOnly.FromDateTime(published.AddHours(5.5));
		var expectedIds = new[] { homework.Id, f.Assignment.Id }.OrderBy(x => x).ToArray();
		for (var offset = -1; offset <= duration + 1; offset++)
		{
			var page = await service.GetListAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id,
				SubjectId = f.SubjectId, Date = firstDay.AddDays(offset) });
			if (offset >= 0 && offset <= duration)
				page.Items.Select(x => x.Id).OrderBy(x => x).ShouldBe(expectedIds);
			else
				page.Items.ShouldBeEmpty();
		}
		(await service.GetListAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id })).TotalCount.ShouldBe(2);
	});

	[Fact]
	public Task Today_filter_includes_published_assignments_with_a_later_due_date() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		f.Assignment.DivisionSchedules.Single().PublishedAt = DateTime.UtcNow.AddDays(-2);
		await GetRequiredService<IRepository<AssignmentEntity, Guid>>().UpdateAsync(f.Assignment, true);
		var page = await GetRequiredService<IMobileHomeworkAppService>().GetListAsync(new()
			{ StudentId = f.Student.Id, AcademicYearId = f.Year.Id, Tab = "today" });
		page.Items.Single().Id.ShouldBe(f.Assignment.Id);
	});
}
