using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShikshaMitra.Students.Records;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class StudentRecordReportWorker : AsyncPeriodicBackgroundWorkerBase
{
	public StudentRecordReportWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes) : base(timer, scopes) => Timer.Period = 15 * 1000;
	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext context)
	{
		var provider = context.ServiceProvider;
		var repo = provider.GetRequiredService<IRepository<StudentRecordReportJob, Guid>>();
		var uows = provider.GetRequiredService<IUnitOfWorkManager>();
		List<(Guid Id, Guid? TenantId)> due;
		try
		{
			using (var read = uows.Begin(requiresNew: true, isTransactional: false))
			using (provider.GetRequiredService<IDataFilter>().Disable<IMultiTenant>())
			{
				var now = DateTimeOffset.UtcNow; var query = await repo.GetQueryableAsync();
				var rows = await provider.GetRequiredService<IAsyncQueryableExecuter>().ToListAsync(query.Where(x => x.ExpiresAt > now &&
					(x.Status == "Queued" || (x.Status == "Generating" && x.LeaseUntil <= now))).OrderBy(x => x.CreationTime).Select(x => new { x.Id, x.TenantId }).Take(10));
				due = rows.Select(x => (x.Id, x.TenantId)).ToList(); await read.CompleteAsync();
			}
		}
		catch (Exception error) when (IsTransientDatabaseFailure(error))
		{
			// The next timer tick gets a new scope and unit of work; never reuse the failed read.
			provider.GetRequiredService<ILogger<StudentRecordReportWorker>>().LogWarning(error,
				"Student report job polling failed temporarily. Retrying on the next worker cycle.");
			return;
		}
		foreach (var item in due)
		{
			try
			{
				using var tenant = provider.GetRequiredService<ICurrentTenant>().Change(item.TenantId);
				await provider.GetRequiredService<StudentRecordReportRunner>().RunAsync(item.Id);
			}
			catch (Exception error) { provider.GetRequiredService<ILogger<StudentRecordReportWorker>>().LogError(error, "Report job {JobId} failed.", item.Id); }
		}
	}

	private static bool IsTransientDatabaseFailure(Exception error)
	{
		for (Exception? cause = error; cause != null; cause = cause.InnerException)
		{
			if (cause is NpgsqlException { IsTransient: true }) return true;
		}
		return false;
	}
}
