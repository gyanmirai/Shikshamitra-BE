using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students.Records;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class StudentRecordScheduleWorker : AsyncPeriodicBackgroundWorkerBase
{
	public StudentRecordScheduleWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes) : base(timer, scopes) => Timer.Period = 60 * 1000;
	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext context)
	{
		var provider = context.ServiceProvider;
		var repo = provider.GetRequiredService<IRepository<StudentRecordChange, Guid>>();
		var filter = provider.GetRequiredService<IDataFilter>();
		var tenant = provider.GetRequiredService<ICurrentTenant>();
		var uows = provider.GetRequiredService<IUnitOfWorkManager>();
		var application = provider.GetRequiredService<StudentRecordChangeApplication>();
		var logger = provider.GetRequiredService<ILogger<StudentRecordScheduleWorker>>();
		var executor = provider.GetRequiredService<Volo.Abp.Linq.IAsyncQueryableExecuter>();
		System.Collections.Generic.List<(Guid Id, Guid? TenantId)> due;
		using (var read = uows.Begin(requiresNew: true, isTransactional: false))
		using (filter.Disable<IMultiTenant>())
		{
			var query = await repo.GetQueryableAsync();
			var now = new DateTimeOffset(provider.GetRequiredService<IClock>().Now.ToUniversalTime());
			var rows = await executor.ToListAsync(query.Where(x => (x.Status == "Scheduled" && x.EffectiveAt <= now) || x.Status == "Approved")
				.OrderBy(x => x.EffectiveAt).Select(x => new { x.Id, x.TenantId }).Take(100));
			due = rows.Select(x => (x.Id, x.TenantId)).ToList();
			await read.CompleteAsync();
		}
		foreach (var item in due)
		{
			try
			{
				using var scope = tenant.Change(item.TenantId);
				await application.ApplyScheduledAsync(item.Id);
			}
			catch (Exception exception) { logger.LogError(exception, "Scheduled Records change {ChangeId} failed.", item.Id); }
		}
	}
}
