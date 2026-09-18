using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class AdmissionWorkflowScheduleWorker : AsyncPeriodicBackgroundWorkerBase
{
	public AdmissionWorkflowScheduleWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
		: base(timer, serviceScopeFactory) => Timer.Period = 60 * 1000;

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
	{
		var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<AdmissionWorkflowScheduleWorker>>();
		var workflows = workerContext.ServiceProvider.GetRequiredService<IRepository<AdmissionWorkflowDefinition, Guid>>();
		var dataFilter = workerContext.ServiceProvider.GetRequiredService<IDataFilter>();
		var currentTenant = workerContext.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var commandService = workerContext.ServiceProvider.GetRequiredService<AdmissionWorkflowCommandService>();
		var now = DateTime.UtcNow;
		List<(Guid Id, Guid? TenantId)> due;
		using (dataFilter.Disable<IMultiTenant>())
		{
			due = (await workflows.GetListAsync(item => item.Status == AdmissionWorkflowStatus.Scheduled &&
				item.ScheduledPublishAt.HasValue && item.ScheduledPublishAt <= now))
				.Select(item => (item.Id, item.TenantId)).ToList();
		}

		foreach (var item in due)
		{
			try
			{
				using var tenantScope = currentTenant.Change(item.TenantId);
				using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
				await commandService.PublishScheduledAsync(item.Id);
				await uow.CompleteAsync();
			}
			catch (Exception exception)
			{
				logger.LogError(exception,
					"Scheduled workflow publication failed for workflow {WorkflowId}.", item.Id);
			}
		}
	}
}
