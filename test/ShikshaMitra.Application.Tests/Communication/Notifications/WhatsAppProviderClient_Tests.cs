using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class WhatsAppProviderClient_Tests
{
	private static readonly WhatsAppProviderConnection Connection = new("v26.0", "100012345678901", "100098765432101", "synthetic-test-token");
	private static readonly WhatsAppApprovedTemplateMessage Message = new("+919000000012", "academic_homework_due", "en_US",
		[new("body", ["First value", "Second value"])]);

	[Fact]
	public async Task Sends_an_official_versioned_template_with_exact_language_and_parameter_order()
	{
		using var handler = new Handler(request => {
			request.Method.ShouldBe(HttpMethod.Post);
			request.RequestUri!.AbsoluteUri.ShouldBe("https://graph.facebook.com/v26.0/100012345678901/messages");
			request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
			request.Headers.Authorization.Parameter.ShouldBe("synthetic-test-token");
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"messages\":[{\"id\":\"wamid.test\",\"message_status\":\"accepted\"}]}") };
		});
		using var http = new HttpClient(handler);

		var result = await CreateClient(http).SendAsync(Connection, Message, default);

		result.Acceptance.ShouldBe(NotificationProviderAcceptance.Accepted);
		result.ProviderMessageId.ShouldBe("wamid.test");
		using var payload = JsonDocument.Parse(handler.Body!);
		var root = payload.RootElement;
		root.GetProperty("messaging_product").GetString().ShouldBe("whatsapp");
		root.GetProperty("recipient_type").GetString().ShouldBe("individual");
		root.GetProperty("type").GetString().ShouldBe("template");
		root.GetProperty("to").GetString().ShouldBe("919000000012");
		var template = root.GetProperty("template");
		template.GetProperty("name").GetString().ShouldBe("academic_homework_due");
		template.GetProperty("language").GetProperty("code").GetString().ShouldBe("en_US");
		var body = template.GetProperty("components")[0];
		body.GetProperty("type").GetString().ShouldBe("body");
		var parameters = body.GetProperty("parameters");
		parameters[0].GetProperty("type").GetString().ShouldBe("text");
		parameters[0].GetProperty("text").GetString().ShouldBe("First value");
		parameters[1].GetProperty("text").GetString().ShouldBe("Second value");
	}

	[Theory]
	[InlineData(429, "{}", NotificationProviderAcceptance.Rejected, true)]
	[InlineData(400, "{\"error\":{\"message\":\"private provider detail\"}}", NotificationProviderAcceptance.Rejected, false)]
	[InlineData(408, "{}", NotificationProviderAcceptance.UnknownAcceptance, false)]
	[InlineData(502, "{}", NotificationProviderAcceptance.UnknownAcceptance, false)]
	[InlineData(200, "{\"messages\":[]}", NotificationProviderAcceptance.UnknownAcceptance, false)]
	public async Task Maps_rejections_and_unconfirmed_responses_without_exposing_provider_content(int status, string body,
		NotificationProviderAcceptance expected, bool retry)
	{
		using var handler = new Handler(_ => {
			var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) };
			response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
			return response;
		});
		using var http = new HttpClient(handler);

		var result = await CreateClient(http).SendAsync(Connection, Message, default);

		result.Acceptance.ShouldBe(expected);
		result.IsTransient.ShouldBe(retry);
		result.RetryAfter.ShouldBe(retry ? TimeSpan.FromSeconds(30) : null);
		result.ProviderMessageId.ShouldBeNull();
		result.ErrorCode.ShouldNotBeNull().ShouldNotContain("private provider detail");
	}

	[Fact]
	public async Task Transport_failure_leaves_acceptance_unknown()
	{
		using var handler = new Handler(_ => throw new HttpRequestException("private destination"));
		using var http = new HttpClient(handler);

		var result = await CreateClient(http).SendAsync(Connection, Message, default);

		result.Acceptance.ShouldBe(NotificationProviderAcceptance.UnknownAcceptance);
		result.IsTransient.ShouldBeFalse();
		result.ErrorCode.ShouldBe("WhatsAppAcceptanceUnknown");
	}

	[Fact]
	public async Task An_oversized_success_body_is_an_unconfirmed_send()
	{
		using var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
			Content = new StringContent("{\"messages\":[{\"id\":\"wamid.test\"}],\"padding\":\"" + new string('x', 33000) + "\"}")
		});
		using var http = new HttpClient(handler);

		var result = await CreateClient(http).SendAsync(Connection, Message, default);

		result.Acceptance.ShouldBe(NotificationProviderAcceptance.UnknownAcceptance);
		result.ProviderMessageId.ShouldBeNull();
	}

	[Fact]
	public async Task A_static_template_omits_the_parameter_components()
	{
		using var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
			Content = new StringContent("{\"messages\":[{\"id\":\"wamid.static\"}]}")
		});
		using var http = new HttpClient(handler);

		var result = await CreateClient(http).SendAsync(Connection, Message with { Components = [] }, default);

		result.Acceptance.ShouldBe(NotificationProviderAcceptance.Accepted);
		using var payload = JsonDocument.Parse(handler.Body!);
		payload.RootElement.GetProperty("template").TryGetProperty("components", out _).ShouldBeFalse();
	}

	[Fact]
	public async Task Invalid_connection_and_template_inputs_never_contact_the_provider()
	{
		using var handler = new Handler(_ => throw new InvalidOperationException("Unexpected network request"));
		using var http = new HttpClient(handler);
		var client = CreateClient(http);

		(await client.SendAsync(Connection with { GraphApiVersion = "../outside" }, Message, default)).ErrorCode
			.ShouldBe(NotificationErrorCodes.ProviderUnavailable);
		(await client.SendAsync(Connection, Message with { LanguageCode = "en us" }, default)).ErrorCode
			.ShouldBe(NotificationErrorCodes.TemplateInvalid);
		handler.Calls.ShouldBe(0);
		Connection.ToString().ShouldNotContain("synthetic-test-token");
	}

	private static WhatsAppProviderClient CreateClient(HttpClient http)
	{
		var clients = Substitute.For<IHttpClientFactory>();
		clients.CreateClient(WhatsAppProviderClient.HttpClientName).Returns(http);
		return new(clients);
	}

	private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
	{
		public string? Body { get; private set; }
		public int Calls { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Calls++;
			Body = await request.Content!.ReadAsStringAsync(cancellationToken);
			return response(request);
		}
	}
}
