using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Threading;

namespace ShikshaMitra.BackgroundWorkers;

public sealed class AcademicNotificationHealthWorker : AcademicNotificationWorker
{
	public AcademicNotificationHealthWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes) : base(timer, scopes) =>
		Timer.Period = 300_000;

	protected override Task RunSchoolAsync(IServiceProvider services, CancellationToken cancellationToken) =>
		services.GetRequiredService<NotificationQueueHealthReporter>().ReportAsync(cancellationToken);
}
