using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace ShikshaMitra.Communication.Notifications;

internal enum WhatsAppDeliveryReceiptStatus { Sent, Delivered, Read, Failed }

internal sealed record WhatsAppDeliveryReceipt(string BusinessAccountId, string PhoneNumberId, string MessageId,
	WhatsAppDeliveryReceiptStatus Status, DateTimeOffset ObservedAt);

internal static class WhatsAppWebhookPayload
{
	private const int MaximumReceipts = 1000;

	internal static IReadOnlyList<WhatsAppDeliveryReceipt> ReadReceipts(ReadOnlyMemory<byte> payload)
	{
		using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 24 });
		if (ReadText(document.RootElement, "object") != "whatsapp_business_account" ||
			!TryReadMember(document.RootElement, "entry", JsonValueKind.Array, out var entries))
			throw new JsonException();

		List<WhatsAppDeliveryReceipt> receipts = [];
		foreach (var entry in entries.EnumerateArray())
		{
			var businessId = ReadText(entry, "id");
			if (!WhatsAppProviderClient.ValidAccountId(businessId) || !TryReadMember(entry, "changes", JsonValueKind.Array, out var changes))
				throw new JsonException();
			foreach (var change in changes.EnumerateArray())
			{
				if (ReadText(change, "field") != "messages") continue;
				ReadMessageReceipts(change, businessId!, receipts);
			}
		}
		return receipts;
	}

	private static void ReadMessageReceipts(JsonElement change, string businessId, List<WhatsAppDeliveryReceipt> receipts)
	{
		if (!TryReadMember(change, "value", JsonValueKind.Object, out var value) || ReadText(value, "messaging_product") != "whatsapp")
			throw new JsonException();
		if (!value.TryGetProperty("statuses", out var statuses)) return;
		if (statuses.ValueKind != JsonValueKind.Array || !TryReadMember(value, "metadata", JsonValueKind.Object, out var metadata))
			throw new JsonException();
		var phoneId = ReadText(metadata, "phone_number_id");
		if (!WhatsAppProviderClient.ValidAccountId(phoneId)) throw new JsonException();
		foreach (var status in statuses.EnumerateArray())
		{
			var state = ReadText(status, "status") switch {
				"sent" => WhatsAppDeliveryReceiptStatus.Sent,
				"delivered" => WhatsAppDeliveryReceiptStatus.Delivered,
				"read" => WhatsAppDeliveryReceiptStatus.Read,
				"failed" => WhatsAppDeliveryReceiptStatus.Failed,
				_ => (WhatsAppDeliveryReceiptStatus?)null
			};
			if (state == null) continue;
			if (receipts.Count == MaximumReceipts) throw new JsonException();
			var messageId = ReadText(status, "id");
			var timestamp = ReadText(status, "timestamp");
			if (!WhatsAppProviderClient.ValidMessageId(messageId) ||
				!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
				seconds is < 1 or > 253402300799)
				throw new JsonException();
			receipts.Add(new(businessId, phoneId!, messageId!, state.Value, DateTimeOffset.FromUnixTimeSeconds(seconds)));
		}
	}

	private static string? ReadText(JsonElement element, string name) =>
		TryReadMember(element, name, JsonValueKind.String, out var value) ? value.GetString() : null;

	private static bool TryReadMember(JsonElement element, string name, JsonValueKind kind, out JsonElement value)
	{
		value = default;
		return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value) && value.ValueKind == kind;
	}
}
