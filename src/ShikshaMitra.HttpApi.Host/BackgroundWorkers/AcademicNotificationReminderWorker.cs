using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace ShikshaMitra.BackgroundWorkers;

public sealed class AcademicNotificationReminderWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes)
	: AcademicNotificationWorker(timer, scopes)
{
	protected override async Task RunSchoolAsync(IServiceProvider services, CancellationToken cancellationToken)
	{
		var now = new DateTimeOffset(services.GetRequiredService<IClock>().Now.ToUniversalTime());
		var scheduler = services.GetRequiredService<INotificationDueScheduler>();
		var configuration = services.GetRequiredService<IConfiguration>();
		var tenantId = services.GetRequiredService<ICurrentTenant>().Id;
		for (var page = 0; page < 4; page++)
		{
			if (NotificationQueueWorkerConfiguration.IsSchoolPaused(configuration, tenantId)) return;
			var result = await scheduler.ScanAsync(now, 100, cancellationToken);
			if (!result.HasMore) break;
		}
	}
}
