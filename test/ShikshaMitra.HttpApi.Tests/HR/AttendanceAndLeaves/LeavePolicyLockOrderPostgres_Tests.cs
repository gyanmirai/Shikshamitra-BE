using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
public class LeavePolicyLockOrderPostgres_Tests
{
	[PayrollPostgresFact]
	public async Task Reference_writer_and_default_writer_use_the_same_fence_then_row_order()
	{
		// Existing factory rejects non-loopback/non-disposable databases and never resets one.
		using var factory = new PayrollPostgresTestFactory();
		await new Scenario(factory).RunAsync();
	}

	private sealed class Scenario(PayrollPostgresTestFactory factory) : ShikshaMitraApiTestBase(factory)
	{
		public async Task RunAsync()
		{
			var response = await Client.PostAsJsonAsync($"/api/leave-policies?academicYearId={Factory.SeededAcademicYearId}", new LeavePolicyCreateRequest
			{
				PolicyTitle = $"Synthetic lock ordering {Guid.NewGuid():N}",
				Status = "Active",
				EffectiveFrom = DateTime.UtcNow.Date,
				Rules = [new() { PaidType = "Paid", LeaveCount = 0 }, new() { PaidType = "Unpaid", LeaveCount = 0 }]
			});
			response.EnsureSuccessStatusCode();
			var policy = (await response.Content.ReadFromJsonAsync<LeavePolicyResponse>(JsonOptions))!;
			using var scope = Factory.Services.CreateScope();
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
			await scope.ServiceProvider.GetRequiredService<ILeavePolicyReferenceLock>().AcquireAsync(policy.Id, Guid.Parse(TestAuthHandler.TestTenantId));
			// The first transaction now owns its reference lock. The second reaches the
			// real SaveChanges tenant-fence command before the first is allowed to save.
			factory.Barrier.Observe("HR_PayrollInputRevisions");
			Task<System.Net.Http.HttpResponseMessage> defaultWrite;
			using (ExecutionContext.SuppressFlow())
				defaultWrite = Task.Run(() => Client.PatchAsync($"/api/leave-policies/{policy.Id}/set-default", null));
			await factory.Barrier.Observed.WaitAsync(TimeSpan.FromSeconds(15));
			try
			{
				var stored = await scope.ServiceProvider.GetRequiredService<IRepository<LeavePolicy, Guid>>().GetAsync(policy.Id);
				stored.Update(stored.PolicyName, stored.EffectiveFrom, stored.EffectiveTo, stored.Status, stored.IsDefault, "Synthetic reference writer");
				await (await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync()).SaveChangesAsync();
				await unit.CompleteAsync();
			}
			finally { unit.Dispose(); }
			var result = await defaultWrite.WaitAsync(TimeSpan.FromSeconds(15));
			// A concurrent version rejection is safe; a PostgreSQL deadlock/server error is not.
			result.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
			(await result.Content.ReadAsStringAsync()).ShouldNotContain("40P01");
		}
	}
}