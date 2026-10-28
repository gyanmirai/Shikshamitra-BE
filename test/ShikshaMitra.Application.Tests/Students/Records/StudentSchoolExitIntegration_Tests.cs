using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task School_exit_pending_book_forces_independent_approval_under_direct_policy() => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct";
		var fixture = await TransitionFixtureAsync(policy);
		var book = new BookIssue(Guid.NewGuid(), Guid.NewGuid(), "EXIT-BOOK", fixture.Student.Id, 0, DateTime.UtcNow, DateTime.UtcNow.AddDays(7), TestTenantId);
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(book, true));
		var preview = (await fixture.Service.PreviewAsync(fixture.Input)).Items.Single();
		preview.Ready.ShouldBeTrue(); preview.ApprovalRequired.ShouldBeTrue(); preview.OutstandingItems.Single().Code.ShouldBe("Students:Records:OutstandingBooks");
		var result = (await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single();
		result.Status.ShouldBe("Submitted"); result.ApprovalStageCount.ShouldBe(1); result.OutstandingOverrideRequired.ShouldBeTrue();
		await Should.ThrowAsync<Volo.Abp.Authorization.AbpAuthorizationException>(() => fixture.Service.ApproveAsync(result.Id, new() { ConcurrencyStamp = result.ConcurrencyStamp, OutstandingReason = "Family will return next week" }));
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id))).IsActive.ShouldBeTrue();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(book.Id))).Status.ShouldBe(BookIssueStatus.Issued);
		var approver = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IdentityUserManager>();
			var user = new IdentityUser(approver, "exit-independent", "exit-independent@example.test", TestTenantId);
			(await users.CreateAsync(user)).Succeeded.ShouldBeTrue(); (await users.AddToRoleAsync(user, "admin")).Succeeded.ShouldBeTrue();
		});
		using (_principalAccessor.Change(EligibilityPrincipal(approver, "exit-independent@example.test", "admin")))
		{
			var missing = await Should.ThrowAsync<BusinessException>(() => fixture.Service.ApproveAsync(result.Id, new() { ConcurrencyStamp = result.ConcurrencyStamp }));
			missing.Code.ShouldBe("Students:Records:OutstandingReasonRequired");
			var applied = await fixture.Service.ApproveAsync(result.Id, new() { ConcurrencyStamp = result.ConcurrencyStamp, OutstandingReason = "Authorized recovery next week; book remains outstanding" });
			applied.Status.ShouldBe("Applied");
			applied.History.ShouldContain(x => x.Action == "OutstandingApproved" && x.ActorId == approver && x.Remarks.Contains("remains outstanding"));
		}
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(book.Id))).Status.ShouldBe(BookIssueStatus.Issued);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id))).IsActive.ShouldBeFalse();
	});
	[Fact]
	public Task School_exit_summary_reports_missing_finance_without_asserting_zero_and_keeps_scope() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await TransitionFixtureAsync();
		var summary = await fixture.Service.GetSchoolExitSummaryAsync(fixture.Student.Id, fixture.Input.AcademicYearId);
		summary.StudentId.ShouldBe(fixture.Student.Id); summary.Fees.Status.ShouldBeOneOf("Unavailable", "Restricted"); summary.Fees.OutstandingAmount.ShouldBeNull();
		summary.Library.PendingCount.ShouldBe(0); summary.Documents.Detail.ShouldNotBeNullOrWhiteSpace();
		using (_currentTenant.Change(Guid.NewGuid())) await Should.ThrowAsync<Exception>(() => fixture.Service.GetSchoolExitSummaryAsync(fixture.Student.Id, fixture.Input.AcademicYearId));
	});
}
