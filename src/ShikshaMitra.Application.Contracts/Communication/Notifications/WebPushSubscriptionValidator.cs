using System;
using System.Linq;
using System.Security.Cryptography;
using FluentValidation;

namespace ShikshaMitra.Communication.Notifications;

public class WebPushSubscriptionValidator : AbstractValidator<RegisterWebPushSubscriptionDto>
{
	public WebPushSubscriptionValidator()
	{
		RuleFor(x => x.BrowserInstallationId).NotEmpty();
		RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(2048);
		RuleFor(x => x.VapidKeyVersion).NotEmpty().MaximumLength(64).Matches(@"^[a-zA-Z0-9._-]+$");
		RuleFor(x => x.Keys).NotNull().SetValidator(new WebPushSubscriptionKeysValidator());
	}
}

public class WebPushSubscriptionKeysValidator : AbstractValidator<WebPushSubscriptionKeysDto>
{
	public WebPushSubscriptionKeysValidator()
	{
		RuleFor(x => x.P256dh).Must(WebPushKeyEncoding.ValidPublicKey).WithMessage("Use a valid P-256 public key.");
		RuleFor(x => x.Auth).Must(x => WebPushKeyEncoding.Decode(x, 16) != null).WithMessage("Use a valid push authentication key.");
	}
}

public static class WebPushKeyEncoding
{
	public static byte[]? Decode(string? encoded, int length)
	{
		if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > 128 || encoded.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not ('-' or '_'))) return null;
		try {
			var value = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/').PadRight((encoded.Length + 3) / 4 * 4, '='));
			return value.Length == length && Encode(value) == encoded ? value : null;
		}
		catch (FormatException) { return null; }
	}
	public static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	public static bool ValidPublicKey(string? encoded)
	{
		var bytes = Decode(encoded, 65);
		if (bytes == null || bytes[0] != 4) return false;
		try {
			using var key = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256,
				Q = new ECPoint { X = bytes[1..33], Y = bytes[33..65] } });
			return key.KeySize == 256;
		}
		catch (CryptographicException) { return false; }
		catch (ArgumentException) { return false; }
	}
}
