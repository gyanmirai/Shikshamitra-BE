using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCommunicationDispatchRequest
{
	public Guid AcademicYearId { get; set; }
	public Guid? LeadId { get; set; }
	public Guid? TemplateId { get; set; }
	public AdmissionCrmMessageChannel Channel { get; set; }
	public string Recipient { get; set; } = string.Empty;
	public string? Subject { get; set; }
	public string Body { get; set; } = string.Empty;
	public string Trigger { get; set; } = "Manual";
	public IReadOnlyList<AdmissionCrmCommunicationAttachment> Attachments { get; set; } = [];
}

public class AdmissionCrmCommunicationAttachment
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = "application/octet-stream";
	public byte[] Content { get; set; } = [];
}

public class AdmissionCrmCommunicationDispatchResult
{
	public string Status { get; set; } = AdmissionCrmMessageDeliveryLog.QueuedStatus;
	public string StatusDetails { get; set; } = string.Empty;
	public string? ProviderMessageId { get; set; }
}

public interface IAdmissionCrmCommunicationDispatcher
{
	Task<AdmissionCrmCommunicationDispatchResult> DispatchAsync(
		AdmissionCrmCommunicationProviderConfiguration provider,
		AdmissionCrmCommunicationDispatchRequest request);
}

public class AdmissionCrmCommunicationDispatcher(
	IHttpClientFactory httpClientFactory,
	ILogger<AdmissionCrmCommunicationDispatcher> logger)
	: IAdmissionCrmCommunicationDispatcher, ITransientDependency
{
	public async Task<AdmissionCrmCommunicationDispatchResult> DispatchAsync(
		AdmissionCrmCommunicationProviderConfiguration provider,
		AdmissionCrmCommunicationDispatchRequest request)
	{
		var settings = ParseSettings(provider);
		var adapter = NormalizeAdapter(settings.Adapter);
		if (adapter is "manual" or "manual-queue")
		{
			return new AdmissionCrmCommunicationDispatchResult
			{
				Status = AdmissionCrmMessageDeliveryLog.QueuedStatus,
				StatusDetails = $"Queued for {provider.ProviderName}. Provider adapter is manual."
			};
		}

		if (adapter is not ("webhook" or "generic-webhook" or "custom-webhook"))
		{
			return new AdmissionCrmCommunicationDispatchResult
			{
				Status = AdmissionCrmMessageDeliveryLog.FailedStatus,
				StatusDetails = $"Provider adapter '{settings.Adapter}' is not supported."
			};
		}

		if (string.IsNullOrWhiteSpace(provider.WebhookUrl))
		{
			return new AdmissionCrmCommunicationDispatchResult
			{
				Status = AdmissionCrmMessageDeliveryLog.QueuedStatus,
				StatusDetails = $"Queued for {provider.ProviderName}. Provider webhook is not configured yet."
			};
		}

		if (!Uri.TryCreate(provider.WebhookUrl, UriKind.Absolute, out var webhookUri) ||
		    webhookUri.Scheme is not ("http" or "https"))
		{
			return new AdmissionCrmCommunicationDispatchResult
			{
				Status = AdmissionCrmMessageDeliveryLog.FailedStatus,
				StatusDetails = "Provider webhook URL is invalid."
			};
		}

		try
		{
			var client = httpClientFactory.CreateClient();
			using var httpRequest = new HttpRequestMessage(HttpMethod.Post, webhookUri)
			{
				Content = JsonContent.Create(BuildPayload(provider, request, settings))
			};

			foreach (var header in settings.Headers)
			{
				httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}

			using var response = await client.SendAsync(httpRequest);

			var responseBody = await response.Content.ReadAsStringAsync();
			if (response.IsSuccessStatusCode)
			{
				var providerMessageId = ExtractProviderMessageId(responseBody, settings);
				var statusDetails = $"Sent to {provider.ProviderName} webhook with HTTP {(int)response.StatusCode}.";
				if (!string.IsNullOrWhiteSpace(providerMessageId))
				{
					statusDetails = $"{statusDetails} Provider message id: {providerMessageId}.";
				}

				return new AdmissionCrmCommunicationDispatchResult
				{
					Status = AdmissionCrmMessageDeliveryLog.SentStatus,
					StatusDetails = statusDetails,
					ProviderMessageId = providerMessageId
				};
			}

			return new AdmissionCrmCommunicationDispatchResult
			{
				Status = AdmissionCrmMessageDeliveryLog.FailedStatus,
				StatusDetails = $"Provider webhook returned HTTP {(int)response.StatusCode}."
			};
		}
		catch (Exception ex)
		{
			logger.LogWarning(
				ex,
				"Admission CRM communication dispatch failed for channel {Channel} and provider {ProviderName}.",
				request.Channel,
				provider.ProviderName);

			return new AdmissionCrmCommunicationDispatchResult
			{
				Status = AdmissionCrmMessageDeliveryLog.FailedStatus,
				StatusDetails = "Provider webhook dispatch failed."
			};
		}
	}

	private static AdmissionCrmProviderDispatchSettings ParseSettings(
		AdmissionCrmCommunicationProviderConfiguration provider)
	{
		if (string.IsNullOrWhiteSpace(provider.SettingsJson))
		{
			return new AdmissionCrmProviderDispatchSettings();
		}

		try
		{
			return JsonSerializer.Deserialize<AdmissionCrmProviderDispatchSettings>(
				       provider.SettingsJson,
				       new JsonSerializerOptions(JsonSerializerDefaults.Web))
			       ?? new AdmissionCrmProviderDispatchSettings();
		}
		catch (JsonException)
		{
			return new AdmissionCrmProviderDispatchSettings
			{
				Adapter = "unsupported-json"
			};
		}
	}

	private static string NormalizeAdapter(string? adapter)
	{
		return string.IsNullOrWhiteSpace(adapter)
			? "webhook"
			: adapter.Trim().ToLowerInvariant();
	}

	private static object BuildPayload(
		AdmissionCrmCommunicationProviderConfiguration provider,
		AdmissionCrmCommunicationDispatchRequest request,
		AdmissionCrmProviderDispatchSettings settings)
	{
		var payloadMode = string.IsNullOrWhiteSpace(settings.PayloadMode)
			? "default"
			: settings.PayloadMode.Trim().ToLowerInvariant();

		var attachments = request.Attachments.Select(item => new
		{
			item.FileName,
			item.ContentType,
			ContentBase64 = Convert.ToBase64String(item.Content)
		}).ToArray();

		if (payloadMode == "envelope")
		{
			return new
			{
				Provider = new
				{
					Name = provider.ProviderName,
					provider.SenderId,
					Channel = request.Channel.ToString()
				},
				Message = new
				{
					request.Recipient,
					request.Subject,
					request.Body,
					Attachments = attachments
				},
				Context = new
				{
					request.AcademicYearId,
					request.LeadId,
					request.TemplateId,
					request.Trigger
				},
				Parameters = settings.Parameters
			};
		}

		return new
		{
			request.AcademicYearId,
			request.LeadId,
			request.TemplateId,
			Channel = request.Channel.ToString(),
			ProviderName = provider.ProviderName,
			provider.SenderId,
			request.Recipient,
			request.Subject,
			request.Body,
			request.Trigger,
			Attachments = attachments,
			Parameters = settings.Parameters
		};
	}

	private static string? ExtractProviderMessageId(
		string responseBody,
		AdmissionCrmProviderDispatchSettings settings)
	{
		if (string.IsNullOrWhiteSpace(responseBody))
		{
			return null;
		}

		try
		{
			using var document = JsonDocument.Parse(responseBody);
			foreach (var path in GetProviderMessageIdPaths(settings))
			{
				var value = ExtractPayloadString(document.RootElement, path);
				if (!string.IsNullOrWhiteSpace(value))
				{
					return NormalizeProviderMessageId(value);
				}
			}
		}
		catch (JsonException)
		{
			return null;
		}

		return null;
	}

	private static IEnumerable<string> GetProviderMessageIdPaths(
		AdmissionCrmProviderDispatchSettings settings)
	{
		if (!string.IsNullOrWhiteSpace(settings.ResponseProviderMessageIdPath))
		{
			yield return settings.ResponseProviderMessageIdPath;
		}

		foreach (var path in new[]
		         {
			         "providerMessageId",
			         "messageId",
			         "id",
			         "message.id",
			         "data.providerMessageId",
			         "data.messageId",
			         "data.id",
			         "messages.0.id"
		         })
		{
			yield return path;
		}
	}

	private static string? ExtractPayloadString(JsonElement root, string path)
	{
		var current = root;
		foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			if (current.ValueKind == JsonValueKind.Object)
			{
				if (!TryGetPropertyIgnoreCase(current, segment, out current))
				{
					return null;
				}
			}
			else if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index))
			{
				if (!TryGetArrayElement(current, index, out current))
				{
					return null;
				}
			}
			else
			{
				return null;
			}
		}

		return current.ValueKind switch
		{
			JsonValueKind.String => current.GetString(),
			JsonValueKind.Number => current.ToString(),
			JsonValueKind.True => current.ToString(),
			JsonValueKind.False => current.ToString(),
			_ => null
		};
	}

	private static bool TryGetPropertyIgnoreCase(
		JsonElement element,
		string propertyName,
		out JsonElement value)
	{
		foreach (var property in element.EnumerateObject())
		{
			if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
			{
				value = property.Value;
				return true;
			}
		}

		value = default;
		return false;
	}

	private static bool TryGetArrayElement(JsonElement element, int index, out JsonElement value)
	{
		var currentIndex = 0;
		foreach (var item in element.EnumerateArray())
		{
			if (currentIndex == index)
			{
				value = item;
				return true;
			}

			currentIndex++;
		}

		value = default;
		return false;
	}

	private static string NormalizeProviderMessageId(string value)
	{
		var trimmed = value.Trim();
		return trimmed.Length <= AdmissionCrmConsts.MaxProviderMessageIdLength
			? trimmed
			: trimmed[..AdmissionCrmConsts.MaxProviderMessageIdLength];
	}

	private sealed class AdmissionCrmProviderDispatchSettings
	{
		public string? Adapter { get; set; }
		public string? PayloadMode { get; set; }
		public string? ResponseProviderMessageIdPath { get; set; }
		public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	}
}
