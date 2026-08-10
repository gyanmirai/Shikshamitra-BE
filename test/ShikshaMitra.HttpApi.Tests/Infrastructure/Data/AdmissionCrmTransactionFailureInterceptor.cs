using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Authorization;

namespace ShikshaMitra.Infrastructure.Data;

public sealed class AdmissionCrmTransactionFailureInterceptor : SaveChangesInterceptor
{
	private int _targetEventType = -1;

	public IDisposable FailNextTimelineSave(AdmissionCrmTimelineEventType eventType)
	{
		if (Interlocked.CompareExchange(ref _targetEventType, (int)eventType, -1) != -1)
		{
			throw new InvalidOperationException("An admission CRM transaction failure is already armed.");
		}

		return new ResetScope(this);
	}

	public override InterceptionResult<int> SavingChanges(
		DbContextEventData eventData,
		InterceptionResult<int> result)
	{
		ThrowIfTargeted(eventData.Context);
		return result;
	}

	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		ThrowIfTargeted(eventData.Context);
		return ValueTask.FromResult(result);
	}

	private void ThrowIfTargeted(DbContext? context)
	{
		var target = Volatile.Read(ref _targetEventType);
		if (target < 0 || context == null || !context.ChangeTracker.Entries<AdmissionCrmTimelineEvent>()
			.Any(entry => entry.State == EntityState.Added && (int)entry.Entity.EventType == target))
		{
			return;
		}

		if (Interlocked.CompareExchange(ref _targetEventType, -1, target) == target)
		{
			throw new AbpAuthorizationException("Injected admission CRM transaction failure.");
		}
	}

	private sealed class ResetScope(AdmissionCrmTransactionFailureInterceptor owner) : IDisposable
	{
		public void Dispose() => Interlocked.Exchange(ref owner._targetEventType, -1);
	}
}
