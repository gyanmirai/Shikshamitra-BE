using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class WhatsAppWebhookAuthenticator_Tests
{
	private const string Secret = "local-meta-app-secret";
	private const string VerificationToken = "local-meta-verify-token";
	private static readonly WhatsAppWebhookAuthenticator Authenticator = Create(Secret, VerificationToken);

	[Fact]
	public void Only_the_original_raw_body_can_pass_authentication()
	{
		var body = """{"object":"whatsapp_business_account","entry":[]}"""u8;
		var signature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData("local-meta-app-secret"u8, body));

		Authenticator.ValidateSignature(signature, body).ShouldBeTrue();
		Authenticator.ValidateSignature(signature, """{"object": "whatsapp_business_account", "entry":[]}"""u8).ShouldBeFalse();
		Authenticator.ValidateSignature(signature, ReadOnlySpan<byte>.Empty).ShouldBeFalse();
		Create(null, VerificationToken).ValidateSignature(signature, body).ShouldBeFalse();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("sha1=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
	[InlineData("sha256=zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
	[InlineData("sha256=aa")]
	[InlineData("sha256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,sha256=aa")]
	public void Invalid_or_multiple_signatures_cannot_authenticate(string? signature)
	{
		Authenticator.ValidateSignature(signature, "{}"u8).ShouldBeFalse();
	}

	[Fact]
	public void Verification_requires_the_configured_token_and_returns_only_a_valid_challenge()
	{
		Authenticator.VerifyChallenge("subscribe", VerificationToken, "1158201444").ShouldBe("1158201444");
		Authenticator.VerifyChallenge("unsubscribe", VerificationToken, "1158201444").ShouldBeNull();
		Authenticator.VerifyChallenge("subscribe", "wrong-token", "1158201444").ShouldBeNull();
		Authenticator.VerifyChallenge("subscribe", VerificationToken, "<script>").ShouldBeNull();
		Create(Secret, null).VerifyChallenge("subscribe", "", "1158201444").ShouldBeNull();
	}

	private static WhatsAppWebhookAuthenticator Create(string? secret, string? verificationToken) => new(new ConfigurationBuilder()
		.AddInMemoryCollection(new Dictionary<string, string?> {
			["Notifications:WhatsApp:Webhook:AppSecret"] = secret,
			["Notifications:WhatsApp:Webhook:VerificationToken"] = verificationToken
		}).Build());
}
