using System;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class EsiContinuityInvalidationPostgres_Tests
{
	[PayrollPostgresFact]
	public async Task Evidence_invalidates_completed_and_inflight_previews_atomically()
	{
		using var factory = new PayrollPostgresTestFactory();
		var scenario = new Scenario(factory);
		await scenario.RunAsync();
	}

	private sealed class Scenario(PayrollPostgresTestFactory factory) : ShikshaMitraApiTestBase(factory)
	{
		private static readonly Guid Tenant = Guid.Parse(TestAuthHandler.TestTenantId);
		private const string Endpoint = "/api/payroll/run/previews";

		public async Task RunAsync()
		{
			var run = await StartAsync();
			await ExecuteAsync(run.Id);
			var completed = await GetAndReadAsync<PayrollPreviewStatusDto>($"{Endpoint}/{run.Id}");
			completed.Status.ShouldBe("SUCCEEDED", completed.ErrorMessage);
			var page = await GetAndReadAsync<RunPayrollResponse>($"{Endpoint}/{run.Id}/employees?pageSize=100");
			var employeeId = page.Items.First(x => !x.IsLocked).EmployeeId;
			await AddEvidenceAsync(employeeId, "2026-08", rollback: true);
			(await GetAndReadAsync<PayrollPreviewStatusDto>($"{Endpoint}/{run.Id}")).IsCurrent.ShouldBeTrue();
			await AddEvidenceAsync(employeeId, "2026-08");
			var stale = await GetAndReadAsync<PayrollPreviewStatusDto>($"{Endpoint}/{run.Id}");
			stale.Status.ShouldBe("SUPERSEDED");
			stale.IsCurrent.ShouldBeFalse();
			var stalePage = await GetAndReadAsync<RunPayrollResponse>($"{Endpoint}/{run.Id}/employees?pageSize=100");
			stalePage.IsCurrent.ShouldBeFalse();
			stalePage.Readiness.CanLock.ShouldBeFalse();
			foreach (var endpoint in new[] { "/api/payroll/run/reviews", "/api/payroll/run" })
			{
				var response = await Client.PostAsJsonAsync(endpoint,
					new { month = "2026-09", previewRunId = run.Id, employeeIds = new[] { employeeId } });
				response.IsSuccessStatusCode.ShouldBeFalse();
				(await response.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.PayrollPreviewStale);
			}

			var running = await StartAsync();
			factory.Barrier.Arm();
			var calculation = ExecuteAsync(running.Id);
			try
			{
				await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30));
				await AddEvidenceAsync(employeeId, "2026-07");
			}
			finally { factory.Barrier.Release(); }
			await calculation.WaitAsync(TimeSpan.FromSeconds(45));
			var rejected = await GetAndReadAsync<PayrollPreviewStatusDto>($"{Endpoint}/{running.Id}");
			rejected.Status.ShouldBe("SUPERSEDED");
			rejected.IsCurrent.ShouldBeFalse();
			var fresh = await StartAsync();
			await ExecuteAsync(fresh.Id);
			var refreshed = await GetAndReadAsync<PayrollPreviewStatusDto>($"{Endpoint}/{fresh.Id}");
			refreshed.Status.ShouldBe("SUCCEEDED", refreshed.ErrorMessage);
			refreshed.IsCurrent.ShouldBeTrue();
		}

		private async Task<PayrollPreviewStatusDto> StartAsync()
		{
			var response = await Client.PostAsJsonAsync(Endpoint, new { month = "2026-09", refresh = true });
			response.EnsureSuccessStatusCode();
			return (await response.Content.ReadFromJsonAsync<PayrollPreviewStatusDto>(JsonOptions))!;
		}

		private async Task ExecuteAsync(Guid id)
		{
			using var scope = Factory.Services.CreateScope();
			await scope.ServiceProvider.GetRequiredService<PayrollPreviewBackgroundJob>()
				.ExecuteAsync(new PayrollPreviewJobArgs { TenantId = Tenant, RunId = id });
		}

		private async Task AddEvidenceAsync(Guid employeeId, string month, bool rollback = false)
		{
			using var scope = Factory.Services.CreateScope();
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Tenant);
			using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>()
				.Begin(requiresNew: true, isTransactional: true);
			await scope.ServiceProvider.GetRequiredService<IPayrollInputRevisionStore>().LockAsync();
			var repository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
			var snapshot = await repository.FindAsync(x => x.EmployeeId == employeeId && x.MonthYear == month);
			if (snapshot == null)
			{
				snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), Tenant, month, employeeId);
				await repository.InsertAsync(snapshot);
			}
			snapshot.EmployeeStateInsuranceRuleId = Guid.NewGuid();
			snapshot.EmployeeStateInsuranceCoverageDecision = "WITHIN_WAGE_CEILING";
			snapshot.EmployeeStateInsuranceNumber = "1234567890";
			snapshot.MarkLocked();
			await repository.UpdateAsync(snapshot, autoSave: true);
			await scope.ServiceProvider.GetRequiredService<EsiContinuityInvalidator>().InvalidateAsync(month, [employeeId]);
			if (rollback) await uow.RollbackAsync();
			else await uow.CompleteAsync();
		}
	}
}
