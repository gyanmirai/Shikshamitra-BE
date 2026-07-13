using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShikshaMitra.Common.Middleware;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Middleware;

public class DevAutoAuthMiddleware_Tests
{
	[Fact]
	public async Task Missing_Enabled_Setting_Should_Leave_Request_Anonymous()
	{
		var context = new DefaultHttpContext();
		var nextCalled = false;
		var middleware = CreateMiddleware(new ConfigurationBuilder().Build(), () => nextCalled = true);

		await middleware.InvokeAsync(context);

		nextCalled.ShouldBeTrue();
		context.Request.Headers.ContainsKey("Authorization").ShouldBeFalse();
		context.Request.Headers.ContainsKey("__tenant").ShouldBeFalse();
	}

	[Fact]
	public async Task Existing_Authorization_Should_Never_Be_Replaced()
	{
		var context = new DefaultHttpContext();
		context.Request.Headers.Authorization = "Bearer caller-token";
		var middleware = CreateMiddleware(
			BuildConfiguration(("DevAutoAuth:Enabled", "true")),
			() => { });

		await middleware.InvokeAsync(context);

		context.Request.Headers.Authorization.ToString().ShouldBe("Bearer caller-token");
		context.Request.Headers.ContainsKey("__tenant").ShouldBeFalse();
	}

	[Fact]
	public async Task Token_Failure_Should_Continue_Without_Elevating_Request()
	{
		var context = new DefaultHttpContext();
		var nextCalled = false;
		var middleware = CreateMiddleware(
			BuildConfiguration(("DevAutoAuth:Enabled", "true")),
			() => nextCalled = true,
			new StaticHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
			{
				Content = new StringContent("{}")
			}));

		await middleware.InvokeAsync(context);

		nextCalled.ShouldBeTrue();
		context.Request.Headers.ContainsKey("Authorization").ShouldBeFalse();
		context.Request.Headers.ContainsKey("__tenant").ShouldBeFalse();
	}

	private static DevAutoAuthMiddleware CreateMiddleware(
		IConfiguration configuration,
		Action next,
		HttpMessageHandler? handler = null)
	{
		return new DevAutoAuthMiddleware(
			_ =>
			{
				next();
				return Task.CompletedTask;
			},
			configuration,
			new TestHttpClientFactory(handler ?? new StaticHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK))),
			NullLogger<DevAutoAuthMiddleware>.Instance);
	}

	private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(values.ToDictionary(item => item.Key, item => (string?)item.Value))
			.Build();

	private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
	{
		public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
	}

	private sealed class StaticHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) => Task.FromResult(response);
	}
}
