using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class AdmissionInquiryQualificationEmailWorker : AsyncPeriodicBackgroundWorkerBase
{
	public AdmissionInquiryQualificationEmailWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes)
		: base(timer, scopes) => Timer.Period = 30_000;

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext context)
	{
		var provider = context.ServiceProvider;
		var units = provider.GetRequiredService<IUnitOfWorkManager>();
		var repository = provider.GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
		// Page over durable inquiry intents, including handoffs, so older pending emails cannot be starved.
		for (var skip = 0; ; skip += 200)
		{
			AdmissionInquiry[] rows;
			using (var read = units.Begin(requiresNew: true, isTransactional: false))
			using (provider.GetRequiredService<IDataFilter>().Disable<IMultiTenant>())
			{
				var query = await repository.GetQueryableAsync();
				rows = (await provider.GetRequiredService<IAsyncQueryableExecuter>().ToListAsync(
					query.OrderBy(item => item.Id).Skip(skip).Take(200))).ToArray();
				await read.CompleteAsync();
			}
			foreach (var row in rows.Where(item => AdmissionInquiryQualificationEmailDispatcher.IsDue(item, DateTime.UtcNow)))
			{
				if (!row.TenantId.HasValue) continue;
				try
				{
					using var tenant = provider.GetRequiredService<ICurrentTenant>().Change(row.TenantId);
					await provider.GetRequiredService<AdmissionInquiryQualificationEmailDispatcher>().DispatchAsync(row.Id);
				}
				catch (Exception)
				{
					provider.GetRequiredService<ILogger<AdmissionInquiryQualificationEmailWorker>>()
						.LogWarning("Qualification email for inquiry {InquiryId} could not complete; its saved state will be checked again.", row.Id);
				}
			}
			if (rows.Length < 200) break;
		}
	}
}
