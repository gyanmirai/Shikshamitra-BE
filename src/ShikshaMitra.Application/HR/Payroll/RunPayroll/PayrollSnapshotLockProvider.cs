using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
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

	public Task<T> WithMonthLockAsync<T>(string monthYear, Func<Task<T>> action) =>
		WithMonthLockAsync(monthYear, _ => action());

	public async Task<T> WithMonthLockAsync<T>(string monthYear, Func<Func<Task>, Task<T>> action)
	{
		var ownerId = guidGenerator.Create();
		var tenantId = currentTenant.Id;
		await AcquireAsync(monthYear, tenantId, ownerId);
		using var cancellation = new CancellationTokenSource();
		Exception? renewalFailure = null;
		async Task CheckLeaseAsync()
		{
			if (renewalFailure != null) throw new BusinessException(HRErrorCodes.PayrollPreviewStale, innerException: renewalFailure);
			await RenewAsync(monthYear, tenantId, ownerId);
		}
		async Task RenewLoopAsync()
		{
			try
			{
				using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
				while (await timer.WaitForNextTickAsync(cancellation.Token)) await RenewAsync(monthYear, tenantId, ownerId);
			}
			catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
			catch (Exception ex) { renewalFailure = ex; }
		}
		var renewal = RenewLoopAsync();
		try
		{
			return await action(CheckLeaseAsync);
		}
		finally
		{
			await cancellation.CancelAsync();
			await renewal;
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
		catch (Exception exception) when (IsDuplicateLease(exception))
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
			x.TenantId == tenantId && x.OwnerId == ownerId));
		if (existing != null)
		{
			await scopedMonthLockRepository.DeleteAsync(existing, autoSave: true);
		}

		await uow.CompleteAsync();
	}

	private static bool IsDuplicateLease(Exception exception)
	{
		var cause = exception.GetBaseException();
		return cause is System.Data.Common.DbException &&
			cause.GetType().GetProperty("SqlState")?.GetValue(cause) as string == "23505";
	}

	private async Task RenewAsync(string monthYear, Guid? tenantId, Guid ownerId)
	{
		using var scope = serviceScopeFactory.CreateScope();
		var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var repository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollMonthLock, Guid>>();
		var filter = scope.ServiceProvider.GetRequiredService<IDataFilter<IMultiTenant>>();
		using var uow = manager.Begin(requiresNew: true, isTransactional: true);
		using var disabled = filter.Disable();
		var lease = await repository.FindAsync(x => x.MonthYear == monthYear && x.TenantId == tenantId && x.OwnerId == ownerId);
		if (lease == null || lease.ExpiresAt <= clock.Now) throw new BusinessException(HRErrorCodes.PayrollPreviewStale);
		lease.Renew(clock.Now.Add(LeaseDuration));
		await repository.UpdateAsync(lease, autoSave: true);
		await uow.CompleteAsync();
	}
}
