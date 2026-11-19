using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class StudentCredentialEmailWorker : AsyncPeriodicBackgroundWorkerBase
{
	public StudentCredentialEmailWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes) : base(timer, scopes) => Timer.Period = 30_000;
	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext context)
	{
		var provider = context.ServiceProvider;
		var repository = provider.GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		List<(Guid Id, Guid? TenantId)> due;
		using (var read = provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false))
		using (provider.GetRequiredService<IDataFilter>().Disable<IMultiTenant>())
		{
			var now = DateTime.UtcNow;
			var expiredClaim = now.AddMinutes(-10);
			var query = await repository.GetQueryableAsync();
			var rows = await provider.GetRequiredService<IAsyncQueryableExecuter>().ToListAsync(query.Where(x =>
				(x.Status == "Pending" && x.NextAttemptAt <= now) || (x.Status == "Processing" && x.ClaimedAt <= expiredClaim))
				.OrderBy(x => x.NextAttemptAt).Select(x => new { x.Id, x.TenantId }).Take(50));
			due = rows.Select(row => (row.Id, row.TenantId)).ToList();
			await read.CompleteAsync();
		}
		foreach (var row in due)
		{
			if (!row.TenantId.HasValue) continue;
			try
			{
				using var tenant = provider.GetRequiredService<ICurrentTenant>().Change(row.TenantId);
				await provider.GetRequiredService<StudentCredentialEmailDispatcher>().DispatchAsync(row.Id);
			}
			catch (Exception)
			{
				provider.GetRequiredService<ILogger<StudentCredentialEmailWorker>>().LogWarning("Student credential email dispatch {RequestId} could not complete; its durable state will be reviewed on the next run.", row.Id);
			}
		}
	}
}
