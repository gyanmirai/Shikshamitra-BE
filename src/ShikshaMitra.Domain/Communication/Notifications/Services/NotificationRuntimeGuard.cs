using System;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Communication.Notifications;

internal static class NotificationRuntimeGuard
{
	internal static void State(bool allowed)
	{
		if (!allowed) throw new BusinessException(NotificationErrorCodes.StateConflict);
	}
	internal static void Lease(bool allowed)
	{
		if (!allowed) throw new BusinessException(NotificationErrorCodes.LeaseLost);
	}
	internal static void Snapshot(string json)
	{
		try {
			if (string.IsNullOrWhiteSpace(json) || json.Length > 65536) throw new JsonException();
			using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
			if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("schemaVersion", out var version) ||
				!version.TryGetInt32(out var number) || number != 1) throw new JsonException();
		}
		catch (Exception error) when (error is JsonException or InvalidOperationException)
		{
			throw new BusinessException(NotificationErrorCodes.RuntimeSnapshotInvalid);
		}
	}
	internal static void ErrorCode(string? code)
	{
		State(code == null || code.Length is > 0 and <= 160 && code.All(x => char.IsAsciiLetterOrDigit(x) || x is ':' or '.' or '_' or '-'));
	}
}
