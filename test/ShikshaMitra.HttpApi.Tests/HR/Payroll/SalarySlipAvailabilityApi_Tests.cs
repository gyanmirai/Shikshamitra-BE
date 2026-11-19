using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

/// <summary>Synthetic SQLite HTTP/read-boundary proof; does not run payroll or certify PostgreSQL locking.</summary>
[Collection(ApiTestCollection.Name)]
public class SalarySlipAvailabilityApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private static readonly Guid Tenant = Guid.Parse(TestAuthHandler.TestTenantId);
	private static readonly Guid Actor = Guid.Parse(TestAuthHandler.TestUserId);
	private const string Month = "2026-10";
	private const string View = "HR.PayrollManagement.GenerateSalarySlip.View";

	[Fact]
	public async Task Unprepared_and_draft_reads_explain_locking_without_creating_or_exposing_a_payslip()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "IssuedBoundary");
		await EmployeeTestHelper.SetActiveStatusForTestAsync(Factory, employee.Id);
		var query = $"employeeId={employee.Id}&monthYear={Month}";
		var availability = await Client.GetFromJsonAsync<SalarySlipAvailabilityDto>($"/api/payroll/salary-slips/availability?{query}", JsonOptions);
		availability!.Status.ShouldBe("NOT_PREPARED");
		availability.MonthYear.ShouldBe("2026-10");
		availability.CanViewPayslip.ShouldBeFalse();
		await AssertUnavailableAsync(query);
		Guid draftId = Guid.Empty; var invalidDocument = Guid.NewGuid();
		await InTenantAsync(async services =>
		{
			var slips = services.GetRequiredService<ISalarySlipRepository>();
			(await slips.GetByEmployeeAndMonthAsync(employee.Id, Month)).ShouldBeNull();
			var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), Tenant, Month, employee.Id) { NetPay = 12345.67m };
			var draft = await services.GetRequiredService<SalarySlipSnapshotWriter>().GenerateOrGetSalarySlipFromSnapshotAsync(
				snapshot, [new("Synthetic Basic", 12345.67m)], [], [], DayCalculationType.TotalMonthDays);
			draftId = draft.Id;
			await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(snapshot);
			// Defend direct document IDs as well, even if legacy storage contains inconsistent draft artifacts.
			await services.GetRequiredService<IRepository<PayslipDocument, Guid>>().InsertAsync(new(invalidDocument, Tenant,
				draftId, employee.Id, Month, 1, DateTime.UtcNow, Actor, "{}", [1]));
		});
		availability = await Client.GetFromJsonAsync<SalarySlipAvailabilityDto>($"/api/payroll/salary-slips/availability?{query}", JsonOptions);
		availability!.Status.ShouldBe("PENDING");
		availability.CanViewPayslip.ShouldBeFalse();
		var safeJson = JsonSerializer.Serialize(availability);
		safeJson.ShouldNotContain("12345");
		safeJson.ShouldNotContain(draftId.ToString());
		await AssertUnavailableAsync(query);
		(await Client.GetAsync($"/api/payroll/payslip-documents/{invalidDocument}/pdf")).IsSuccessStatusCode.ShouldBeFalse();
		(await Client.GetFromJsonAsync<System.Collections.Generic.List<PayslipDocumentDto>>($"/api/payroll/payslip-documents?{query}", JsonOptions))!.ShouldBeEmpty();
		await InTenantAsync(async services =>
		{
			var service = services.GetRequiredService<ISalarySlipAppService>();
			(await Should.ThrowAsync<BusinessException>(() => service.GetAsync(draftId))).Code.ShouldBe(HRErrorCodes.PayslipNotAvailable);
			(await Should.ThrowAsync<BusinessException>(() => service.GenerateSalarySlipAsync(new() { EmployeeId = employee.Id, MonthYear = Month }))).Code.ShouldBe(HRErrorCodes.PayslipNotAvailable);
			(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(draftId, new() { Remarks = "Must remain unmodified" }))).Code.ShouldBe(HRErrorCodes.PayslipNotAvailable);
			(await services.GetRequiredService<ISalarySlipRepository>().GetAsync(draftId)).Status.ShouldBe(SalarySlipStatus.Draft);
			(await services.GetRequiredService<ISalarySlipRepository>().GetAsync(draftId)).Remarks.ShouldBeNull();
		});
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Ineligible_employee_month_does_not_advise_preparing_or_locking_payroll(bool prejoining)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var joined = prejoining ? new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc) : new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "UnavailableBoundary", dateOfJoining: joined);
		await EmployeeTestHelper.SetActiveStatusForTestAsync(Factory, employee.Id);
		if (!prejoining)
			await InTenantAsync(async services =>
			{
				var inactive = await services.GetRequiredService<EmployeeManager>().UpdateEmployeeStatusAsync(employee.Id,
					EmployeeStatusType.Inactive, "Synthetic availability lifecycle fixture", new DateOnly(2026, 9, 30));
				await services.GetRequiredService<IEmployeeRepository>().UpdateAsync(inactive, true);
			});
		var availability = await Client.GetFromJsonAsync<SalarySlipAvailabilityDto>($"/api/payroll/salary-slips/availability?employeeId={employee.Id}&monthYear={Month}", JsonOptions);
		availability!.Status.ShouldBe("UNAVAILABLE");
		availability.CanViewPayslip.ShouldBeFalse();
		availability.Message.ShouldNotContain("lock", Case.Insensitive);
		availability.Message.ShouldNotContain("prepare", Case.Insensitive);
	}

	[Theory]
	[InlineData("QUEUED", "PREPARING")]
	[InlineData("FAILED", "FAILED")]
	[InlineData("SUCCEEDED", "REVIEWED")]
	[InlineData("STALE", "PENDING")]
	public async Task Availability_tracks_preparation_review_and_staleness_without_starting_a_run(string state, string expected)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "StatusBoundary", dateOfJoining: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
		await EmployeeTestHelper.SetActiveStatusForTestAsync(Factory, employee.Id);
		var runId = Guid.NewGuid();
		await InTenantAsync(async services =>
		{
			var scoped = await services.GetRequiredService<PayrollScopedEmployeeResolver>().GetScopedPayrollEmployeesAsync(2026, 10, false);
			var fingerprint = PayrollPreviewRunAppService.ScopeFingerprint(scoped.Select(x => x.Id));
			var run = new PayrollPreviewRun(runId, Tenant, Actor, Month, null, fingerprint, scoped.Count, DateTime.UtcNow);
			if (state != "QUEUED")
			{
				run.Start(state == "STALE" ? Guid.NewGuid() : await services.GetRequiredService<IPayrollInputRevisionStore>().ReadAsync(), DateTime.UtcNow);
				if (state == "FAILED") run.Fail(HRErrorCodes.PayrollPreviewFailed, DateTime.UtcNow);
				else run.Complete("{}", DateTime.UtcNow);
			}
			await services.GetRequiredService<IRepository<PayrollPreviewRun, Guid>>().InsertAsync(run);
			if (state is "SUCCEEDED" or "STALE")
			{
				var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), Tenant, Month, employee.Id);
				snapshot.MarkReviewed(Actor, DateTime.UtcNow);
				await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(snapshot);
				await services.GetRequiredService<IRepository<PayrollRunReview, Guid>>().InsertAsync(new(Guid.NewGuid(), Tenant, Month,
					employee.Id, Actor, DateTime.UtcNow, runId));
			}
		});
		var availability = await Client.GetFromJsonAsync<SalarySlipAvailabilityDto>($"/api/payroll/salary-slips/availability?employeeId={employee.Id}&monthYear={Month}", JsonOptions);
		availability!.Status.ShouldBe(expected);
		availability.MonthYear.ShouldBe("2026-10");
		availability.CanViewPayslip.ShouldBeFalse();
		await InTenantAsync(async services => (await services.GetRequiredService<IRepository<PayrollPreviewRun, Guid>>().GetAsync(runId)).Status.ShouldBe(state == "STALE" ? "SUCCEEDED" : state));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Issued_finalized_and_paid_slips_return_identical_frozen_documents(bool paid)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "FrozenBoundary");
		var slipId = Guid.Empty; var documentId = Guid.NewGuid();
		byte[] pdf = [37, 80, 68, 70, 45, 49, 46, 55];
		await InTenantAsync(async services =>
		{
			var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), Tenant, Month, employee.Id) { NetPay = 10000m };
			var slip = await services.GetRequiredService<SalarySlipSnapshotWriter>().GenerateOrGetSalarySlipFromSnapshotAsync(
				snapshot, [new("Basic", 10000m)], [], [], DayCalculationType.TotalMonthDays);
			slipId = slip.Id;
			slip.FinalizeSlip();
			if (paid) slip.RecordPayment(DateTimeOffset.UtcNow.AddMinutes(-1), "synthetic-payment", Actor, Guid.NewGuid());
			await services.GetRequiredService<ISalarySlipRepository>().UpdateAsync(slip);
			var facts = new SalarySlipDto { Id = slipId, EmployeeId = employee.Id, EmployeeName = "Frozen Synthetic Staff",
				DocumentId = documentId, DocumentRevision = 1, MonthYear = Month, NetSalary = 10000m };
			await services.GetRequiredService<IRepository<PayslipDocument, Guid>>().InsertAsync(new(documentId, Tenant,
				slipId, employee.Id, Month, 1, DateTime.UtcNow, Actor, JsonSerializer.Serialize(facts), pdf));
		});
		var query = $"employeeId={employee.Id}&monthYear={Month}";
		var availability = await Client.GetFromJsonAsync<SalarySlipAvailabilityDto>($"/api/payroll/salary-slips/availability?{query}", JsonOptions);
		availability!.Status.ShouldBe("AVAILABLE");
		availability.MonthYear.ShouldBe("2026-10");
		var history = await Client.GetFromJsonAsync<SalarySlipAvailabilityDto>($"/api/payroll/salary-slips/availability?{query}&academicYearId={Guid.NewGuid()}", JsonOptions);
		history!.Status.ShouldBe("AVAILABLE");
		availability.CanViewPayslip.ShouldBeTrue();
		using var detailResponse = await Client.GetAsync($"/api/payroll/salary-slips?{query}");
		detailResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await detailResponse.Content.ReadAsStringAsync());
		var result = await detailResponse.Content.ReadFromJsonAsync<SalarySlipResponse>(JsonOptions);
		result!.DocumentId.ShouldBe(documentId);
		result.Status.ShouldBe(paid ? "Paid" : "Finalized");
		foreach (var path in new[] { "pdf", "pdf-preview", "preview-pdf" })
			(await Client.GetByteArrayAsync($"/api/payroll/salary-slips/{path}?{query}")).ShouldBe(pdf);
		(await Client.GetByteArrayAsync($"/api/payroll/payslip-documents/{documentId}/pdf")).ShouldBe(pdf);
	}

	[Fact]
	public async Task Availability_requires_exact_view_permission_and_rejects_foreign_staff()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Client, "AvailabilityDenied");
		var path = $"/api/payroll/salary-slips/availability?employeeId={employee.Id}&monthYear={Month}";
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "HR.PayrollManagement.RunPayroll.Lock");
		(await denied.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		denied.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, View);
		(await denied.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.OK);
		using var foreign = Factory.CreateClient();
		foreign.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		(await foreign.GetAsync(path)).IsSuccessStatusCode.ShouldBeFalse();
		(await foreign.GetAsync($"/api/payroll/salary-slips?employeeId={employee.Id}&monthYear={Month}")).IsSuccessStatusCode.ShouldBeFalse();
		using var unassigned = CreateClientAs(Guid.NewGuid());
		(await unassigned.GetAsync(path)).IsSuccessStatusCode.ShouldBeFalse();
	}

	private async Task AssertUnavailableAsync(string query)
	{
		foreach (var path in new[] { "", "/pdf", "/pdf-preview", "/preview-pdf" })
		{
			using var response = await Client.GetAsync($"/api/payroll/salary-slips{path}?{query}");
			response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
			(await response.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayslipNotAvailable);
		}
	}

	private async Task InTenantAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Tenant);
		using var principal = services.GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, Actor.ToString()), new Claim(AbpClaimTypes.TenantId, Tenant.ToString())], "SyntheticFixture")));
		using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(services);
		await unit.CompleteAsync();
	}
}
