using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.HR.Payroll;

public class PayrollInputRevisionStore_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Revision_query_executes_for_host_and_tenant(bool hasTenant)
	{
		using var tenant = GetRequiredService<ICurrentTenant>().Change(hasTenant ? Guid.NewGuid() : null);
		await WithUnitOfWorkAsync(async () =>
		{
			var revision = await GetRequiredService<IPayrollInputRevisionStore>().ReadAsync();
			revision.ShouldBe(Guid.Empty);
		});
	}

	[Fact]
	public async Task Revision_reads_only_current_tenant_fence()
	{
		var currentTenant = GetRequiredService<ICurrentTenant>();
		var provider = GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>();
		var store = GetRequiredService<IPayrollInputRevisionStore>();
		var tenantId = Guid.NewGuid();
		var expected = Guid.NewGuid();
		using (currentTenant.Change(tenantId))
		{
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await provider.GetDbContextAsync();
				var fence = new PayrollInputRevision(tenantId);
				db.PayrollInputRevisions.Add(fence);
				db.Entry(fence).Property(x => x.Revision).CurrentValue = expected;
				await db.SaveChangesAsync();
				(await store.ReadAsync()).ShouldBe(expected);
			});
		}
		using (currentTenant.Change(Guid.NewGuid()))
		{
			await WithUnitOfWorkAsync(async () => (await store.ReadAsync()).ShouldBe(Guid.Empty));
		}
	}
}
