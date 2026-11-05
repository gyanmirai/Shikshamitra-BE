using System;
using System.Linq;
using System.Net;

namespace ShikshaMitra.Communication;

internal static class ParentRegistrationEmailContent
{
	private const string Introduction = "Please complete the registration form for your child and upload any documents requested by the school.";
	private const string PrivacyNote = "Use this private link before it expires. The expiry time is shown on your registration page. Please do not forward it.";
	private const string Help = "Contact the school admissions team if you need help or notice an incorrect locked detail.";
	private static readonly string[] Instructions =
	[
		"Names must match the official documents.\nUse CAPITAL LETTERS where requested. Example: MOHIT RANA",
		"Check the date of birth and contact details carefully.",
		"Complete all required fields marked with *.",
		"Upload clear, complete documents requested in the form.\nAllowed file types and sizes appear beside each upload.",
		"Review your answers and documents before sending them to school."
	];

	public static string PlainText() => Introduction + "\n\nBefore you start\n" +
		string.Join("\n\n", Instructions.Select((instruction, index) => $"{index + 1}. {instruction}")) +
		"\n\n" + PrivacyNote + "\n\n" + Help;

	// The capability URL exists only in this transient gateway body, never in the campaign.
	public static string Html(string recipientName, string? applicationReference, string link)
	{
		if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
			throw new ArgumentException("Registration requires an HTTP or HTTPS link.", nameof(link));
		var instructions = string.Join("", Instructions.Select(instruction =>
			$"<li style=\"margin:0 0 12px;padding-left:4px;\">{Encode(instruction).Replace("\n", "<br />")}</li>"));
		return $$"""
			<!doctype html>
			<html lang="en"><head><meta charset="utf-8" /><meta name="viewport" content="width=device-width, initial-scale=1" /><title>Complete your child's registration</title></head>
			<body style="margin:0;padding:16px;background:#f4f6f8;color:#212b36;font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:1.6;">
			<table role="presentation" style="width:100%;max-width:640px;margin:0 auto;background:#ffffff;border:1px solid #dfe3e8;border-radius:8px;border-spacing:0;"><tr><td style="padding:24px;">
			<p style="margin:0 0 24px;font-size:20px;font-weight:bold;">ShikshaMitra <span style="font-size:12px;color:#637381;font-weight:normal;">ADMISSIONS</span></p>
			<h1 style="margin:0 0 20px;font-size:24px;line-height:1.3;">Complete your child's registration</h1>
			<p>Dear {{Encode(string.IsNullOrWhiteSpace(recipientName) ? "Parent" : recipientName)}},</p>
			<p>{{Encode(Introduction)}}</p>
			<p style="padding:12px;background:#f4f6f8;border-radius:6px;overflow-wrap:anywhere;"><strong>Application:</strong> {{Encode(applicationReference ?? string.Empty)}}</p>
			<div style="margin:24px 0;padding:16px;border:1px solid #c4cdd5;border-radius:8px;">
			<h2 style="margin:0 0 12px;font-size:18px;">Before you start</h2>
			<ol style="margin:0;padding-left:24px;">{{instructions}}</ol>
			</div>
			<p style="margin:24px 0;text-align:center;"><a href="{{Encode(link)}}" style="display:inline-block;background:#212b36;color:#ffffff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold;">Complete Registration</a></p>
			<p style="font-size:12px;color:#637381;text-align:center;">{{Encode(PrivacyNote)}}</p>
			<hr style="border:0;border-top:1px solid #dfe3e8;margin:24px 0;" />
			<p>{{Encode(Help)}}</p>
			<p style="margin:24px 0 0;font-size:12px;color:#637381;">Sent by your school via ShikshaMitra</p>
			</td></tr></table></body></html>
			""";
	}

	private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
