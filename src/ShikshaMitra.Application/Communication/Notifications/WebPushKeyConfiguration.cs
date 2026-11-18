using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

internal sealed record WebPushApplicationKeys(string PublicKey, string PrivateKey, string Version);

public sealed class WebPushKeyConfiguration(IConfiguration configuration) : ITransientDependency
{
	internal WebPushApplicationKeys? Read()
	{
		var publicKey = configuration["Notifications:WebPush:PublicKey"];
		var privateKey = configuration["Notifications:WebPush:PrivateKey"];
		var version = configuration["Notifications:WebPush:KeyVersion"];
		if (!WebPushKeyEncoding.ValidPublicKey(publicKey) || WebPushKeyEncoding.Decode(privateKey, 32) is not byte[] secret ||
			string.IsNullOrWhiteSpace(version) || version.Length > 64 || version.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not ('.' or '-' or '_')) ||
			!configuration.GetSection("Notifications:WebPush:AllowedOrigins").GetChildren().Any()) return null;
		var bytes = WebPushKeyEncoding.Decode(publicKey, 65)!;
		try {
			using var signing = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = secret,
				Q = new ECPoint { X = bytes[1..33], Y = bytes[33..65] } });
			using var verifying = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256,
				Q = new ECPoint { X = bytes[1..33], Y = bytes[33..65] } });
			var challenge = Encoding.UTF8.GetBytes("ShikshaMitra.WebPush.KeyPair.v1");
			return verifying.VerifyData(challenge, signing.SignData(challenge, HashAlgorithmName.SHA256), HashAlgorithmName.SHA256)
				? new(publicKey!, privateKey!, version) : null;
		}
		catch (CryptographicException) { return null; }
		catch (ArgumentException) { return null; }
	}
}
