using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class StudentRecordTransitionWorker : AsyncPeriodicBackgroundWorkerBase
{
	public StudentRecordTransitionWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes) : base(timer, scopes) => Timer.Period = 60 * 1000;
	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext context)
	{
		var provider = context.ServiceProvider;
		var repo = provider.GetRequiredService<IRepository<StudentRecordTransition, Guid>>();
		var uows = provider.GetRequiredService<IUnitOfWorkManager>();
		List<(Guid Id, Guid? TenantId)> due;
		using (var read = uows.Begin(requiresNew: true, isTransactional: false))
		using (provider.GetRequiredService<IDataFilter>().Disable<IMultiTenant>())
		{
			var today = DateTime.SpecifyKind(AdmissionCycleWindowEvaluator.GetInstitutionDate(provider.GetRequiredService<IClock>().Now), DateTimeKind.Utc);
			var query = await repo.GetQueryableAsync();
			var rows = await provider.GetRequiredService<IAsyncQueryableExecuter>().ToListAsync(query.Where(x => x.Status == "Approved" ||
				(x.Status == "Scheduled" && x.EffectiveDate <= today)).OrderBy(x => x.EffectiveDate).Select(x => new { x.Id, x.TenantId }).Take(100));
			due = rows.Select(x => (x.Id, x.TenantId)).ToList(); await read.CompleteAsync();
		}
		foreach (var item in due)
		{
			try
			{
				using var tenant = provider.GetRequiredService<ICurrentTenant>().Change(item.TenantId);
				await provider.GetRequiredService<StudentRecordTransitionApplication>().ApplyAsRecordedActorAsync(item.Id);
			}
			catch (Exception error) { provider.GetRequiredService<ILogger<StudentRecordTransitionWorker>>().LogError(error, "Scheduled student transition {Id} failed.", item.Id); }
		}
	}
}
