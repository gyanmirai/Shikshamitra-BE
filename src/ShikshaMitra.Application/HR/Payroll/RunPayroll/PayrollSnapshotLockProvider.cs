using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSnapshotLockProvider(ICurrentTenant currentTenant) : ITransientDependency
{
	private static readonly ConcurrentDictionary<string, SemaphoreSlim> SnapshotRefreshLocks = new();

	public async Task<T> WithMonthLockAsync<T>(string monthYear, Func<Task<T>> action)
	{
		var lockKey = $"{currentTenant.Id?.ToString("D") ?? "host"}:{monthYear}";
		var snapshotLock = SnapshotRefreshLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
		await snapshotLock.WaitAsync();
		try
		{
			return await action();
		}
		finally
		{
			snapshotLock.Release();
		}
	}
}
