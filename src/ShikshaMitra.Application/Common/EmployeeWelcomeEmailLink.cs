using System;

namespace ShikshaMitra.Common;

public static class EmployeeWelcomeEmailLink
{
	public static string Build(string? clientUrl, string? schoolCode)
	{
		if (!Uri.TryCreate(clientUrl?.Trim(), UriKind.Absolute, out var client) ||
			(client.Scheme != Uri.UriSchemeHttps && client.Scheme != Uri.UriSchemeHttp) ||
			!string.IsNullOrEmpty(client.UserInfo) || !string.IsNullOrEmpty(client.Query) || !string.IsNullOrEmpty(client.Fragment))
			throw new InvalidOperationException("App:ClientUrl must be an absolute HTTP(S) application root without credentials, query or fragment.");

		var login = client.AbsoluteUri.TrimEnd('/') + "/login";
		return string.IsNullOrWhiteSpace(schoolCode)
			? login
			: login + "?organization=" + Uri.EscapeDataString(schoolCode.Trim());
	}
}
