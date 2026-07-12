using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmCommunicationDispatcher_Tests
{
	[Fact]
	public async Task Should_Capture_Provider_Message_Id_From_Configured_Webhook_Response_Path()
	{
		var provider = CreateProvider(new
		{
			responseProviderMessageIdPath = "messages.0.id"
		});
		var dispatcher = CreateDispatcher(new
		{
			messages = new[]
			{
				new { id = "wamid.crm-message-123" }
			}
		});

		var result = await dispatcher.DispatchAsync(provider, CreateRequest());

		result.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.SentStatus);
		result.ProviderMessageId.ShouldBe("wamid.crm-message-123");
		result.StatusDetails.ShouldContain("Provider message id: wamid.crm-message-123");
	}

	[Fact]
	public async Task Should_Capture_Provider_Message_Id_From_Default_Webhook_Response_Keys()
	{
		var provider = CreateProvider(new { adapter = "webhook" });
		var dispatcher = CreateDispatcher(new
		{
			data = new
			{
				messageId = "sms-message-456"
			}
		});

		var result = await dispatcher.DispatchAsync(provider, CreateRequest());

		result.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.SentStatus);
		result.ProviderMessageId.ShouldBe("sms-message-456");
	}

	[Fact]
	public void Should_Record_Dispatch_Provider_Message_Id_Without_Overwriting_Callback_Id()
	{
		var log = new AdmissionCrmMessageDeliveryLog(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			AdmissionCrmMessageChannel.WhatsApp,
			"9999999999",
			"WhatsApp Sandbox",
			null,
			"Message body",
			AdmissionCrmMessageDeliveryLog.QueuedStatus,
			"Queued for provider.",
			DateTime.UtcNow);

		log.ApplyDispatchResult(
			AdmissionCrmMessageDeliveryLog.SentStatus,
			"Sent to provider.",
			DateTime.UtcNow,
			"dispatch-message-id");
		log.ApplyProviderCallback(
			AdmissionCrmMessageDeliveryLog.DeliveredStatus,
			"Delivered callback received.",
			"callback-message-id",
			DateTime.UtcNow);

		log.ProviderMessageId.ShouldBe("dispatch-message-id");
		log.Status.ShouldBe(AdmissionCrmMessageDeliveryLog.DeliveredStatus);
	}

	private static AdmissionCrmCommunicationDispatcher CreateDispatcher(object responseBody)
	{
		var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted)
		{
			Content = new StringContent(
				JsonSerializer.Serialize(responseBody),
				Encoding.UTF8,
				"application/json")
		});

		return new AdmissionCrmCommunicationDispatcher(
			new StubHttpClientFactory(handler),
			NullLogger<AdmissionCrmCommunicationDispatcher>.Instance);
	}

	private static AdmissionCrmCommunicationProviderConfiguration CreateProvider(object settings)
	{
		var provider = new AdmissionCrmCommunicationProviderConfiguration(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			AdmissionCrmMessageChannel.WhatsApp);
		provider.Update(
			isEnabled: true,
			providerName: "WhatsApp Sandbox",
			senderId: "school-main",
			settingsJson: JsonSerializer.Serialize(settings),
			webhookUrl: "https://provider.example.test/send",
			lastValidatedAt: null);

		return provider;
	}

	private static AdmissionCrmCommunicationDispatchRequest CreateRequest()
	{
		return new AdmissionCrmCommunicationDispatchRequest
		{
			AcademicYearId = Guid.NewGuid(),
			LeadId = Guid.NewGuid(),
			TemplateId = Guid.NewGuid(),
			Channel = AdmissionCrmMessageChannel.WhatsApp,
			Recipient = "9999999999",
			Body = "Admission message body",
			Trigger = "Manual"
		};
	}

	private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
	{
		public HttpClient CreateClient(string name)
		{
			return new HttpClient(handler, disposeHandler: false);
		}
	}

	private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
		: HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			return Task.FromResult(handler(request));
		}
	}
}
