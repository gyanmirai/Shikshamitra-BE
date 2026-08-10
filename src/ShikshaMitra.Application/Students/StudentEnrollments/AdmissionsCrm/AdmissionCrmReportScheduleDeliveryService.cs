using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmReportScheduleDeliveryService(
	IIdentityUserRepository userRepository,
	IEmailSender emailSender,
	IConfiguration configuration,
	ILogger<AdmissionCrmReportScheduleDeliveryService> logger) : ITransientDependency
{
	public async Task EnsureRecipientAsync(Guid userId)
	{
		var user = await userRepository.FindAsync(userId);
		if (user is not { IsActive: true } || string.IsNullOrWhiteSpace(user.Email))
		{
			throw new BusinessException("AdmissionCrm:ScheduledReportRecipientRequiresEmail")
				.WithData("UserId", userId);
		}
	}

	public async Task<bool> SendAsync(
		Guid scheduleId,
		string scheduleName,
		Guid academicYearId,
		Guid exportId,
		Guid recipientUserId)
	{
		try
		{
			var user = await userRepository.FindAsync(recipientUserId);
			if (user is not { IsActive: true } || string.IsNullOrWhiteSpace(user.Email)) return false;
			await emailSender.SendAsync(
				user.Email,
				$"Scheduled admissions report ready: {scheduleName}",
				BuildEmailBody(scheduleName, BuildReportLink(academicYearId, exportId)),
				isBodyHtml: true);
			return true;
		}
		catch (Exception exception)
		{
			logger.LogWarning(
				exception,
				"Failed to deliver scheduled admissions report {ScheduleId} to user {UserId}.",
				scheduleId,
				recipientUserId);
			return false;
		}
	}

	private string BuildReportLink(Guid academicYearId, Guid exportId)
	{
		var relativePath = $"/student/enrollment/reports?cycle={academicYearId}&export={exportId}";
		var clientUrl = configuration["App:ClientUrl"]?.TrimEnd('/');
		return string.IsNullOrWhiteSpace(clientUrl) ? relativePath : $"{clientUrl}{relativePath}";
	}

	private static string BuildEmailBody(string scheduleName, string reportLink)
	{
		var encodedName = HtmlEncoder.Default.Encode(scheduleName);
		var encodedLink = HtmlEncoder.Default.Encode(reportLink);
		return $"""
			<p>Your scheduled admissions report <strong>{encodedName}</strong> is ready.</p>
			<p>Open Admission Reports to review and download the expiring export. Sign-in and admissions permissions are required.</p>
			<p><a href="{encodedLink}">Open Admission Reports</a></p>
			""";
	}
}
