using System;
using System.Buffers;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false)]
public sealed class WhatsAppWebhookAuthenticator(IConfiguration configuration) : ITransientDependency
{
	internal const string SettingsPrefix = "Notifications:WhatsApp:Webhook";
	private const string SignaturePrefix = "sha256=";
	private const int DigestBytes = 32;

	internal string? VerifyChallenge(string? mode, string? token, string? challenge)
	{
		var configured = configuration[$"{SettingsPrefix}:VerificationToken"];
		if (mode != "subscribe" || !ValidSecret(configured) || !ValidSecret(token) ||
			challenge is not { Length: > 0 and <= 32 } || !challenge.All(char.IsAsciiDigit))
			return null;

		var expected = SHA256.HashData(Encoding.UTF8.GetBytes(configured!));
		var actual = SHA256.HashData(Encoding.UTF8.GetBytes(token!));
		return CryptographicOperations.FixedTimeEquals(actual, expected) ? challenge : null;
	}

	internal bool ValidateSignature(string? header, ReadOnlySpan<byte> payload)
	{
		var secret = configuration[$"{SettingsPrefix}:AppSecret"];
		if (!ValidSecret(secret) || payload.IsEmpty || header == null || header.Length != SignaturePrefix.Length + DigestBytes * 2 ||
			!header.StartsWith(SignaturePrefix, StringComparison.Ordinal))
			return false;

		Span<byte> supplied = stackalloc byte[DigestBytes];
		if (Convert.FromHexString(header.AsSpan(SignaturePrefix.Length), supplied, out _, out _) != OperationStatus.Done)
			return false;

		Span<byte> expected = stackalloc byte[DigestBytes];
		HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret!), payload, expected);
		return CryptographicOperations.FixedTimeEquals(supplied, expected);
	}

	private static bool ValidSecret(string? value) => value is { Length: > 0 and <= 256 } && !string.IsNullOrWhiteSpace(value);
}
