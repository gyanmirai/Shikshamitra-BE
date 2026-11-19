using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.HomeworkAssignment;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Academic.Reports;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Homework_counts_drop_after_first_submission_but_not_draft_or_edit(bool parent) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, parent); using var identity = SignIn(f.User.Id);
		var service = GetRequiredService<IMobileHomeworkAppService>();
		var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, TextAnswer = "Answer" };
		var initial = await service.GetCountsAsync(input);
		initial.PendingHomework.ShouldBe(1);
		initial.StudentId.ShouldBe(f.Student.Id); initial.AcademicYearId.ShouldBe(f.Year.Id);
		initial.CalculatedAt.Offset.ShouldBe(TimeSpan.Zero);
		await service.SaveAsync(f.Assignment.Id, input, true, false);
		(await service.GetCountsAsync(input)).PendingHomework.ShouldBe(1);
		(await service.SaveAsync(f.Assignment.Id, input, false, false)).Status.ShouldBe("submittedEditable");
		(await service.GetCountsAsync(input)).PendingHomework.ShouldBe(0);
		(await service.GetListAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, Tab = "pending" })).Items.ShouldNotBeEmpty();
		input.TextAnswer = "Edited answer";
		await service.SaveAsync(f.Assignment.Id, input, false, true);
		(await service.GetCountsAsync(input)).PendingHomework.ShouldBe(0);
		(await GetRequiredService<IMobileDashboardAppService>().GetAsync(input)).PendingHomework.ShouldBe(0);
	});

	[Theory]
	[InlineData(SubmissionStatus.NotSubmitted, false, false, 1)]
	[InlineData(SubmissionStatus.Draft, false, false, 1)]
	[InlineData(SubmissionStatus.NeedsReview, false, false, 1)]
	[InlineData(SubmissionStatus.Submitted, true, false, 0)]
	[InlineData(SubmissionStatus.Late, true, false, 0)]
	[InlineData(SubmissionStatus.Reviewed, false, true, 0)]
	[InlineData(SubmissionStatus.Excused, false, false, 0)]
	public Task Homework_counts_match_first_submission_eligibility(SubmissionStatus status, bool submitted, bool reviewed, int expected) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		var response = new StudentSubmissionEntity(Guid.NewGuid(), f.Assignment.Id, f.Student.Id, f.Enrollment.DivisionId!.Value,
			DateTime.UtcNow.AddDays(3), tenantId, true) { Status = status };
		if (submitted) response.SubmissionDate = DateTimeOffset.UtcNow;
		if (reviewed) response.ReviewedAt = DateTime.UtcNow;
		await Insert(response);
		var count = await GetRequiredService<IMobileHomeworkAppService>().GetCountsAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id });
		count.PendingHomework.ShouldBe(expected);
	});

	[Fact]
	public Task Homework_counts_deny_unlinked_student_wrong_year_and_revoked_link() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, true); using var identity = SignIn(f.User.Id);
		var service = GetRequiredService<IMobileHomeworkAppService>();
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetCountsAsync(new() { StudentId = Guid.NewGuid(), AcademicYearId = f.Year.Id }));
		await Should.ThrowAsync<Volo.Abp.BusinessException>(() => service.GetCountsAsync(new() { StudentId = f.Student.Id, AcademicYearId = Guid.NewGuid() }));
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetCountsAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id }));
		await GetRequiredService<IRepository<ShikshaMitra.Students.Portal.StudentPortalAccountLink, Guid>>().DeleteAsync(f.Link, true);
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetCountsAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id }));
	});

	[Theory]
	[InlineData("closed", 0)]
	[InlineData("unsupported", 0)]
	[InlineData("unpublished", 0)]
	[InlineData("future", 0)]
	[InlineData("overdue", 1)]
	public Task Homework_counts_respect_publication_closed_types_and_late_first_submissions(string scenario, int expected) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		if (scenario == "closed") {
			f.Assignment.ChangeStatus(AssignmentStatus.Published);
			f.Assignment.ChangeStatus(AssignmentStatus.Closed);
		}
		if (scenario == "unsupported") f.Assignment.SubmissionType = (SubmissionType)99;
		var schedule = f.Assignment.DivisionSchedules.Single();
		if (scenario == "unpublished") schedule.IsPublished = false;
		if (scenario == "future") schedule.PublishedAt = DateTime.UtcNow.AddDays(1);
		if (scenario == "overdue") schedule.DueDate = DateTime.UtcNow.AddDays(-1);
		await GetRequiredService<IRepository<AssignmentEntity, Guid>>().UpdateAsync(f.Assignment, true);
		(await GetRequiredService<IMobileHomeworkAppService>().GetCountsAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id }))
			.PendingHomework.ShouldBe(expected);
	});

	[Fact]
	public Task Homework_counts_combine_legacy_subject_homework_and_assignments_with_scalar_queries() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		var assigned = await GetRequiredService<IRepository<ShikshaMitra.Academic.Curriculum.AssignedSubject, Guid>>().GetAsync(x => x.SubjectId == f.SubjectId);
		var type = await GetRequiredService<HomeworkTypeManager>().CreateAsync(f.Student.AcademicMediumId, "Written practice");
		var task = new HomeworkAssignmentEntity(Guid.NewGuid(), f.Enrollment.StandardId!.Value, assigned.Id,
			"Legacy subject homework", new string('x', 10000), type.Id, SubmissionType.OnlineText, tenantId);
		var schedule = new HomeworkDivisionSchedule(Guid.NewGuid(), task.Id, f.Enrollment.DivisionId!.Value,
			DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), tenantId);
		schedule.Publish(); task.AddDivisionSchedule(schedule); await Insert(task);
		var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
		var result = await GetRequiredService<IMobileHomeworkAppService>().GetCountsAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id });
		result.PendingHomework.ShouldBe(2);
		capture.Reads.Count(read => read.Sql.Contains("COUNT(", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
		capture.Reads.ShouldNotContain(read => read.Sql.Contains("\"AttachmentsJson\"", StringComparison.Ordinal) ||
			read.Sql.Contains("\"SubmittedContentHtml\"", StringComparison.Ordinal));
	});

	[Fact]
	public async Task Homework_counts_after_committed_write_use_a_fresh_unit_of_work()
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await WithUnitOfWorkAsync(() => Seed(tenantId, false));
		using var identity = SignIn(f.User.Id);
		var service = GetRequiredService<IMobileHomeworkAppService>();
		var input = new MobileSubmissionInput { StudentId = f.Student.Id, AcademicYearId = f.Year.Id, TextAnswer = "Final answer" };
		(await WithUnitOfWorkAsync(() => service.GetCountsAsync(input))).PendingHomework.ShouldBe(1);
		await WithUnitOfWorkAsync(() => service.SaveAsync(f.Assignment.Id, input, false, false));
		(await WithUnitOfWorkAsync(() => service.GetCountsAsync(input))).PendingHomework.ShouldBe(0);
	}
}
