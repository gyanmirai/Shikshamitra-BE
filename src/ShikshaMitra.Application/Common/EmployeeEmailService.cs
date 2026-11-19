using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Common;

/// <summary>
/// Email service for employee-related communications
/// </summary>
public class EmployeeEmailService : IEmployeeEmailService, ITransientDependency
{
	private readonly IEmailSender emailSender;
	private readonly ILogger<EmployeeEmailService> logger;
	private readonly IConfiguration configuration;
	private readonly ICurrentTenant currentTenant;
	private readonly ITenantStore tenantStore;

	public EmployeeEmailService(
		IEmailSender emailSender,
		ILogger<EmployeeEmailService> logger,
		IConfiguration configuration,
		ICurrentTenant currentTenant,
		ITenantStore tenantStore)
	{
		this.emailSender = emailSender;
		this.logger = logger;
		this.configuration = configuration;
		this.currentTenant = currentTenant;
		this.tenantStore = tenantStore;
	}

	/// <summary>
	/// Sends welcome email to newly created employee with login credentials.
	/// </summary>
	/// <param name="employeeEmail">Employee's email address</param>
	/// <param name="employeeName">Employee's full name</param>
	/// <param name="username">Generated username</param>
	/// <param name="password">Generated password</param>
	/// <returns>Task representing the async operation</returns>
	public async Task SendWelcomeEmailAsync(
		string? employeeEmail,
		string employeeName,
		string username,
		string password)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(employeeEmail))
			{
				return;
			}

			var schoolCode = currentTenant.Id.HasValue
				? (await tenantStore.FindAsync(currentTenant.Id.Value))?.Name
				: null;
			if (currentTenant.Id.HasValue && string.IsNullOrWhiteSpace(schoolCode))
				throw new InvalidOperationException("The employee's school code could not be resolved.");
			var loginUrl = EmployeeWelcomeEmailLink.Build(configuration["App:ClientUrl"], schoolCode);
			var htmlBody = BuildWelcomeEmailHtmlBody(employeeName, username, password, loginUrl, schoolCode);

			await emailSender.SendAsync(
				to: employeeEmail,
				subject: "Welcome to ShikshaMitra - Your Account Credentials",
				body: htmlBody,
				isBodyHtml: true);
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Failed to send welcome email to employee: {EmployeeEmail}, Name: {EmployeeName}. Error: {ErrorMessage}",
				employeeEmail,
				employeeName,
				ex.Message);
		}
	}

	/// <summary>
	/// Builds HTML body for welcome email with employee login credentials.
	/// </summary>
	private static string BuildWelcomeEmailHtmlBody(string employeeName, string username, string password, string loginUrl, string? schoolCode)
	{
		var encodedUsername = HtmlEncoder.Default.Encode(username);
		var encodedPassword = HtmlEncoder.Default.Encode(password);
		var encodedLoginUrl = HtmlEncoder.Default.Encode(loginUrl);
		var schoolDetails = string.IsNullOrWhiteSpace(schoolCode) ? string.Empty
			: $"<p><span class='label'>School code:</span> {HtmlEncoder.Default.Encode(schoolCode)}</p>";

		return $@"
		<!DOCTYPE html>
		<html lang='en'>
		<head>
			<meta charset='UTF-8'>
			<meta name='viewport' content='width=device-width, initial-scale=1.0'>
			<style>
				body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
				.container {{ max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px; }}
				.header {{ background-color: #2c3e50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
				.content {{ padding: 20px; background-color: #ecf0f1; }}
				.credentials {{ background-color: #fff; padding: 15px; margin: 15px 0; border-left: 4px solid #3498db; }}
				.credentials p {{ margin: 10px 0; }}
				.label {{ font-weight: bold; color: #2c3e50; }}
				.value {{ color: #e74c3c; font-family: 'Courier New', monospace; }}
				.warning {{ background-color: #fff3cd; border: 1px solid #ffc107; padding: 10px; margin: 15px 0; border-radius: 3px; }}
				.footer {{ background-color: #34495e; color: white; padding: 15px; text-align: center; font-size: 12px; border-radius: 0 0 5px 5px; }}
				a {{ color: #3498db; text-decoration: none; }}
			</style>
		</head>
		<body>
			<div class='container'>
				<div class='header'>
					<h1>Welcome to ShikshaMitra</h1>
				</div>
				<div class='content'>
					<p>Dear <strong>{HtmlEncoder.Default.Encode(employeeName)}</strong>,</p>
					<p>Welcome to the ShikshaMitra platform! Your employee account has been successfully created. Below are your login credentials:</p>

					<div class='credentials'>
						<p><span class='label'>Username:</span> <span class='value'>{encodedUsername}</span></p>
						<p><span class='label'>Password:</span> <span class='value'>{encodedPassword}</span></p>
						{schoolDetails}
					</div>
					<p><a href='{encodedLoginUrl}' style='display:inline-block;padding:12px 18px;background-color:#2c3e50;color:#ffffff;border-radius:4px;font-weight:bold;'>Login to ShikshaMitra</a></p>
					<p>If the button does not open, copy this address into your browser:</p>
					<p style='overflow-wrap:anywhere;word-break:break-word;'><a href='{encodedLoginUrl}'>{encodedLoginUrl}</a></p>

					<div class='warning'>
						<strong>⚠️ Important Security Notice:</strong>
						<p>Please keep your credentials secure and confidential. We recommend that you change your password on your first login. Never share your credentials with anyone.</p>
					</div>

					<p><strong>Next Steps:</strong></p>
					<ol>
						<li>Log in to ShikshaMitra with your credentials</li>
						<li>Update your password immediately</li>
						<li>Contact HR if you need any assistance</li>
					</ol>

					<p>If you have any questions or encounter any issues, please reach out to the HR department.</p>

					<p>Best regards,<br/>
					<strong>ShikshaMitra Administration Team</strong></p>
				</div>
				<div class='footer'>
					<p>&copy; 2026 ShikshaMitra. All rights reserved.</p>
					<p>This is an automated email. Please do not reply to this message.</p>
				</div>
			</div>
		</body>
		</html>";
	}
}
