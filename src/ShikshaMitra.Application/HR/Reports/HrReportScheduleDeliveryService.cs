using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Reports;

public class HrReportScheduleDeliveryService(
	IIdentityUserRepository userRepository,
	IEmailSender emailSender,
	IConfiguration configuration,
	ILogger<HrReportScheduleDeliveryService> logger) : ITransientDependency
{
	public async Task SendDeliveryEmailsAsync(HrReportSchedule schedule, Guid jobId, IReadOnlyCollection<Guid> recipientIds)
	{
		var reportLink = BuildReportLink(jobId);
		foreach (var recipientId in recipientIds.Where(x => x != Guid.Empty).Distinct())
		{
			try
			{
				var user = await userRepository.FindAsync(recipientId);
				if (user == null || string.IsNullOrWhiteSpace(user.Email))
				{
					continue;
				}

				await emailSender.SendAsync(
					user.Email,
					$"Scheduled HR report ready: {schedule.Name}",
					BuildEmailBody(schedule.Name, reportLink),
					isBodyHtml: true);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Failed to send scheduled HR report email for schedule {ScheduleId} to user {UserId}.", schedule.Id, recipientId);
			}
		}
	}

	private string BuildReportLink(Guid jobId)
	{
		var clientUrl = configuration["App:ClientUrl"]?.TrimEnd('/');
		return string.IsNullOrWhiteSpace(clientUrl)
			? $"/hr/reports?exportJobId={jobId}"
			: $"{clientUrl}/hr/reports?exportJobId={jobId}";
	}

	private static string BuildEmailBody(string scheduleName, string reportLink)
	{
		var encodedName = HtmlEncoder.Default.Encode(scheduleName);
		var encodedLink = HtmlEncoder.Default.Encode(reportLink);
		return $@"
<p>Your scheduled HR report <strong>{encodedName}</strong> has been queued.</p>
<p>Use the secure report history link below to download it when generation is complete.</p>
<p><a href=""{encodedLink}"">Open HR Reports</a></p>";
	}
}
