using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll;

// The public payroll lock endpoint performs issuance and finalization in its real transaction.
// No locked snapshot or finalized slip is manually inserted in these race cases.
internal sealed class EsiHistoricalPayrollRaceScenario(EsiHistoricalPostgresFactory factory) : EsiPayrollWorkflowScenario(factory)
{
	private const string Configuration = "/api/payroll/statutory-schemes/esi/configuration";
	private static readonly DateTime Month = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
	private static readonly string HistoricalMonth = Month.AddMonths(-1).ToString("yyyy-MM");

	private async Task InTenantAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await action(scope.ServiceProvider);
		await unit.CompleteAsync();
	}

	private async Task<(Guid Employee, PayrollPreviewStatusDto Run, RunPayrollItem Before, SaveEsiConfigurationDto Draft)> PrepareAsync()
	{
		factory.TestClock.Now = Month.AddDays(2);
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		// Only this test factory's disposable database is reset between the two orderings.
		await InTenantAsync(async services => {
			var tenants = services.GetRequiredService<ITenantRepository>();
			var school = await tenants.GetAsync(Tenant);
			school.SetProperty("InstituteName", "Synthetic Historical Payroll School");
			school.SetProperty("Address1", "1 Synthetic Payroll Test Road");
			await tenants.UpdateAsync(school);
			await services.GetRequiredService<IRepository<PayrollRunReview, Guid>>().DeleteAsync(x => true);
			await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().DeleteAsync(x => true);
			await services.GetRequiredService<IRepository<SalarySlip, Guid>>().DeleteAsync(x => true);
			var configurations = services.GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>();
			foreach (var configuration in await configurations.GetListAsync()) await configurations.HardDeleteAsync(configuration);
			// Exclude unrelated FullDemo/actor compensation from this payroll population.
			// Only synthetic fixture employees are changed, before creating the race employee.
			var employees = services.GetRequiredService<IEmployeeRepository>();
			foreach (var employee in await employees.GetListAsync()) {
				var inactive = await services.GetRequiredService<EmployeeManager>().UpdateEmployeeStatusAsync(employee.Id,
					ShikshaMitra.Common.EmployeeStatusType.Inactive, "Synthetic historical-race population isolation",
					DateOnly.FromDateTime(Month.AddDays(-1)));
				await employees.UpdateAsync(inactive);
			}
		});
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(Configuration);
		await RequireSuccess(await Api.PutAsJsonAsync(Configuration, new SaveEsiConfigurationDto {
			EffectiveMonth = HistoricalMonth, Enabled = true, ConcurrencyStamp = settings.Current!.ConcurrencyStamp,
			IdempotencyKey = Guid.NewGuid()
		}, JsonOptions));
		var employee = await EmployeeTestHelper.CreateEmployeeAsync(Api, "HistoricalEsiRace", dateOfJoining: Month);
		await EnrollAsync(employee.Id, "1234567890");
		await SaveCompensationAsync(employee.Id, Month, "GROSS", 20000m);
		await RequireSuccess(await ActivateAsync(employee.Id));
		await SeedAttendanceAsync(employee.Id, Month);
		var run = await CalculateAsync(Month);
		var before = await EmployeeAsync(run.Id, employee.Id);
		before.GrossSalary.ShouldBe(20000d);
		before.EmployerContributionTotal.ShouldBeGreaterThan(0);
		before.StatutoryApplicationJson.ShouldNotBeNullOrWhiteSpace();
		await ReviewAsync(run.Id, employee.Id, Month);
		settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(Configuration);
		return (employee.Id, run, before, new SaveEsiConfigurationDto {
			EffectiveMonth = HistoricalMonth, Enabled = false, RegistrationNumber = "SYNTHETIC-HISTORICAL-RACE",
			ConcurrencyStamp = settings.Current!.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid()
		});
	}

	internal async Task Real_lock_wins_and_historical_save_preserves_issued_payroll()
	{
		var prepared = await PrepareAsync();
		factory.Barrier.Arm("INSERT INTO \"HR_SalarySlips\"");
		var locking = LockAsync(prepared.Run.Id, prepared.Employee, Month);
		System.Net.Http.HttpResponseMessage rejected;
		try {
			await WaitForBarrierAsync(locking);
			factory.Barrier.Observe("INSERT INTO \"HR_PayrollInputRevisions\"");
			var saving = Api.PutAsJsonAsync(Configuration, prepared.Draft, JsonOptions);
			await factory.Barrier.Observed.WaitAsync(TimeSpan.FromSeconds(30));
			saving.IsCompleted.ShouldBeFalse("The real lock owns the tenant revision fence.");
			factory.Barrier.Release();
			await RequireSuccess(await locking.WaitAsync(TimeSpan.FromSeconds(45)));
			rejected = await saving.WaitAsync(TimeSpan.FromSeconds(45));
		} finally { factory.Barrier.Release(); }
		rejected.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await rejected.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.EsiConfigurationLocked);
		var after = await EmployeeAsync(prepared.Run.Id, prepared.Employee);
		after.IsLocked.ShouldBeTrue();
		after.NetPay.ShouldBe(prepared.Before.NetPay);
		after.TotalDeduction.ShouldBe(prepared.Before.TotalDeduction);
		after.EmployerContributionTotal.ShouldBe(prepared.Before.EmployerContributionTotal);
		after.StatutoryApplicationJson.ShouldBe(prepared.Before.StatutoryApplicationJson);
		var protectedEvidence = await EvidenceAsync(prepared.Employee);
		(await Api.PutAsJsonAsync(Configuration, prepared.Draft, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await EvidenceAsync(prepared.Employee)).ShouldBe(protectedEvidence);
	}

	internal async Task Historical_save_wins_stales_real_lock_and_retry_after_protection_is_read_only()
	{
		var prepared = await PrepareAsync();
		// Historical period validation reaches this query only after acquiring the revision fence.
		factory.Barrier.Arm("HR_SalarySlips");
		var saving = Api.PutAsJsonAsync(Configuration, prepared.Draft, JsonOptions);
		System.Net.Http.HttpResponseMessage stale;
		try {
			await WaitForBarrierAsync(saving);
			factory.Barrier.Observe("INSERT INTO \"HR_PayrollInputRevisions\"");
			var locking = LockAsync(prepared.Run.Id, prepared.Employee, Month);
			await factory.Barrier.Observed.WaitAsync(TimeSpan.FromSeconds(30));
			locking.IsCompleted.ShouldBeFalse("The historical save owns the tenant revision fence.");
			factory.Barrier.Release();
			await RequireSuccess(await saving.WaitAsync(TimeSpan.FromSeconds(45)));
			stale = await locking.WaitAsync(TimeSpan.FromSeconds(45));
		} finally { factory.Barrier.Release(); }
		stale.IsSuccessStatusCode.ShouldBeFalse();
		(await stale.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayrollPreviewStale);
		var fresh = await CalculateAsync(Month);
		var recalculated = await EmployeeAsync(fresh.Id, prepared.Employee);
		recalculated.GrossSalary.ShouldBe(prepared.Before.GrossSalary);
		recalculated.EmployerContributionTotal.ShouldBe(0);
		recalculated.NetPay.ShouldBeGreaterThan(prepared.Before.NetPay);
		await ReviewAsync(fresh.Id, prepared.Employee, Month);
		await RequireSuccess(await LockAsync(fresh.Id, prepared.Employee, Month));
		var settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(Configuration);
		var evidence = await EvidenceAsync(prepared.Employee);
		// This is the exact pre-lock payload/key, including its old concurrency token.
		await RequireSuccess(await Api.PutAsJsonAsync(Configuration, prepared.Draft, JsonOptions));
		var retried = await GetAndReadAsync<EsiConfigurationSettingsDto>(Configuration);
		retried.History.Count.ShouldBe(settings.History.Count);
		retried.Current!.Id.ShouldBe(settings.Current!.Id);
		(await EvidenceAsync(prepared.Employee)).ShouldBe(evidence);
		prepared.Draft.IdempotencyKey = Guid.NewGuid();
		var forbidden = await Api.PutAsJsonAsync(Configuration, prepared.Draft, JsonOptions);
		forbidden.StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await forbidden.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.EsiConfigurationLocked);
		(await EvidenceAsync(prepared.Employee)).ShouldBe(evidence);
	}

	private async Task<string> EvidenceAsync(Guid employee)
	{
		string? evidence = null;
		await InTenantAsync(async services => {
			var snapshot = await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>()
				.GetAsync(x => x.EmployeeId == employee && x.MonthYear == Month.ToString("yyyy-MM"));
			var slip = await services.GetRequiredService<IRepository<SalarySlip, Guid>>()
				.GetAsync(x => x.EmployeeId == employee && x.MonthYear == Month.ToString("yyyy-MM"));
			snapshot.IsLocked.ShouldBeTrue();
			slip.Status.ShouldBe(SalarySlipStatus.Finalized);
			slip.NetSalary.ShouldBe(snapshot.NetPay);
			slip.StatutoryApplicationJson.ShouldNotBeNullOrWhiteSpace();
			var documents = await services.GetRequiredService<IRepository<PayslipDocument, Guid>>().GetListAsync(x => x.EmployeeId == employee);
			documents.Count.ShouldBe(1);
			var document = documents.Single();
			document.Sha256.ShouldBe(Convert.ToHexString(SHA256.HashData(document.Pdf)));
			evidence = JsonSerializer.Serialize(new {
				Snapshot = snapshot, Slip = slip,
				Reviews = (await services.GetRequiredService<IRepository<PayrollRunReview, Guid>>().GetListAsync(x => x.EmployeeId == employee)).OrderBy(x => x.Id),
				document.Id, document.FactsJson, document.Sha256, document.IssuedAt, document.SalarySlipId, document.Revision,
				Pdf = Convert.ToBase64String(document.Pdf)
			}, JsonOptions);
		});
		return evidence!;
	}

	private async Task WaitForBarrierAsync(Task<System.Net.Http.HttpResponseMessage> request)
	{
		var reached = factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30));
		if (await Task.WhenAny(reached, request) == request) await RequireSuccess(await request);
		await reached;
	}
}
