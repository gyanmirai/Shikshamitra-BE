using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class PayrollSnapshotLockProvider(
	IRepository<PayrollMonthLock, Guid> payrollMonthLockRepository,
	IServiceScopeFactory serviceScopeFactory,
	ICurrentTenant currentTenant,
	IDataFilter<IMultiTenant> multiTenantFilter,
	IGuidGenerator guidGenerator,
	IClock clock,
	IAsyncQueryableExecuter asyncExecuter,
	IUnitOfWorkManager unitOfWorkManager) : ITransientDependency
{
	private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(15);
	private static readonly TimeSpan AcquireTimeout = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

	public async Task<T> WithMonthLockAsync<T>(string monthYear, Func<Task<T>> action)
	{
		var ownerId = guidGenerator.Create();
		var tenantId = currentTenant.Id;
		await AcquireAsync(monthYear, tenantId, ownerId);
		try
		{
			return await action();
		}
		finally
		{
			await ReleaseAsync(monthYear, tenantId, ownerId);
		}
	}

	private async Task AcquireAsync(string monthYear, Guid? tenantId, Guid ownerId)
	{
		var deadline = clock.Now.Add(AcquireTimeout);
		while (clock.Now <= deadline)
		{
			if (await TryAcquireOnceAsync(monthYear, tenantId, ownerId))
			{
				return;
			}

			await Task.Delay(RetryDelay);
		}

		throw new BusinessException(HRErrorCodes.PayrollMonthLockBusy)
			.WithData("month", monthYear);
	}

	private async Task<bool> TryAcquireOnceAsync(string monthYear, Guid? tenantId, Guid ownerId)
	{
		try
		{
			using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
			using var _ = multiTenantFilter.Disable();
			var now = clock.Now;
			var query = await payrollMonthLockRepository.GetQueryableAsync();
			var existing = await asyncExecuter.FirstOrDefaultAsync(query.Where(x =>
				x.MonthYear == monthYear &&
				x.TenantId == tenantId));

			if (existing != null)
			{
				if (existing.ExpiresAt > now)
				{
					return false;
				}

				await payrollMonthLockRepository.DeleteAsync(existing, autoSave: true);
			}

			await payrollMonthLockRepository.InsertAsync(
				new PayrollMonthLock(
					guidGenerator.Create(),
					tenantId,
					monthYear,
					ownerId,
					now.Add(LeaseDuration)),
				autoSave: true);
			await uow.CompleteAsync();
			return true;
		}
		catch
		{
			return false;
		}
	}

	private async Task ReleaseAsync(string monthYear, Guid? tenantId, Guid ownerId)
	{
		using var scope = serviceScopeFactory.CreateScope();
		var scopedUnitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var scopedMonthLockRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollMonthLock, Guid>>();
		var scopedMultiTenantFilter = scope.ServiceProvider.GetRequiredService<IDataFilter<IMultiTenant>>();
		var scopedAsyncExecuter = scope.ServiceProvider.GetRequiredService<IAsyncQueryableExecuter>();

		using var uow = scopedUnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		using var _ = scopedMultiTenantFilter.Disable();
		var query = await scopedMonthLockRepository.GetQueryableAsync();
		var existing = await scopedAsyncExecuter.FirstOrDefaultAsync(query.Where(x =>
			x.MonthYear == monthYear &&
			x.TenantId == tenantId));
		if (existing != null)
		{
			await scopedMonthLockRepository.DeleteAsync(existing, autoSave: true);
		}

		await uow.CompleteAsync();
	}
}
