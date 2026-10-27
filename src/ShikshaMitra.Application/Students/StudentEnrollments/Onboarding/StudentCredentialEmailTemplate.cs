using System;
using System.Text.Encodings.Web;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Email-compatible presentation only; account preparation and delivery stay in the dispatcher.</summary>
internal static class StudentCredentialEmailTemplate
{
	private const string GooglePlayBadge = "https://play.google.com/intl/en_us/badges/static/images/badges/en_badge_web_generic.png";

	public static string Build(StudentCredentialEmailEligibility state, IdentityUser user, string? password,
		string? resetUrl, bool initialSetupPending)
	{
		var school = string.IsNullOrWhiteSpace(state.SchoolName) ? "Your school" : state.SchoolName;
		var greeting = string.IsNullOrWhiteSpace(user.Name) ? "Hello," : "Hello " + user.Name + ",";
		var passwordRow = password == null ? "" : DetailRow("Temporary password", password, secret: true);
		var accountHelp = password != null
			? "Change your temporary password when you first sign in."
			: initialSetupPending
				? "Your account needs email verification and password setup. Use the secure setup link before signing in."
				: "Use your existing password. Linking another child does not change your account credentials.";
		// Recovery-only messages must remain actionable when no temporary password can be resent.
		// The normal credential email has the approved store badge instead of a verification CTA.
		var recovery = password == null && Uri.TryCreate(resetUrl, UriKind.Absolute, out var uri) &&
			(uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
			? $"<p style=\"margin:12px 0 0;font-size:14px;line-height:22px;\"><a href=\"{Encode(resetUrl)}\" style=\"color:#007867;text-decoration:underline;\">{(initialSetupPending ? "Open secure setup link" : "Reset your password")}</a></p>"
			: "";

		// Colors follow the project's primary dark, info lighter and grey palette.
		// Inline styles provide the baseline; media rules improve narrow email clients.
		return $$"""
		<!doctype html>
		<html lang="en">
		<head>
		<meta charset="utf-8">
		<meta name="viewport" content="width=device-width, initial-scale=1">
		<title>Your school account details</title>
		<style>
		@media only screen and (max-width:480px) {
		  .email-body { padding:24px 16px !important; }
		  .email-header { padding:24px 16px !important; }
		  .email-title { font-size:24px !important; line-height:32px !important; }
		  .details-label,.details-value { display:block !important; width:auto !important; }
		  .details-label { padding-bottom:4px !important; border-bottom:0 !important; }
		  .details-value { padding-top:0 !important; }
		}
		</style>
		</head>
		<body style="margin:0;padding:0;background-color:#F4F6F8;color:#1C252E;font-family:Arial,Helvetica,sans-serif;-webkit-text-size-adjust:100%;">
		<div style="display:none;max-height:0;overflow:hidden;opacity:0;mso-hide:all;">Your school account details. Please keep your login information private.</div>
		<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#F4F6F8;">
		<tr><td align="center" style="padding:28px 12px;">
		<table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:600px;background-color:#FFFFFF;border:1px solid #DFE3E8;border-radius:12px;overflow:hidden;">
		<tr><td class="email-header" style="padding:28px 32px;background-color:#CAFDF5;background-image:linear-gradient(110deg,#CAFDF5,#EBF7FF);border-radius:12px 12px 0 0;">
		<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="table-layout:fixed;">
		<tr><td width="54" style="width:54px;vertical-align:middle;font-size:32px;line-height:40px;"><span aria-hidden="true">&#127979;</span></td>
		<td style="vertical-align:middle;overflow-wrap:anywhere;word-break:break-word;">
		<p style="margin:0 0 8px;font-size:25px;font-weight:700;line-height:32px;color:#004B50;">{{Encode(school)}}</p>
		<span style="display:inline-block;padding:5px 10px;border-radius:12px;background-color:#E8F4FF;color:#006C9C;font-size:12px;line-height:18px;">School App Access</span>
		</td></tr></table>
		</td></tr>
		<tr><td class="email-body" style="padding:28px 32px;">
		<h1 class="email-title" style="margin:0 0 20px;font-size:28px;line-height:36px;font-weight:600;color:#1C252E;">Your school account is ready</h1>
		<p style="margin:0 0 12px;font-size:16px;line-height:24px;overflow-wrap:anywhere;word-break:break-word;">{{Encode(greeting)}}</p>
		<p style="margin:0 0 20px;font-size:15px;line-height:24px;color:#637381;overflow-wrap:anywhere;word-break:break-word;">Your school app account has access to {{Encode(state.StudentName)}}. Please keep your login details private.</p>
		<table aria-label="Account details" width="100%" cellpadding="0" cellspacing="0" border="0" style="width:100%;table-layout:fixed;border-collapse:separate;background-color:#F1FAF9;border-radius:10px;">
		<tbody>
		{{DetailRow("School code", state.SchoolName)}}
		{{DetailRow("Username", user.UserName)}}
		{{passwordRow}}
		{{DetailRow("Student", state.StudentName)}}
		</tbody></table>
		<p style="margin:16px 0 0;font-size:13px;line-height:21px;color:#637381;">{{Encode(accountHelp)}}</p>
		{{recovery}}
		<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
		<tr><td align="center" style="padding:18px 0;">
		<img src="{{GooglePlayBadge}}" alt="Get it on Google Play" width="190" height="74" style="display:inline-block;width:190px;max-width:100%;height:auto;border:0;">
		</td></tr></table>
		<p style="margin:0;padding-top:16px;border-top:1px solid #DFE3E8;text-align:center;font-size:12px;line-height:20px;color:#637381;">Need help? Contact your school office.</p>
		</td></tr></table>
		<p style="margin:12px 0 0;text-align:center;font-size:11px;line-height:18px;color:#637381;">Powered by ShikshaMitra</p>
		</td></tr></table>
		</body></html>
		""";
	}

	private static string DetailRow(string label, string? value, bool secret = false)
	{
		var content = Encode(value);
		if (secret)
			content = "<span style=\"font-family:Consolas,'Courier New',monospace;letter-spacing:0.3px;overflow-wrap:anywhere;word-break:break-all;\">" + content + "</span>";
		return $"<tr><th class=\"details-label\" scope=\"row\" width=\"38%\" style=\"width:38%;padding:13px 16px;text-align:left;vertical-align:top;font-size:14px;line-height:22px;font-weight:400;color:#637381;border-bottom:1px solid #DFE3E8;overflow-wrap:anywhere;word-break:break-word;\">{Encode(label)}</th>" +
			$"<td class=\"details-value\" style=\"padding:13px 16px;vertical-align:top;font-size:14px;line-height:22px;color:#1C252E;border-bottom:1px solid #DFE3E8;overflow-wrap:anywhere;word-break:break-all;\">{content}</td></tr>";
	}

	private static string Encode(string? value) => HtmlEncoder.Default.Encode(value ?? string.Empty);
}
