using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class WebPushEndpointValidator_Tests
{
	private const string Error = "Communication:Notification:PushEndpointInvalid";
	private readonly IWebPushEndpointDnsResolver _dns = Substitute.For<IWebPushEndpointDnsResolver>();
	private readonly WebPushEndpointValidator _validator;
	public WebPushEndpointValidator_Tests()
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:WebPush:AllowedOrigins:0"] = "https://push.example.test"
		}).Build();
		_dns.ResolveAsync("push.example.test", Arg.Any<CancellationToken>()).Returns([IPAddress.Parse("8.8.8.8")]);
		_validator = new(configuration, _dns);
	}

	[Fact]
	public async Task Validated_endpoint_preserves_its_opaque_path_and_query_and_returns_only_the_checked_addresses()
	{
		const string endpoint = "https://push.example.test/opaque-token?key=opaque-value";
		var result = await _validator.ValidateAsync(endpoint, default);
		result.Uri.AbsoluteUri.ShouldBe(endpoint);
		result.Addresses.ShouldBe(new[] { IPAddress.Parse("8.8.8.8") });
		await _dns.Received(1).ResolveAsync("push.example.test", Arg.Any<CancellationToken>());
	}

	[Theory]
	[InlineData("")]
	[InlineData("http://push.example.test/token")]
	[InlineData("https://other.example.test/token")]
	[InlineData("https://push.example.test:444/token")]
	[InlineData("https://user:secret@push.example.test/token")]
	[InlineData("https://push.example.test/token#fragment")]
	[InlineData("https://push.example.test./token")]
	[InlineData("https://push.example.test/\\token")]
	[InlineData("https://push.example.test/token\n")]
	[InlineData("https://127.0.0.1/token")]
	public async Task Invalid_or_unconfigured_origins_are_rejected_before_dns(string endpoint)
	{
		var error = await Should.ThrowAsync<BusinessException>(() => _validator.ValidateAsync(endpoint, default));
		error.Code.ShouldBe(Error);
		await _dns.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default);
	}

	[Theory]
	[InlineData("127.0.0.1")]
	[InlineData("10.1.2.3")]
	[InlineData("172.16.1.1")]
	[InlineData("192.168.1.1")]
	[InlineData("169.254.169.254")]
	[InlineData("100.64.0.1")]
	[InlineData("0.0.0.0")]
	[InlineData("224.0.0.1")]
	[InlineData("::1")]
	[InlineData("::")]
	[InlineData("fe80::1")]
	[InlineData("fc00::1")]
	[InlineData("::ffff:127.0.0.1")]
	[InlineData("64:ff9b::a00:1")]
	[InlineData("2001:db8::1")]
	public async Task Any_private_or_reserved_dns_answer_rejects_the_entire_endpoint(string address)
	{
		_dns.ResolveAsync("push.example.test", Arg.Any<CancellationToken>()).Returns([IPAddress.Parse("8.8.8.8"), IPAddress.Parse(address)]);
		var error = await Should.ThrowAsync<BusinessException>(() => _validator.ValidateAsync("https://push.example.test/token", default));
		error.Code.ShouldBe(Error);
	}

	[Fact]
	public async Task Dns_errors_are_reported_without_embedding_the_endpoint_or_provider_details()
	{
		_dns.ResolveAsync("push.example.test", Arg.Any<CancellationToken>()).Returns(Task.FromException<IPAddress[]>(new SocketException()));
		var error = await Should.ThrowAsync<BusinessException>(() => _validator.ValidateAsync("https://push.example.test/private-token", default));
		error.Code.ShouldBe(Error); error.InnerException.ShouldBeNull(); error.Message.ShouldNotContain("private-token");
	}

	[Fact]
	public async Task Cancellation_is_preserved_and_never_converted_to_a_success_or_provider_error()
	{
		using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
		await Should.ThrowAsync<OperationCanceledException>(() => _validator.ValidateAsync("https://push.example.test/token", cancellation.Token));
	}
}
