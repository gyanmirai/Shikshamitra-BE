using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public partial class RunPayrollApi_Tests
{
	[PayrollWorkflowPostgresFact]
	public async Task CalculationFix_Direct_finalization_should_refresh_employer_evidence_and_issue_new_document()
	{
		var id = await PrepareCalculationEmployeeAsync("FinalizeMeta");
		await SeedAttendanceOnDateAsync(id, new DateOnly(2026, 6, 10));
		await GetPreviewFromQueryAsync($"{BaseUrl}?month=2026-06&forceRefresh=true&pageSize=100");
		await GetSalarySlipResponseAsync(id, "2026-06");
		var review = await Client.PostAsJsonAsync(ReviewUrl,
			new { month = "2026-06", previewRunId = LastPreviewId("2026-06"), employeeIds = new[] { id } });
		review.EnsureSuccessStatusCode();
		await MarkPayrollSnapshotReviewedAsync(id, "2026-06");
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		var principal = new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, TestAuthHandler.TestUserId),
			new Claim(AbpClaimTypes.UserName, TestAuthHandler.TestUserName),
			new Claim(AbpClaimTypes.Role, "admin"),
			new Claim(AbpClaimTypes.TenantId, TestAuthHandler.TestTenantId)
		], TestAuthHandler.SchemeName));
		using var caller = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(principal);
		var http = services.GetRequiredService<IHttpContextAccessor>();
		var prior = http.HttpContext;
		http.HttpContext = new DefaultHttpContext { User = principal };
		try
		{
			using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
			var snapshots = services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
			var snapshot = await snapshots.GetAsync(x => x.EmployeeId == id && x.MonthYear == "2026-06");
			snapshot.EmployerContributionTotal = 321m;
			snapshot.EmployerContributionsJson = JsonSerializer.Serialize(new[] { new { Name = "Employer PF", Amount = 321m } });
			await snapshots.UpdateAsync(snapshot, autoSave: true);
			var settings = services.GetRequiredService<IRepository<PayslipSettings, Guid>>();
			var school = await settings.FindAsync(Guid.Parse(TestAuthHandler.TestTenantId));
			if (school == null)
			{
				school = new PayslipSettings(Guid.Parse(TestAuthHandler.TestTenantId));
				school.Configure("Synthetic payroll school", "Synthetic local test address", false, Guid.NewGuid());
				await settings.InsertAsync(school, autoSave: true);
			}
			var slip = (await services.GetRequiredService<ISalarySlipRepository>().GetByEmployeeAndMonthAsync(id, "2026-06"))!;
			var finalized = await services.GetRequiredService<ISalarySlipAppService>().FinalizeAsync(slip.Id);
			finalized.Id.ShouldBe(slip.Id);
			finalized.Status.ShouldBe(SalarySlipStatus.Finalized);
			finalized.EmployerContributionTotal.ShouldBe(321m);
			finalized.DocumentId.ShouldNotBeNull();
			await uow.CompleteAsync();
		}
		finally { http.HttpContext = prior; }
		var pdf = await Client.GetAsync($"/api/payroll/salary-slips/pdf?employeeId={id}&monthYear=2026-06");
		pdf.EnsureSuccessStatusCode();
		(await pdf.Content.ReadAsByteArrayAsync()).Length.ShouldBeGreaterThan(1000);
	}

	[PayrollWorkflowPostgresFact]
	public async Task CalculationFix_Missing_pf_basis_should_remain_previewable_but_block_lock()
	{
		var id = await PrepareCalculationEmployeeAsync("MissingPf");
		var rule = await PayrollTestHelper.CreatePfRuleAsync(Client, "MissingPf");
		await CreateApprovedCompensationRevisionAsync(id, null, "Consolidated Pay", 10000m, null, "", 0m,
			rule.Id, PayrollComponentSemanticType.Ordinary);
		await SeedAttendanceOnDateAsync(id, new DateOnly(2026, 6, 10));
		var preview = await GetPreviewFromQueryAsync($"{BaseUrl}?month=2026-06&forceRefresh=true&pageSize=100");
		preview.Items.Single(x => x.EmployeeId == id).IssueCodes.ShouldContain(IssueCodes.PfWageBasisRequired);
		preview.Readiness.BlockingIssues.ShouldContain(IssueCodes.PfWageBasisRequired);
		var request = new { month = "2026-06", previewRunId = LastPreviewId("2026-06"), employeeIds = new[] { id } };
		(await Client.PostAsJsonAsync(ReviewUrl, request)).EnsureSuccessStatusCode();
		var rejected = await Client.PostAsJsonAsync(BaseUrl, request);
		rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await rejected.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayrollBlockingIssuesBeforeLock);
	}

	[PayrollWorkflowPostgresFact]
	public async Task CalculationFix_Paid_half_day_should_restore_full_payable_day()
	{
		var id = await PrepareCalculationEmployeeAsync("HalfPaid");
		await AssignTotalMonthEmployeeOverrideAsync(id, new DateTime(2026, 6, 1));
		var date = new DateOnly(2026, 6, 10);
		await SeedAttendanceOnDateAsync(id, date);
		await ChangeCalculationAttendanceAsync(id, EmployeeAttendanceStatusType.HalfDayLeave);
		var leavePolicy = await EnsureActiveDefaultLeavePolicyAsync(new DateTime(2026, 6, 1));
		await SeedApprovedLeaveAsync(id, leavePolicy, date.ToDateTime(TimeOnly.MinValue), LeavePaidType.Paid, 0.5m, halfDay: true);
		var preview = await GetPreviewFromQueryAsync($"{BaseUrl}?month=2026-06&forceRefresh=true&pageSize=100");
		preview.Items.Single(x => x.EmployeeId == id).PayableDays.ShouldBe(1d);
	}

	[PayrollWorkflowPostgresFact]
	public async Task CalculationFix_Zero_earned_basic_should_not_deduct_pf_from_adjustment()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var rule = await PayrollTestHelper.CreatePfRuleAsync(Client, "ZeroBasicFix");
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "ZeroBasicFix", dateOfJoining: new DateTime(2026, 5, 1));
		await AssignSalaryStructureAsync(employee.Id, "ZeroBasicFix", 10000m, 0m, pfRuleId: rule.Id);
		await ActivateCalculationFixtureAsync(employee.Id);
		await SeedAttendanceOnDateAsync(employee.Id, new DateOnly(2026, 5, 11));
		await ChangeCalculationAttendanceAsync(employee.Id, EmployeeAttendanceStatusType.FullDayLeave);
		await CreatePayrollAdjustmentAsync(employee.Id, "2026-05", 1000m);
		var preview = await GetPreviewFromQueryAsync($"{BaseUrl}?month=2026-05&forceRefresh=true&pageSize=100");
		var item = preview.Items.Single(x => x.EmployeeId == employee.Id);
		item.BasicSalary.ShouldBe(0d);
		item.TotalDeduction.ShouldBe(0d);
		item.EmployerContributionTotal.ShouldBe(0d);
		item.NetPay.ShouldBe(1000d);
	}

	[PayrollWorkflowPostgresTheory]
	[InlineData("insurance-number")]
	[InlineData("employer-contribution")]
	[InlineData("compensation-revision")]
	public async Task CalculationFix_Draft_read_should_refresh_metadata_only_changes(string change)
	{
		var id = await PrepareCalculationEmployeeAsync("DraftMeta");
		await SeedAttendanceOnDateAsync(id, new DateOnly(2026, 5, 11));
		await GetPreviewFromQueryAsync($"{BaseUrl}?month=2026-05&forceRefresh=true&pageSize=100");
		var original = await GetSalarySlipResponseAsync(id, "2026-05");
		Guid originalSlipId;
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			originalSlipId = (await scope.ServiceProvider.GetRequiredService<ISalarySlipRepository>()
				.GetByEmployeeAndMonthAsync(id, "2026-05"))!.Id;
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
			var snapshot = await repository.GetAsync(x => x.EmployeeId == id && x.MonthYear == "2026-05");
			if (change == "insurance-number") snapshot.EmployeeStateInsuranceNumber = "1234567890";
			if (change == "employer-contribution")
			{
				snapshot.EmployerContributionTotal = 325m;
				snapshot.EmployerContributionsJson = JsonSerializer.Serialize(new[] { new { Name = "Employer PF", Amount = 325m } });
			}
			if (change == "compensation-revision") snapshot.CompensationRevisionNumber = 2;
			await repository.UpdateAsync(snapshot);
			await uow.CompleteAsync();
		}
		var refreshed = await GetSalarySlipResponseAsync(id, "2026-05");
		refreshed.NetPay.ShouldBe(original.NetPay);
		if (change == "insurance-number") refreshed.EmployeeStateInsuranceNumber.ShouldBe("1234567890");
		using var readScope = Factory.Services.CreateScope();
		using var readTenant = readScope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var readUnit = readScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var persisted = (await readScope.ServiceProvider.GetRequiredService<ISalarySlipRepository>().GetByEmployeeAndMonthAsync(id, "2026-05"))!;
		persisted.Id.ShouldBe(originalSlipId);
		if (change == "employer-contribution") persisted.EmployerContributionTotal.ShouldBe(325m);
		if (change == "compensation-revision") persisted.CompensationRevisionNumber.ShouldBe(2);
	}

	private async Task<Guid> PrepareCalculationEmployeeAsync(string suffix)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, suffix + Guid.NewGuid().ToString("N")[..8], dateOfJoining: new DateTime(2026, 5, 1));
		await AssignSalaryStructureAsync(employee.Id, suffix + Guid.NewGuid().ToString("N")[..8], 10000m, 0m);
		await ActivateCalculationFixtureAsync(employee.Id);
		return employee.Id;
	}

	private async Task ActivateCalculationFixtureAsync(Guid id)
	{
		// Owned historical fixture only: onboarding defaults new staff to inactive today.
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<Employee, Guid>>();
		var employee = await repository.GetAsync(id);
		typeof(Employee).GetProperty(nameof(Employee.EmployeeStatusType))!.SetValue(employee, ShikshaMitra.Common.EmployeeStatusType.Active);
		await repository.UpdateAsync(employee);
		await uow.CompleteAsync();
	}

	private async Task ChangeCalculationAttendanceAsync(Guid id, EmployeeAttendanceStatusType status)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<EmployeeAttendance, Guid>>();
		var attendance = await repository.GetAsync(x => x.EmployeeId == id);
		attendance.StatusType = status;
		await repository.UpdateAsync(attendance);
		await uow.CompleteAsync();
	}
}

public sealed class PayrollWorkflowPostgresTheoryAttribute : TheoryAttribute
{
	public PayrollWorkflowPostgresTheoryAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_WORKFLOW_TEST_POSTGRES")))
			Skip = "Set PAYROLL_WORKFLOW_TEST_POSTGRES to a fresh disposable payroll_tests_* database.";
	}
}
