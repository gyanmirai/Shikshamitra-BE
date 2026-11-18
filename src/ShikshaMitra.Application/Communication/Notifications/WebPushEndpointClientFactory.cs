using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

public interface IWebPushEndpointClientFactory
{
	HttpClient Create(ValidatedWebPushEndpoint endpoint);
}

[ExposeServices(typeof(IWebPushEndpointClientFactory))]
public sealed class WebPushEndpointClientFactory : IWebPushEndpointClientFactory, ITransientDependency
{
	public HttpClient Create(ValidatedWebPushEndpoint endpoint) => new(new DiscardProviderContent(CreateHandler(endpoint)));

	internal static SocketsHttpHandler CreateHandler(ValidatedWebPushEndpoint endpoint) => new() {
		AllowAutoRedirect = false,
		UseCookies = false,
		UseProxy = false,
		MaxResponseHeadersLength = 16,
		MaxResponseDrainSize = 0,
		ConnectCallback = (context, cancellationToken) => ConnectAsync(endpoint, context.DnsEndPoint, cancellationToken)
	};

	private static async ValueTask<Stream> ConnectAsync(ValidatedWebPushEndpoint endpoint, DnsEndPoint requested,
		CancellationToken cancellationToken)
	{
		if (!string.Equals(endpoint.Uri.IdnHost, requested.Host, StringComparison.OrdinalIgnoreCase) || requested.Port != endpoint.Uri.Port)
			throw new HttpRequestException("Unexpected web push destination.");
		foreach (var address in endpoint.Addresses)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
			try
			{
				await socket.ConnectAsync(new IPEndPoint(address, requested.Port), cancellationToken);
				return new NetworkStream(socket, ownsSocket: true);
			}
			catch (SocketException) { socket.Dispose(); }
			catch { socket.Dispose(); throw; }
		}
		throw new HttpRequestException("Could not connect to a validated web push address.");
	}

	private sealed class DiscardProviderContent(HttpMessageHandler transport) : DelegatingHandler(transport)
	{
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var response = await base.SendAsync(request, cancellationToken);
			response.Content.Dispose();
			response.Content = new ByteArrayContent([]);
			response.ReasonPhrase = null;
			return response;
		}
	}
}
