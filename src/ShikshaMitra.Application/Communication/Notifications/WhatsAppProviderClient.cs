using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record WhatsAppProviderConnection(string GraphApiVersion, string PhoneNumberId, string BusinessAccountId, string AccessToken)
{
	public override string ToString() => nameof(WhatsAppProviderConnection);
}

internal sealed record WhatsAppTextComponent(string Type, IReadOnlyList<string> Parameters);

internal sealed record WhatsAppApprovedTemplateMessage(string Destination, string TemplateName, string LanguageCode,
	IReadOnlyList<WhatsAppTextComponent> Components)
{
	public override string ToString() => nameof(WhatsAppApprovedTemplateMessage);
}

[RemoteService(false), DisableAuditing]
public sealed class WhatsAppProviderClient(IHttpClientFactory clients) : ITransientDependency
{
	public const string HttpClientName = "AcademicWhatsApp";
	internal const int MaximumTextParameters = 20;
	private const int MaximumResponseBytes = 32768;
	private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);
	private static readonly JsonSerializerOptions PayloadJson = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

	internal async Task<NotificationChannelSendResult> SendAsync(WhatsAppProviderConnection connection, WhatsAppApprovedTemplateMessage message,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (!ValidConnection(connection)) return new(NotificationProviderAcceptance.Rejected, null, NotificationErrorCodes.ProviderUnavailable);
		if (!ValidMessage(message)) return new(NotificationProviderAcceptance.Rejected, null, NotificationErrorCodes.TemplateInvalid);
		var components = message.Components.Select(component => new {
			type = component.Type,
			parameters = component.Parameters.Select(value => new { type = "text", text = value }).ToArray()
		}).ToArray();
		using var request = new HttpRequestMessage(HttpMethod.Post,
			$"https://graph.facebook.com/{connection.GraphApiVersion}/{connection.PhoneNumberId}/messages") {
			Content = JsonContent.Create(new {
				messaging_product = "whatsapp", recipient_type = "individual", to = message.Destination[1..], type = "template",
				template = new { name = message.TemplateName, language = new { code = message.LanguageCode },
					components = components.Length == 0 ? null : components }
			}, options: PayloadJson)
		};
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(SendTimeout);
		try
		{
			using var client = clients.CreateClient(HttpClientName);
			using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
			if (response.StatusCode == HttpStatusCode.TooManyRequests)
				return new(NotificationProviderAcceptance.Rejected, null, "WhatsAppRateLimited", true, ReadRetryAfter(response));
			if ((int)response.StatusCode is >= 400 and < 500 && response.StatusCode != HttpStatusCode.RequestTimeout)
				return new(NotificationProviderAcceptance.Rejected, null, "WhatsAppRequestRejected");
			if (!response.IsSuccessStatusCode) return UnknownAcceptance();
			var messageId = await ReadMessageIdAsync(response.Content, timeout.Token);
			return messageId == null ? UnknownAcceptance() : new(NotificationProviderAcceptance.Accepted, messageId, null);
		}
		catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException)
		{
			return UnknownAcceptance();
		}
	}

	internal static bool ValidAccountId(string? value) => value is { Length: > 0 and <= 32 } && value.All(char.IsAsciiDigit);

	internal static bool ValidMessageId(string? value) => value is { Length: > 6 and <= 256 } &&
		value.StartsWith("wamid.", StringComparison.Ordinal) && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or '+' or '/' or '=');

	internal static bool ValidConnection(WhatsAppProviderConnection connection)
	{
		var version = connection.GraphApiVersion;
		return version is { Length: >= 4 and <= 10 } && version.StartsWith('v') &&
			version[1..].Split('.') is [var major, var minor] && major.Length > 0 && minor.Length > 0 &&
			major.All(char.IsAsciiDigit) && minor.All(char.IsAsciiDigit) &&
			ValidAccountId(connection.PhoneNumberId) && ValidAccountId(connection.BusinessAccountId) &&
			connection.AccessToken is { Length: > 0 and <= 4096 } &&
			connection.AccessToken.All(c => c is >= '!' and <= '~');
	}

	private static bool ValidMessage(WhatsAppApprovedTemplateMessage message)
	{
		var number = message.Destination;
		var language = message.LanguageCode;
		return number is { Length: >= 8 and <= 16 } && number[0] == '+' && number[1] is >= '1' and <= '9' &&
			number.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0 &&
			message.TemplateName is { Length: > 0 and <= 160 } &&
			message.TemplateName.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_') &&
			ValidLanguage(language) &&
			message.Components is { Count: <= 2 } &&
			message.Components.All(c => c.Type is "header" or "body" && c.Parameters is { Count: > 0 and <= MaximumTextParameters } &&
				c.Parameters.All(p => !string.IsNullOrWhiteSpace(p) && p.Length <= 1024 && !p.Any(char.IsControl))) &&
			message.Components.Select(c => c.Type).Distinct(StringComparer.Ordinal).Count() == message.Components.Count;
	}

	internal static bool ValidLanguage(string? language)
	{
		if (language is not { Length: >= 2 and <= 6 }) return false;
		var segments = language.Split('_');
		return segments.Length is 1 or 2 && segments[0].Length is 2 or 3 && segments[0].All(char.IsAsciiLetterLower) &&
			(segments.Length == 1 || segments[1].Length == 2 && segments[1].All(char.IsAsciiLetterUpper));
	}

	private static async Task<string?> ReadMessageIdAsync(HttpContent content, CancellationToken cancellationToken)
	{
		if (content.Headers.ContentLength > MaximumResponseBytes) return null;
		await using var stream = await content.ReadAsStreamAsync(cancellationToken);
		var buffer = new byte[MaximumResponseBytes + 1];
		var count = await stream.ReadAtLeastAsync(buffer, buffer.Length, false, cancellationToken);
		if (count > MaximumResponseBytes) return null;
		using var payload = JsonDocument.Parse(buffer.AsMemory(0, count));
		if (payload.RootElement.ValueKind != JsonValueKind.Object ||
			!payload.RootElement.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array ||
			messages.GetArrayLength() != 1 || messages[0].ValueKind != JsonValueKind.Object ||
			!messages[0].TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String) return null;
		var messageId = id.GetString();
		return ValidMessageId(messageId) ? messageId : null;
	}

	private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
	{
		var header = response.Headers.RetryAfter;
		var delay = header?.Delta ?? (header?.Date - DateTimeOffset.UtcNow);
		return delay > TimeSpan.Zero ? delay : null;
	}

	private static NotificationChannelSendResult UnknownAcceptance() =>
		new(NotificationProviderAcceptance.UnknownAcceptance, null, "WhatsAppAcceptanceUnknown");
}
