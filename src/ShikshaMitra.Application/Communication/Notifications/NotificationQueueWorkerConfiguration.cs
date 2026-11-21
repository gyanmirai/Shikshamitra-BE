using Microsoft.Extensions.Configuration;

namespace ShikshaMitra.Communication.Notifications;

public static class NotificationQueueWorkerConfiguration
{
	public static bool IsEnabled(IConfiguration configuration) =>
		bool.TryParse(configuration["Notifications:QueueWorker:Enabled"], out var enabled) && enabled;
}
