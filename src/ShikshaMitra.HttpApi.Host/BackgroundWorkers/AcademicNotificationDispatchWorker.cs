using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Threading;

namespace ShikshaMitra.BackgroundWorkers;

public sealed class AcademicNotificationDispatchWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes)
	: AcademicNotificationWorker(timer, scopes)
{
	protected override Task RunSchoolAsync(IServiceProvider services, CancellationToken cancellationToken) =>
		services.GetRequiredService<NotificationTenantQueueRunner>().RunAsync(10, cancellationToken);
}
