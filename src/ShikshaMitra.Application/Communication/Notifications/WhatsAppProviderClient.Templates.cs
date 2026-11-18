using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record WhatsAppProviderTemplate(string Id, NotificationProviderTemplateState Status, string? Header, string? Body,
	string Language, bool IsUtilityText)
{
	internal bool Matches(WhatsAppCompiledTemplate template) => IsUtilityText && Language == template.ProviderLanguage &&
		Header == template.Header && Body == template.Body;
}

internal sealed record WhatsAppTemplateSubmission(string Id, NotificationProviderTemplateState Status);

public sealed partial class WhatsAppProviderClient
{
	internal async Task<WhatsAppProviderTemplate?> FindTemplateAsync(WhatsAppProviderConnection connection, string name, string language,
		CancellationToken cancellationToken)
	{
		ValidateTemplateRequest(connection, name, language);
		using var request = CreateTemplateRequest(connection, HttpMethod.Get,
			"?fields=id,name,language,status,category,components&limit=100&name=" + Uri.EscapeDataString(name));
		using var response = await ReadTemplateResponseAsync(request, cancellationToken);
		var root = response.RootElement;
		if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array ||
			root.TryGetProperty("paging", out var paging) && ReadString(paging, "next") != null)
			throw Unavailable();

		WhatsAppProviderTemplate? match = null;
		foreach (var entry in data.EnumerateArray())
		{
			var actualName = ReadString(entry, "name");
			var actualLanguage = ReadString(entry, "language");
			if (string.IsNullOrWhiteSpace(actualName) || string.IsNullOrWhiteSpace(actualLanguage)) throw Unavailable();
			if (actualName != name || actualLanguage != language) continue;
			if (match != null) throw Unavailable();
			var identity = ReadTemplateIdentity(entry);
			var text = ReadTemplateText(entry);
			match = new(identity.Id, identity.Status, text.Header, text.Body, language, text.IsText &&
				ReadString(entry, "category") == "UTILITY");
		}
		return match;
	}

	internal async Task<WhatsAppTemplateSubmission> SubmitTemplateAsync(WhatsAppProviderConnection connection, string name,
		WhatsAppCompiledTemplate template, IReadOnlyDictionary<string, string> examples, CancellationToken cancellationToken)
	{
		ValidateTemplateRequest(connection, name, template.ProviderLanguage);
		var bodyExamples = ComponentExamples(template, examples, "body");
		var components = new List<object>(2);
		if (template.Header != null)
		{
			var headerExamples = ComponentExamples(template, examples, "header");
			components.Add(new { type = "HEADER", format = "TEXT", text = template.Header,
				example = headerExamples.Length == 0 ? null : new { header_text = headerExamples } });
		}
		components.Add(new { type = "BODY", text = template.Body,
			example = bodyExamples.Length == 0 ? null : new { body_text = new[] { bodyExamples } } });
		using var request = CreateTemplateRequest(connection, HttpMethod.Post, "");
		request.Content = JsonContent.Create(new { name, category = "UTILITY", language = template.ProviderLanguage, components },
			options: PayloadJson);
		using var response = await ReadTemplateResponseAsync(request, cancellationToken);
		return ReadTemplateIdentity(response.RootElement);
	}

	private static void ValidateTemplateRequest(WhatsAppProviderConnection connection, string name, string language)
	{
		if (!ValidConnection(connection)) throw Unavailable();
		if (!ValidTemplateName(name) || !ValidLanguage(language)) throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
	}

	private static string[] ComponentExamples(WhatsAppCompiledTemplate template, IReadOnlyDictionary<string, string> examples, string component) =>
		template.Parameters.Where(x => x.Component == component).Select(x =>
			examples.TryGetValue(x.VariableKey, out var example) && !string.IsNullOrWhiteSpace(example) &&
			example.Length <= 1024 && !example.Any(char.IsControl) ? example :
				throw new BusinessException(NotificationErrorCodes.RequiredVariableMissing)).ToArray();

	private static HttpRequestMessage CreateTemplateRequest(WhatsAppProviderConnection connection, HttpMethod method, string query)
	{
		var request = new HttpRequestMessage(method,
			$"https://graph.facebook.com/{connection.GraphApiVersion}/{connection.BusinessAccountId}/message_templates{query}");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.AccessToken);
		return request;
	}

	private async Task<JsonDocument> ReadTemplateResponseAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(SendTimeout);
		try
		{
			using var client = clients.CreateClient(HttpClientName);
			using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
			if (!response.IsSuccessStatusCode) throw Unavailable();
			var payload = await ReadResponseAsync(response.Content, timeout.Token);
			if (payload?.RootElement.ValueKind == JsonValueKind.Object) return payload;
			payload?.Dispose();
			throw Unavailable();
		}
		catch (Exception error) when (error is HttpRequestException or IOException or JsonException ||
			error is OperationCanceledException && !cancellationToken.IsCancellationRequested)
		{
			throw Unavailable();
		}
	}

	private static WhatsAppTemplateSubmission ReadTemplateIdentity(JsonElement node)
	{
		var id = ReadString(node, "id");
		var status = ReadString(node, "status");
		if (!ValidAccountId(id) || status == null) throw Unavailable();
		return new(id!, status switch {
			"APPROVED" => NotificationProviderTemplateState.Approved,
			"PENDING" or "IN_APPEAL" => NotificationProviderTemplateState.PendingApproval,
			"REJECTED" => NotificationProviderTemplateState.Rejected,
			"PAUSED" => NotificationProviderTemplateState.Paused,
			_ => NotificationProviderTemplateState.Disabled
		});
	}

	private static (string? Header, string? Body, bool IsText) ReadTemplateText(JsonElement node)
	{
		if (!node.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array) return default;
		string? header = null;
		string? body = null;
		foreach (var component in components.EnumerateArray())
		{
			var text = ReadString(component, "text");
			if (string.IsNullOrWhiteSpace(text)) return default;
			if (ReadString(component, "type") == "HEADER" && ReadString(component, "format") == "TEXT" && header == null)
				header = text;
			else if (ReadString(component, "type") == "BODY" && body == null)
				body = text;
			else
				return default;
		}
		return (header, body, body != null);
	}

	private static string? ReadString(JsonElement node, string name) =>
		node.ValueKind == JsonValueKind.Object && node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() : null;

	private static BusinessException Unavailable() => new(NotificationErrorCodes.ProviderUnavailable);
}
