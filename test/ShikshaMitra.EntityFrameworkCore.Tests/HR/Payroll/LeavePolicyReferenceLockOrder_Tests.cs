using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.HR.Payroll;

public class LeavePolicyReferenceLockOrder_Tests
{
	[Fact]
	public async Task Policy_context_is_not_accessed_until_tenant_fence_has_completed()
	{
		var contexts = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
		var fence = Substitute.For<IPayrollInputRevisionStore>();
		var tenant = Substitute.For<ICurrentTenant>();
		var tenantId = Guid.NewGuid();
		tenant.Id.Returns(tenantId);
		var acquired = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
		fence.LockAsync().Returns(acquired.Task);
		contexts.GetDbContextAsync().Returns(Task.FromException<ShikshaMitraDbContext>(new InvalidOperationException("Row access reached")));
		var operation = new EfCoreLeavePolicyReferenceLock(contexts, fence, tenant).AcquireAsync(Guid.NewGuid(), tenantId);
		await fence.Received(1).LockAsync();
		await contexts.DidNotReceive().GetDbContextAsync();
		operation.IsCompleted.ShouldBeFalse();
		acquired.SetResult(Guid.Empty);
		(await Should.ThrowAsync<InvalidOperationException>(() => operation)).Message.ShouldBe("Row access reached");
		await contexts.Received(1).GetDbContextAsync();
	}

	[Fact]
	public async Task Fence_failure_cannot_fall_through_to_a_policy_row_lock()
	{
		var contexts = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
		var fence = Substitute.For<IPayrollInputRevisionStore>();
		var tenant = Substitute.For<ICurrentTenant>();
		fence.LockAsync().Returns(Task.FromException<Guid>(new InvalidOperationException("Synthetic fence failure")));
		await Should.ThrowAsync<InvalidOperationException>(() => new EfCoreLeavePolicyReferenceLock(contexts, fence, tenant).AcquireAsync(Guid.NewGuid(), null));
		await contexts.DidNotReceive().GetDbContextAsync();
	}

	[Fact]
	public async Task Cross_tenant_lock_request_is_rejected_before_either_lock()
	{
		var contexts = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
		var fence = Substitute.For<IPayrollInputRevisionStore>();
		var tenant = Substitute.For<ICurrentTenant>();
		await Should.ThrowAsync<InvalidOperationException>(() => new EfCoreLeavePolicyReferenceLock(contexts, fence, tenant).AcquireAsync(Guid.NewGuid(), Guid.NewGuid()));
		await fence.DidNotReceive().LockAsync();
		await contexts.DidNotReceive().GetDbContextAsync();
	}
}