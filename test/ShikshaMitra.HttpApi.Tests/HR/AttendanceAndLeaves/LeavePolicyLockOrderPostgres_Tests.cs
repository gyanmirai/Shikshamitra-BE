using System;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
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
			var input = new LeavePolicyCreateRequest
			{
				PolicyTitle = $"Synthetic lock ordering {Guid.NewGuid():N}",
				Status = "Active",
				EffectiveFrom = DateTime.UtcNow.Date,
				Rules = [new() { PaidType = "Paid", LeaveCount = 0 }, new() { PaidType = "Unpaid", LeaveCount = 0 }]
			};
			// Ensure the target starts non-default even when the seeded school has no leave default.
			(await Client.PostAsJsonAsync($"/api/leave-policies?academicYearId={Factory.SeededAcademicYearId}", input)).EnsureSuccessStatusCode();
			input.PolicyTitle = $"Synthetic competing default {Guid.NewGuid():N}";
			var response = await Client.PostAsJsonAsync($"/api/leave-policies?academicYearId={Factory.SeededAcademicYearId}", input);
			response.EnsureSuccessStatusCode();
			var policy = (await response.Content.ReadFromJsonAsync<LeavePolicyResponse>(JsonOptions))!;
			using var scope = Factory.Services.CreateScope();
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
			using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
			// Explicitly await successful acquisition, not an interceptor's pre-execution
			// signal. This transaction definitely owns the tenant fence before the other starts.
			await scope.ServiceProvider.GetRequiredService<IPayrollInputRevisionStore>().LockAsync();
			factory.Barrier.Observe("HR_PayrollInputRevisions");
			var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			var allowReferenceSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			Task referenceWrite;
			using (ExecutionContext.SuppressFlow())
				referenceWrite = Task.Run(async () =>
				{
					using var otherScope = Factory.Services.CreateScope();
					using var otherTenant = otherScope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
					using var otherUnit = otherScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
					await otherScope.ServiceProvider.GetRequiredService<ILeavePolicyReferenceLock>().AcquireAsync(policy.Id, Guid.Parse(TestAuthHandler.TestTenantId));
					acquired.SetResult();
					await allowReferenceSave.Task.WaitAsync(TimeSpan.FromSeconds(15));
					var stored = await otherScope.ServiceProvider.GetRequiredService<IRepository<LeavePolicy, Guid>>().GetAsync(policy.Id);
					stored.Update(stored.PolicyName, stored.EffectiveFrom, stored.EffectiveTo, stored.Status, stored.IsDefault, "Synthetic reference writer");
					await (await otherScope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync()).SaveChangesAsync();
					await otherUnit.CompleteAsync();
				});
			try
			{
				await Task.WhenAny(factory.Barrier.Observed, acquired.Task).WaitAsync(TimeSpan.FromSeconds(15));
				// A row-first implementation acquires P here despite the other transaction
				// owning the fence, so fails deterministically before a deadlock can hang the test.
				acquired.Task.IsCompleted.ShouldBeFalse("A reference writer must wait for the owned tenant fence before acquiring a policy row.");
				await factory.Barrier.Observed.WaitAsync(TimeSpan.FromSeconds(15)); // Requested, not acquired.
				var stored = await scope.ServiceProvider.GetRequiredService<IRepository<LeavePolicy, Guid>>().GetAsync(policy.Id);
				stored.IsDefault.ShouldBeFalse();
				await scope.ServiceProvider.GetRequiredService<LeavePolicyManager>().SetDefaultAsync(stored);
				await (await scope.ServiceProvider.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync()).SaveChangesAsync();
				await unit.CompleteAsync();
				unit.Dispose();
				await acquired.Task.WaitAsync(TimeSpan.FromSeconds(15)); // Now actually acquired.
			}
			finally
			{
				unit.Dispose();
				allowReferenceSave.TrySetResult();
				await referenceWrite.WaitAsync(TimeSpan.FromSeconds(15));
			}
			// Both real transactions completed, without accepting a deadlock/concurrency rollback.
		}
	}
}