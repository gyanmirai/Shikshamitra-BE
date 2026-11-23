using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record WebPushApplicationKeys(string PublicKey, string PrivateKey, string Version, string? Subject = null)
{
	public override string ToString() => nameof(WebPushApplicationKeys);
}

public sealed class WebPushKeyConfiguration(IConfiguration configuration, ICurrentTenant tenant) : ITransientDependency
{
	internal WebPushApplicationKeys? Read()
	{
		var pilotTenant = configuration["Notifications:WebPush:PilotTenantId"];
		if (pilotTenant != null && (!Guid.TryParse(pilotTenant, out var pilotTenantId) ||
			pilotTenantId == Guid.Empty || pilotTenantId != tenant.Id)) return null;
		var publicKey = configuration["Notifications:WebPush:PublicKey"];
		var privateKey = configuration["Notifications:WebPush:PrivateKey"];
		var version = configuration["Notifications:WebPush:KeyVersion"];
		var subject = configuration["Notifications:WebPush:Subject"];
		if (!string.IsNullOrEmpty(subject) && !ValidSubject(subject)) return null;
		if (!WebPushKeyEncoding.ValidPublicKey(publicKey) || WebPushKeyEncoding.Decode(privateKey, 32) is not byte[] secret ||
			string.IsNullOrWhiteSpace(version) || version.Length > 64 || version.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not ('.' or '-' or '_')) ||
			!WebPushEndpointValidator.ReadAllowedOrigins(configuration).Any()) return null;
		var bytes = WebPushKeyEncoding.Decode(publicKey, 65)!;
		try {
			using var signing = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = secret,
				Q = new ECPoint { X = bytes[1..33], Y = bytes[33..65] } });
			using var verifying = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256,
				Q = new ECPoint { X = bytes[1..33], Y = bytes[33..65] } });
			var challenge = Encoding.UTF8.GetBytes("ShikshaMitra.WebPush.KeyPair.v1");
			return verifying.VerifyData(challenge, signing.SignData(challenge, HashAlgorithmName.SHA256), HashAlgorithmName.SHA256)
				? new(publicKey!, privateKey!, version, string.IsNullOrEmpty(subject) ? null : subject) : null;
		}
		catch (CryptographicException) { return null; }
		catch (ArgumentException) { return null; }
	}

	private static bool ValidSubject(string value) => value.Length <= 2048 && !value.Any(c => char.IsControl(c) || c is '"' or '\\') &&
		Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Fragment.Length == 0 &&
		(uri.Scheme == Uri.UriSchemeMailto && value.Length > "mailto:".Length ||
		 uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 && uri.Host.Length > 0);
}
