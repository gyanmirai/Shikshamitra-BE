using System.Net.Http.Json;
using System.Text.Json;
﻿using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class EsiPayrollWorkflowPostgres_Tests
{
	[PayrollWorkflowPostgresFact]
	public async Task Earlier_finalization_later_calculation_and_lock_serialize_in_both_orders()
	{
		using var factory = new PayrollWorkflowPostgresTestFactory();
		var scenario = new Scenario(factory);
		await scenario.FinalizationWinsAsync();
		await scenario.LockWinsAsync();
		await scenario.ProfileWinsAsync();
	}

	private sealed class Scenario(PayrollWorkflowPostgresTestFactory factory) : EsiPayrollWorkflowScenario(factory)
	{
		private static readonly DateTime EarlierMonth = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		private static readonly DateTime LaterMonth = EarlierMonth.AddMonths(1);

		private async Task<Guid> PrepareAsync()
		{
			await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
			using (var scope = Factory.Services.CreateScope())
			using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Tenant))
			using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
			{
				var configurations = scope.ServiceProvider.GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>();
				foreach (var month in new[] { EarlierMonth, LaterMonth })
				{
					var existing = await configurations.FindAsync(x => x.EffectiveMonth == month && !x.IsCancelled);
					if (existing?.Enabled == true) continue;
					if (existing != null)
					{
						existing.Cancel("Replace disabled demo month for continuity scenario");
						await configurations.UpdateAsync(existing, autoSave: true);
					}
					await configurations.InsertAsync(new EsiPayrollConfiguration(Guid.NewGuid(), Tenant, month, true,
						null, null, $"historical-esic-workflow-{month:yyyy-MM}", existing?.Id));
				}
				await unit.CompleteAsync();
			}
			var employee = await EmployeeTestHelper.CreateEmployeeAsync(Api, "EsiRace", dateOfJoining: EarlierMonth);
			await EnrollAsync(employee.Id, "1234567890");
			await SaveCompensationAsync(employee.Id, EarlierMonth, "GROSS", 20000m);
			await SaveCompensationAsync(employee.Id, LaterMonth, "GROSS", 30000m);
			await RequireSuccess(await ActivateAsync(employee.Id));
			await SeedAttendanceAsync(employee.Id, EarlierMonth);
			await SeedAttendanceAsync(employee.Id, LaterMonth);
			var august = await CalculateAsync(EarlierMonth);
			(await EmployeeAsync(august.Id, employee.Id)).EmployeeStateInsuranceCoverageDecision.ShouldBe("WITHIN_WAGE_CEILING");
			await ReviewAsync(august.Id, employee.Id, EarlierMonth);
			await WithSalarySlipAsync(employee.Id, finalize: false);
			return employee.Id;
		}

		internal async Task FinalizationWinsAsync()
		{
			var employee = await PrepareAsync();
			var reviewed = await CalculateAsync(LaterMonth);
			(await EmployeeAsync(reviewed.Id, employee)).EmployeeStateInsuranceCoverageDecision.ShouldBe("ABOVE_WAGE_CEILING");
			await ReviewAsync(reviewed.Id, employee, LaterMonth);
			var running = await StartAsync(LaterMonth);
			factory.Barrier.Arm();
			var calculation = ExecuteAsync(running.Id);
			var ownsFence = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			var releaseFinalization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			Task<SalarySlipDto>? finalization = null;
			try
			{
				await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30));
				finalization = WithSalarySlipAsync(employee, true, ownsFence, releaseFinalization.Task);
				await ownsFence.Task.WaitAsync(TimeSpan.FromSeconds(30));
				factory.Barrier.Observe("HR_PayrollMonthLocks");
				var locking = LockAsync(reviewed.Id, employee, LaterMonth);
				await factory.Barrier.Observed.WaitAsync(TimeSpan.FromSeconds(30));
				releaseFinalization.TrySetResult();
				(await finalization.WaitAsync(TimeSpan.FromSeconds(30))).Status.ShouldBe(SalarySlipStatus.Finalized);
				factory.Barrier.Release();
				await calculation.WaitAsync(TimeSpan.FromSeconds(45));
				var rejected = await locking.WaitAsync(TimeSpan.FromSeconds(45));
				rejected.IsSuccessStatusCode.ShouldBeFalse();
				(await rejected.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayrollPreviewStale);
			}
			finally { releaseFinalization.TrySetResult(); factory.Barrier.Release(); }
			(await StatusAsync(running.Id)).Status.ShouldBe("SUPERSEDED");
			(await StatusAsync(reviewed.Id)).IsCurrent.ShouldBeFalse();
			var fresh = await CalculateAsync(LaterMonth);
			var row = await EmployeeAsync(fresh.Id, employee);
			row.EmployeeStateInsuranceCoverageDecision.ShouldBe("CONTRIBUTION_PERIOD_CONTINUATION");
			row.EmployerContributions.ShouldContain(x => x.Amount > 0);
			await ReviewAsync(fresh.Id, employee, LaterMonth);
			await RequireSuccess(await LockAsync(fresh.Id, employee, LaterMonth));
			(await EmployeeAsync(fresh.Id, employee)).IsLocked.ShouldBeTrue();
		}

		internal async Task LockWinsAsync()
		{
			var employee = await PrepareAsync();
			var reviewed = await CalculateAsync(LaterMonth);
			var before = await EmployeeAsync(reviewed.Id, employee);
			before.EmployeeStateInsuranceCoverageDecision.ShouldBe("ABOVE_WAGE_CEILING");
			await ReviewAsync(reviewed.Id, employee, LaterMonth);
			var profileUrl = $"/api/employees/{employee}/payroll/statutory-profile";
			var profile = (await Api.GetFromJsonAsync<EmployeeStatutoryProfileDto>(profileUrl, JsonOptions))!;
			// Stop the actual LaterMonth lock while it owns the tenant fence, before inserting its slip.
			factory.Barrier.Arm("INSERT INTO \"HR_SalarySlips\"");
			var locking = LockAsync(reviewed.Id, employee, LaterMonth);
			try
			{
				await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30));
				factory.Barrier.Observe("INSERT INTO \"HR_PayrollInputRevisions\"", count: 3);
				var finalization = WithSalarySlipAsync(employee, true);
				// Starting a preview also takes the fence; this request competes with finalization.
				var start = StartAsync(LaterMonth);
				var registrationChange = Api.PutAsJsonAsync(profileUrl, new SaveEmployeeStatutoryProfileDto
				{
					Profile = profile.Profile with { ApplyEsic = false, EsicDisabledReason = "Synthetic concurrent registration exception" },
					EffectiveMonth = LaterMonth.ToString("yyyy-MM"), ConcurrencyStamp = profile.ConcurrencyStamp,
					UpdatePf = false, IdempotencyKey = Guid.NewGuid()
				}, JsonOptions);
				await factory.Barrier.Observed.WaitAsync(TimeSpan.FromSeconds(30));
				factory.Barrier.Release();
				await RequireSuccess(await locking.WaitAsync(TimeSpan.FromSeconds(45)));
				var registrationResponse = await registrationChange.WaitAsync(TimeSpan.FromSeconds(45));
				await RequireSuccess(registrationResponse);
				(await registrationResponse.Content.ReadFromJsonAsync<EmployeeStatutoryProfileDto>(JsonOptions))!.EffectiveMonth
					.ShouldBe(LaterMonth.AddMonths(1).ToString("yyyy-MM"));
				(await finalization.WaitAsync(TimeSpan.FromSeconds(45))).Status.ShouldBe(SalarySlipStatus.Finalized);
				var running = await start.WaitAsync(TimeSpan.FromSeconds(45));
				await ExecuteAsync(running.Id);
				// A run queued before finalization may be superseded; regeneration remains safe.
				var fresh = await CalculateAsync(LaterMonth);
				var after = await EmployeeAsync(fresh.Id, employee);
				after.IsLocked.ShouldBeTrue();
				after.NetPay.ShouldBe(before.NetPay);
				after.EmployerContributionTotal.ShouldBe(before.EmployerContributionTotal);
				after.EmployeeStateInsuranceCoverageDecision.ShouldBe(before.EmployeeStateInsuranceCoverageDecision);
			}
			finally { factory.Barrier.Release(); }
		}

		internal async Task ProfileWinsAsync()
		{
			var employee = await PrepareAsync();
			var reviewed = await CalculateAsync(EarlierMonth);
			await ReviewAsync(reviewed.Id, employee, EarlierMonth);
			var profileUrl = $"/api/employees/{employee}/payroll/statutory-profile";
			var profile = (await Api.GetFromJsonAsync<EmployeeStatutoryProfileDto>(profileUrl, JsonOptions))!;
			factory.Barrier.Arm("HR_EmployeeStatutoryProfileVersions");
			var saving = Api.PutAsJsonAsync(profileUrl, new SaveEmployeeStatutoryProfileDto
			{
				Profile = profile.Profile with { ApplyEsic = false, EsicDisabledReason = "Synthetic registration exception before lock" },
				ConcurrencyStamp = profile.ConcurrencyStamp, UpdatePf = false, IdempotencyKey = Guid.NewGuid()
			}, JsonOptions);
			try
			{
				await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30));
				factory.Barrier.Observe("INSERT INTO \"HR_PayrollInputRevisions\"");
				var locking = LockAsync(reviewed.Id, employee, EarlierMonth);
				await factory.Barrier.Observed.WaitAsync(TimeSpan.FromSeconds(30));
				factory.Barrier.Release();
				await RequireSuccess(await saving.WaitAsync(TimeSpan.FromSeconds(45)));
				(await locking.WaitAsync(TimeSpan.FromSeconds(45))).IsSuccessStatusCode.ShouldBeFalse();
				var fresh = await CalculateAsync(EarlierMonth);
				var row = await EmployeeAsync(fresh.Id, employee);
				row.IssueCodes.ShouldContain("ESI_APPLY_DISABLED");
				var application = JsonSerializer.Deserialize<PayrollStatutoryApplication>(row.StatutoryApplicationJson!, JsonOptions)!;
				application.EmployeeStateInsurance.AppliedEmployeeAmount.ShouldBe(0);
				application.EmployeeStateInsurance.AppliedEmployerAmount.ShouldBe(0);
				row.TotalDeduction.ShouldBe(200); // Professional tax remains independently applied.
				row.GrossSalary.ShouldBe(20000);
				row.IsLocked.ShouldBeFalse();
			}
			finally { factory.Barrier.Release(); }
		}

		private async Task<SalarySlipDto> WithSalarySlipAsync(Guid employee, bool finalize,
			TaskCompletionSource? ownsFence = null, Task? release = null)
		{
			using var scope = Factory.Services.CreateScope();
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Tenant);
			using var principal = scope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>().Change(
				new ClaimsPrincipal(new ClaimsIdentity(new[] {
					new Claim(AbpClaimTypes.UserId, TestAuthHandler.TestUserId),
					new Claim(AbpClaimTypes.UserName, TestAuthHandler.TestUserName),
					new Claim(AbpClaimTypes.TenantId, TestAuthHandler.TestTenantId),
					new Claim(AbpClaimTypes.Role, "admin") }, "Test")));
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
			if (finalize)
			{
				await scope.ServiceProvider.GetRequiredService<IPayrollInputRevisionStore>().LockAsync();
				ownsFence?.TrySetResult();
				if (release != null) await release.WaitAsync(TimeSpan.FromSeconds(30));
			}
			var service = scope.ServiceProvider.GetRequiredService<ISalarySlipAppService>();
			var slip = await service.GetSalarySlipAsync(employee, EarlierMonth.ToString("yyyy-MM"));
			if (finalize) slip = await service.FinalizeAsync(slip.Id);
			await unit.CompleteAsync();
			return slip;
		}
	}
}
