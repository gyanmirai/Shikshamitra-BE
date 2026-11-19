using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;

namespace ShikshaMitra.HR.Employees.Onboarding;

public record EmployeeOnboardingInvitationEmailResult(bool Succeeded, string? Message);

public interface IEmployeeOnboardingInvitationEmailService
{
	Task<EmployeeOnboardingInvitationEmailResult> SendInvitationAsync(
		EmployeeApplication application,
		string publicPath);
}

public class EmployeeOnboardingInvitationEmailService(
	IEmailSender emailSender,
	IConfiguration configuration,
	ILogger<EmployeeOnboardingInvitationEmailService> logger)
	: IEmployeeOnboardingInvitationEmailService, ITransientDependency
{
	public async Task<EmployeeOnboardingInvitationEmailResult> SendInvitationAsync(
		EmployeeApplication application,
		string publicPath)
	{
		if (string.IsNullOrWhiteSpace(application.Email))
		{
			return new(false, "Invite was created, but no candidate email is available.");
		}

		var invitationUrl = BuildInvitationUrl(publicPath);

		try
		{
			await emailSender.SendAsync(
				application.Email,
				"Complete your ShikshaMitra staff onboarding",
				BuildEmailBody(application, invitationUrl),
				isBodyHtml: true);

			return new(true, "Invite email sent.");
		}
		catch (Exception ex)
		{
			logger.LogWarning(
				ex,
				"Failed to send staff onboarding invitation email for application {ApplicationId} to {Email}.",
				application.Id,
				application.Email);

			return new(false, "Invite was created, but email could not be sent.");
		}
	}

	private string BuildInvitationUrl(string publicPath)
	{
		var clientUrl = configuration["App:ClientUrl"]?.TrimEnd('/');
		if (string.IsNullOrWhiteSpace(clientUrl))
		{
			return publicPath;
		}

		return publicPath.StartsWith("/", StringComparison.Ordinal)
			? $"{clientUrl}{publicPath}"
			: $"{clientUrl}/{publicPath}";
	}

	private static string BuildEmailBody(EmployeeApplication application, string invitationUrl)
	{
		var candidateName = string.IsNullOrWhiteSpace(application.FullName)
			? "Candidate"
			: application.FullName;
		var encodedName = HtmlEncoder.Default.Encode(candidateName);
		var encodedPosition = HtmlEncoder.Default.Encode(application.PositionAppliedFor ?? "the role");
		var encodedDepartment = HtmlEncoder.Default.Encode(application.WorkAssignmentDisplay);
		var encodedUrl = HtmlEncoder.Default.Encode(invitationUrl);
		var encodedExpiry = HtmlEncoder.Default.Encode(application.TokenExpiresAt.ToString("dd MMM yyyy"));

		return $@"
<p>Dear {encodedName},</p>
<p>You have been invited to complete your staff onboarding for <strong>{encodedPosition}</strong> with work assignment <strong>{encodedDepartment}</strong>.</p>
<p>Please complete your application before <strong>{encodedExpiry}</strong>.</p>
<p><a href=""{encodedUrl}"">Open employee onboarding application</a></p>
<p>If the button does not open, copy and paste this link into your browser:</p>
<p>{encodedUrl}</p>";
	}
}
