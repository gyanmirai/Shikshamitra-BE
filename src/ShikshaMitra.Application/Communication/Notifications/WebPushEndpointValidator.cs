using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

public interface IWebPushEndpointDnsResolver
{
	Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}
public sealed record ValidatedWebPushEndpoint(Uri Uri, IReadOnlyList<IPAddress> Addresses);

public sealed class WebPushEndpointValidator(IConfiguration configuration, IWebPushEndpointDnsResolver dns) : ITransientDependency
{
	private const string Invalid = NotificationErrorCodes.PushEndpointInvalid;
	public async Task<ValidatedWebPushEndpoint> ValidateAsync(string endpoint, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Length > 2048 || endpoint.Any(char.IsControl) || endpoint.Contains('\\') ||
			!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
			uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || uri.HostNameType != UriHostNameType.Dns || uri.Host.EndsWith('.') ||
			!configuration.GetSection("Notifications:WebPush:AllowedOrigins").GetChildren().Any(x =>
				Uri.TryCreate(x.Value, UriKind.Absolute, out var allowed) && allowed.Scheme == Uri.UriSchemeHttps &&
				allowed.UserInfo.Length == 0 && allowed.AbsolutePath == "/" && allowed.Query.Length == 0 && allowed.Fragment.Length == 0 &&
				string.Equals(allowed.IdnHost, uri.IdnHost, StringComparison.OrdinalIgnoreCase) && allowed.Port == uri.Port))
			throw new BusinessException(Invalid);
		IPAddress[] addresses;
		try { addresses = await dns.ResolveAsync(uri.IdnHost, cancellationToken).WaitAsync(TimeSpan.FromSeconds(3), cancellationToken); }
		catch (Exception error) when (error is SocketException or TimeoutException or ArgumentException)
		{
			throw new BusinessException(Invalid);
		}
		if (addresses.Length == 0 || addresses.Any(x => !IsPublic(x))) throw new BusinessException(Invalid);
		return new(uri, addresses.Distinct().ToArray());
	}

	private static bool IsPublic(IPAddress address)
	{
		if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
		var bytes = address.GetAddressBytes();
		if (address.AddressFamily == AddressFamily.InterNetworkV6)
			return (bytes[0] & 0xe0) == 0x20 && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8);
		if (address.AddressFamily != AddressFamily.InterNetwork) return false;
		return bytes[0] is not (0 or 10 or 127) && bytes[0] < 224 &&
			!(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) && !(bytes[0] == 169 && bytes[1] == 254) &&
			!(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) && !(bytes[0] == 192 && bytes[1] == 168) &&
			!(bytes[0] == 192 && bytes[1] == 0) && !(bytes[0] == 198 && bytes[1] is 18 or 19) &&
			!(bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) && !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
	}
}

[ExposeServices(typeof(IWebPushEndpointDnsResolver))]
public sealed class WebPushEndpointDnsResolver : IWebPushEndpointDnsResolver, ITransientDependency
{
	public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) => Dns.GetHostAddressesAsync(host, cancellationToken);
}
