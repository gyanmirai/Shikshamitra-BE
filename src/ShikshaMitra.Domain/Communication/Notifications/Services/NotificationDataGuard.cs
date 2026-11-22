using System;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Communication.Notifications;

internal static class NotificationDataGuard
{
	internal static Guid Identifier(Guid value, string name) => Check.NotDefaultOrNull<Guid>(value, name);

	internal static string Text(string value, string name, int maxLength, bool allowEmpty = false)
	{
		if (value == null || (!allowEmpty && string.IsNullOrWhiteSpace(value)))
			throw new ArgumentException("A value is required.", name);
		if (value.Length > maxLength) throw new ArgumentException("The value exceeds its allowed length.", name);
		return value;
	}

	internal static string Json(string value, string name)
	{
		Text(value, name, 65536);
		using var document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = 32 });
		return value;
	}
}
