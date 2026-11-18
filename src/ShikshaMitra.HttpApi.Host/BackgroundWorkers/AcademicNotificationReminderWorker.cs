using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace ShikshaMitra.BackgroundWorkers;

public sealed class AcademicNotificationReminderWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes)
	: AcademicNotificationWorker(timer, scopes)
{
	protected override Task RunSchoolAsync(IServiceProvider services, CancellationToken cancellationToken)
	{
		var now = new DateTimeOffset(services.GetRequiredService<IClock>().Now.ToUniversalTime());
		return services.GetRequiredService<INotificationDueScheduler>().ScanAsync(now, 100, cancellationToken);
	}
}
