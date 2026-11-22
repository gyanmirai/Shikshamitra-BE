using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;
using Permission = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll;

namespace ShikshaMitra.HR.Payroll;

[Trait("Database", "PostgreSQL")]
public class PayrollSectionReviewPostgres_Tests(PayrollSectionReviewPostgresTestFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<PayrollSectionReviewPostgresTestFactory>
{
	private const string Root = "/api/payroll/run";
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid HrUserId = Guid.Parse(TestAuthHandler.TestUserId);

	[PayrollSectionReviewPostgresFact]
	public async Task Section_review_checks_role_and_scope_and_HR_override_issues_and_audits_atomically()
	{
		var academicYearId = Factory.SeededAcademicYearId;
		var stageId = Factory.SeededMediumStageId;
		var roleId = Guid.NewGuid();
		var reviewerId = Guid.NewGuid();
		var otherSectionReviewerId = Guid.NewGuid();
		var noRoleUserId = Guid.NewGuid();
		var secondStageId = Guid.Empty;
		var month = string.Empty;
		List<Guid> employees = [];
		await WithDatabaseAsync(async (services, db) =>
		{
			var year = await db.Set<AcademicYear>().SingleAsync(x => x.Id == academicYearId);
			month = year.EndDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
			var lastDay = DateOnly.FromDateTime(year.EndDate);
			employees = await db.Set<Employee>()
				.Where(x => x.TenantId == TenantId && x.DateOfJoining <= lastDay && x.EmployeeStatusType == EmployeeStatusType.Active)
				.OrderBy(x => x.Id).Select(x => x.Id).Take(2).ToListAsync();
			employees.Count.ShouldBe(2);
			var secondStage = await services.GetRequiredService<EducationMediumStageManager>()
				.CreateAsync(Factory.SeededMediumId, "Synthetic Payroll Review Section");
			secondStageId = secondStage.Id;
			db.Add(secondStage);
			db.Add(new IdentityRole(roleId, "Synthetic Payroll Section Reviewer", TenantId));
			AddReviewer(reviewerId, stageId, member: true);
			AddReviewer(otherSectionReviewerId, secondStageId, member: true);
			AddReviewer(noRoleUserId, stageId, member: false);
			if (!await db.Set<EmployeeOrganizationAssignment>().AnyAsync(x => x.EmployeeId == employees[0] && x.EducationMediumStageId == stageId))
				db.Add(new EmployeeOrganizationAssignment(Guid.NewGuid(), TenantId, employees[0], stageId));
			// The second employee needs exactly one configured route for this test.
			var oldRoute = await db.Set<EmployeeOrganizationAssignment>()
				.SingleOrDefaultAsync(x => x.EmployeeId == employees[1] && x.EducationMediumStageId == stageId);
			if (oldRoute != null) db.Remove(oldRoute);
			db.Add(new EmployeeOrganizationAssignment(Guid.NewGuid(), TenantId, employees[1], secondStageId));
			var payslipSettings = await db.Set<PayslipSettings>().SingleAsync(x => x.Id == TenantId);
			payslipSettings.Configure("Test school for section review", "Synthetic testing address", true, Guid.NewGuid());

			void AddReviewer(Guid userId, Guid scopedStageId, bool member)
			{
				var account = new IdentityUser(userId, $"payroll-section-{userId:N}", $"payroll-section-{userId:N}@example.invalid", TenantId);
				if (member) account.AddRole(roleId);
				db.Add(account);
				db.Add(new UserScopeAssignment(Guid.NewGuid(), TenantId, userId: userId, educationMediumStageId: scopedStageId));
			}
		});

		using var configure = await Client.PutAsJsonAsync($"{Root}/approval-settings/sections", new ConfigurePayrollSectionsCommand
		{
			Reason = "Enable synthetic section workflow",
			SectionRoutes = [new(stageId, roleId), new(secondStageId, roleId)]
		}, JsonOptions);
		configure.StatusCode.ShouldBe(HttpStatusCode.OK, await configure.Content.ReadAsStringAsync());
		var settings = await configure.Content.ReadFromJsonAsync<PayrollApprovalSettingsDto>(JsonOptions);
		settings!.Mode.ShouldBe(PayrollApprovalSettings.SectionReview);
		var run = await PostAsync<PayrollPreviewStatusDto>(Client, $"{Root}/previews", new StartPayrollPreviewRequest
		{
			Month = month, AcademicYearId = academicYearId, CommandKey = Guid.NewGuid()
		});
		await SeedCompletedPreviewAsync(run.Id, month, employees);
		await PostAsync<PayrollLockResponse>(Client, $"{Root}/reviews", new PayrollRunReviewRequest
		{
			Month = month, AcademicYearId = academicYearId, PreviewRunId = run.Id, EmployeeIds = employees
		});
		var batches = await PostAsync<List<PayrollSubmissionDto>>(Client, $"{Root}/section-reviews", new SubmitPayrollCommand
		{
			Month = month, AcademicYearId = academicYearId, PreviewRunId = run.Id, EmployeeIds = employees,
			ExpectedSettingsVersion = settings.Version, CommandKey = Guid.NewGuid()
		});
		batches.Count.ShouldBe(2);
		var firstBatch = batches.Single(x => x.StageId == stageId);
		var secondBatch = batches.Single(x => x.StageId == secondStageId);
		firstBatch.ReviewerRoleId.ShouldBe(roleId);
		secondBatch.ReviewerRoleId.ShouldBe(roleId);
		var register = await GetAndReadAsync<RunPayrollResponse>($"{Root}/register?month={month}&academicYearId={academicYearId}");
		register.Items.Where(row => employees.Contains(row.EmployeeId)).Count(row => row.HasPendingSectionReview && !row.IsSubmitted)
			.ShouldBe(employees.Count);
		var detailUrl = $"{Root}/submissions/{firstBatch.Id}";
		var review = new ResolvePayrollSubmissionCommand { ExpectedVersion = firstBatch.Version, CommandKey = Guid.NewGuid() };

		using var reviewer = ReviewerClient(reviewerId);
		using var wrongSection = ReviewerClient(otherSectionReviewerId);
		using var noRole = ReviewerClient(noRoleUserId);
		foreach (var denied in new[] { wrongSection, noRole })
		{
			using var detail = await denied.GetAsync(detailUrl);
			detail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			using var resolve = await denied.PostAsJsonAsync($"{detailUrl}/review", review, JsonOptions);
			resolve.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		}
		var reviewerBatch = await reviewer.GetFromJsonAsync<PayrollSubmissionDetailDto>(detailUrl, JsonOptions);
		reviewerBatch!.Items.Select(x => x.EmployeeId).ShouldBe([employees[0]]);
		reviewerBatch.Submission.AllowedActions.ShouldContain("review");
		reviewerBatch.Submission.AllowedActions.ShouldNotContain("finalize");
		using (var denied = await reviewer.PostAsJsonAsync($"{detailUrl}/finalize", review, JsonOptions))
			denied.StatusCode.ShouldBe(HttpStatusCode.Conflict, await denied.Content.ReadAsStringAsync());
		var reviewed = await PostAsync<PayrollSubmissionDto>(reviewer, $"{detailUrl}/review", review);
		reviewed.State.ShouldBe(PayrollSubmission.Reviewed);
		reviewed.ResolvedByUserId.ShouldBe(reviewerId);

		var lockRequest = new RunPayrollRequest { Month = month, AcademicYearId = academicYearId, PreviewRunId = run.Id, EmployeeIds = employees };
		using (var denied = await reviewer.PostAsJsonAsync(Root, lockRequest, JsonOptions))
			denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using (var denied = await Client.PostAsJsonAsync(Root, lockRequest, JsonOptions))
		{
			denied.StatusCode.ShouldBe(HttpStatusCode.Conflict, await denied.Content.ReadAsStringAsync());
			(await denied.Content.ReadAsStringAsync()).ShouldContain("PAYROLL_SECTION_REVIEW_OVERRIDE_REQUIRED");
		}
		await AssertNothingIssuedAsync();

		var replacement = await PostAsync<PayrollPreviewStatusDto>(Client, $"{Root}/previews", new StartPayrollPreviewRequest
		{
			Month = month, AcademicYearId = academicYearId, Refresh = true, CommandKey = Guid.NewGuid()
		});
		await WithDatabaseAsync(async (_, db) =>
		{
			var stored = await db.PayrollPreviewRuns.SingleAsync(x => x.Id == replacement.Id);
			var original = await db.PayrollPreviewRuns.SingleAsync(x => x.Id == run.Id);
			stored.Start(original.InputRevision, DateTime.UtcNow);
			var originalRows = await db.Set<PayrollPreviewEmployee>().Where(x => x.RunId == run.Id).ToListAsync();
			foreach (var row in originalRows)
				db.Add(new PayrollPreviewEmployee(Guid.NewGuid(), TenantId, replacement.Id, row.EmployeeId,
					row.EmployeeName, row.EmployeeCode, row.ItemJson));
			stored.Complete(original.SummaryJson!, DateTime.UtcNow);
		});
		await PostAsync<PayrollLockResponse>(Client, $"{Root}/reviews", new PayrollRunReviewRequest
		{
			Month = month, AcademicYearId = academicYearId, PreviewRunId = replacement.Id, EmployeeIds = employees
		});
		lockRequest.PreviewRunId = replacement.Id;
		const string reason = "HR verified the remaining section evidence";
		lockRequest.SectionReviewOverrideReason = reason;
		factory.Barrier.FailNextCommand("UPDATE \"HR_PayrollSubmissions\"");
		using (var failed = await Client.PostAsJsonAsync(Root, lockRequest, JsonOptions))
			failed.IsSuccessStatusCode.ShouldBeFalse();
		await AssertNothingIssuedAsync();

		var issued = await PostAsync<PayrollLockResponse>(Client, Root, lockRequest);
		issued.EmployeesProcessed.ShouldBe(2);
		await WithDatabaseAsync(async (_, db) =>
		{
			var closed = await db.Set<PayrollSubmission>().SingleAsync(x => x.Id == secondBatch.Id);
			closed.State.ShouldBe(PayrollSubmission.Withdrawn);
			closed.ReasonCode.ShouldBe("HrLockOverride");
			closed.ResolvedByUserId.ShouldBe(HrUserId);
			closed.ResolutionReason.ShouldBe(reason);
			var finalized = await db.Set<SalarySlip>().Where(x => x.MonthYear == month).ToListAsync();
			finalized.Select(x => x.EmployeeId).OrderBy(x => x).ShouldBe(employees.OrderBy(x => x));
			finalized.ShouldAllBe(x => x.Status == SalarySlipStatus.Finalized && x.NetSalary == 9900m);
			(await db.Set<PayrollEmployeeClaim>().CountAsync(x => x.Month == month && x.SubmissionId.HasValue)).ShouldBe(0);
			var audit = await db.Set<HrAuditEvent>().SingleAsync(x => x.EntityId == secondBatch.Id && x.Action == "hr_lock_override");
			audit.ActorUserId.ShouldBe(HrUserId);
			audit.Reason.ShouldBe(reason);
		});

		Task AssertNothingIssuedAsync() => WithDatabaseAsync(async (_, db) =>
		{
			(await db.Set<SalarySlip>().CountAsync(x => x.MonthYear == month)).ShouldBe(0);
			(await db.Set<PayslipDocument>().CountAsync(x => x.MonthYear == month)).ShouldBe(0);
			(await db.Set<PayrollSubmission>().SingleAsync(x => x.Id == secondBatch.Id)).State.ShouldBe(PayrollSubmission.Pending);
			var claim = await db.Set<PayrollEmployeeClaim>().SingleAsync(x => x.Month == month && x.EmployeeId == employees[1]);
			claim.SubmissionId.ShouldBe(secondBatch.Id);
			(await db.Set<HrAuditEvent>().AnyAsync(x => x.EntityId == secondBatch.Id && x.Action == "hr_lock_override")).ShouldBeFalse();
		});
	}

	private HttpClient ReviewerClient(Guid userId)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, userId.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Synthetic Payroll Section Reviewer");
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, $"{Permission.View},{Permission.Add}");
		return client;
	}

	private Task SeedCompletedPreviewAsync(Guid runId, string month, List<Guid> employeeIds) => WithDatabaseAsync(async (_, db) =>
	{
		var run = await db.PayrollPreviewRuns.SingleAsync(x => x.Id == runId);
		var revision = await db.Set<PayrollInputRevision>().Where(x => x.Id == TenantId).Select(x => x.Revision).SingleAsync();
		run.Start(revision, DateTime.UtcNow);
		foreach (var person in await db.Set<Employee>().Where(x => employeeIds.Contains(x.Id)).ToListAsync())
		{
			var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), TenantId, month, person.Id)
			{
				BasicSalary = 10000m, GrossSalary = 10000m, FullGrossSalary = 10000m, NetPay = 9900m, TotalDeduction = 100m,
				BasisDays = 31, PayableUnits = 31, PresentDays = 31, PayableDays = 31, TotalWorkingDays = 31, ProrationFactor = 1m,
				EmployeeName = person.EmployeeName, EmployeeIdentifier = person.EmployeeCode ?? "", DayCalculationMode = "total-month-days",
				EarningComponentsJson = JsonSerializer.Serialize(new[] { new RunPayrollComponentItem { Name = "Basic", Amount = 10000, FullMonthAmount = 10000m } }),
				DeductionComponentsJson = JsonSerializer.Serialize(new[] { new RunPayrollComponentItem { Name = "Synthetic deduction", Amount = 100 } })
			};
			db.Add(snapshot);
			var item = PayrollRunProjection.ToItem(snapshot);
			db.Add(new PayrollPreviewEmployee(Guid.NewGuid(), TenantId, runId, person.Id, item.EmployeeName, item.EmployeeIdentifier, JsonSerializer.Serialize(item)));
		}
		run.Complete(JsonSerializer.Serialize(new RunPayrollReadinessSummary { Month = month }), DateTime.UtcNow);
	});

	private static async Task<T> PostAsync<T>(HttpClient client, string url, object command)
	{
		using var response = await client.PostAsJsonAsync(url, command, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
	}

	private async Task WithDatabaseAsync(Func<IServiceProvider, ShikshaMitraDbContext, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(scope.ServiceProvider, await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync());
		await unit.CompleteAsync();
	}
}

public sealed class PayrollSectionReviewPostgresTestFactory : PayrollPostgresTestFactory
{
	internal const string ConnectionKey = "PAYROLL_SECTION_REVIEW_TEST_POSTGRES";
	protected override string ConnectionVariable => ConnectionKey;
}

public sealed class PayrollSectionReviewPostgresFactAttribute : FactAttribute
{
	public PayrollSectionReviewPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PayrollSectionReviewPostgresTestFactory.ConnectionKey)))
			Skip = "Set PAYROLL_SECTION_REVIEW_TEST_POSTGRES to a separate empty, disposable loopback payroll_tests_* database.";
	}
}
