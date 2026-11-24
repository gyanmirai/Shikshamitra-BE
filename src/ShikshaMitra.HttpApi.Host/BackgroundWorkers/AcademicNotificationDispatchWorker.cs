using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Threading;

namespace ShikshaMitra.BackgroundWorkers;

public sealed class AcademicNotificationDispatchWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes, IHostEnvironment environment)
	: AcademicNotificationWorker(timer, scopes)
{
	protected override Task RunSchoolAsync(IServiceProvider services, CancellationToken cancellationToken) =>
		services.GetRequiredService<NotificationTenantQueueRunner>().RunAsync(10, cancellationToken);

	protected override Task RunWhilePausedAsync(IConfiguration configuration, ILogger logger, CancellationToken cancellationToken)
	{
		var stagingPilot = NotificationQueueWorkerConfiguration.ReadStagingInAppPilot(configuration, environment.IsStaging());
		if (stagingPilot != null)
			return ProcessSchoolAsync(stagingPilot.TenantId, SchoolTurnBudget(configuration), logger, cancellationToken,
				(services, token) => services.GetRequiredService<StagingInAppNotificationPilotRunner>().RunAsync(stagingPilot, token)).AsTask();

		var pilot = NotificationQueueWorkerConfiguration.ReadTemplateTestPilot(configuration);
		return pilot == null ? Task.CompletedTask :
			ProcessSchoolAsync(pilot.TenantId, SchoolTurnBudget(configuration), logger, cancellationToken,
				(services, token) => services.GetRequiredService<NotificationTenantQueueRunner>().RunTemplateTestPilotAsync(pilot, token)).AsTask();
	}
}
