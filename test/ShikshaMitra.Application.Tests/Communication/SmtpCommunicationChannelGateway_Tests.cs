using System;
using System.Net.Mail;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ShikshaMitra.Communication;
using Shouldly;
using Volo.Abp.Emailing;
using Xunit;

namespace ShikshaMitra.CommunicationTests;

public class SmtpCommunicationChannelGateway_Tests
{
	[Fact]
	public async Task Smtp_uses_the_existing_sender_and_returns_transport_acceptance()
	{
		var sender = Substitute.For<IEmailSender>();
		var gateway = new SmtpCommunicationChannelGateway(sender);
		gateway.CanHandle(CommunicationChannelType.Email, "smtp").ShouldBeTrue();
		gateway.CanHandle(CommunicationChannelType.Email, "OtherProvider").ShouldBeFalse();
		gateway.CanHandle(CommunicationChannelType.Sms, "smtp").ShouldBeFalse();
		var result = await gateway.SendAsync(new CommunicationSendRequest
		{
			Destination = "test@example.invalid", Subject = "Interested", Body = "Next steps"
		});
		await sender.Received(1).SendAsync("test@example.invalid", "Interested", "Next steps", false);
		result.Status.ShouldBe(CommunicationDeliveryStatus.Sent);
	}

	[Fact]
	public async Task Registration_html_escapes_parent_application_and_url_and_does_not_append_a_second_link()
	{
		var sender = Substitute.For<IEmailSender>();
		const string link = "https://school.example/admission-registration?example=1&other=2#token=private-capability";
		var html = ParentRegistrationEmailContent.Html("Parent <script>bad()</script>", "APP-\"<test>", link);
		html.ShouldContain("Parent &lt;script&gt;bad()&lt;/script&gt;");
		html.ShouldContain("APP-&quot;&lt;test&gt;");
		html.ShouldContain("href=\"https://school.example/admission-registration?example=1&amp;other=2#token=private-capability\"");
		html.ShouldNotContain("<script>");
		var result = await new SmtpCommunicationChannelGateway(sender).SendAsync(new()
		{
			Destination = "parent@example.invalid", Subject = "Registration", Body = "Plain guidance", HtmlBody = html, LinkUrl = link
		});
		await sender.Received(1).SendAsync("parent@example.invalid", "Registration", html, true);
		result.Status.ShouldBe(CommunicationDeliveryStatus.Sent);
		ParentRegistrationEmailContent.PlainText().ShouldNotContain("private-capability");
	}

	[Theory]
	[InlineData("javascript:alert(1)")]
	[InlineData("file:///local-file")]
	[InlineData("/admission-registration")]
	public void Registration_html_rejects_non_web_or_relative_links(string link) =>
		Should.Throw<ArgumentException>(() => ParentRegistrationEmailContent.Html("Parent", "APP-1", link));

	[Fact]
	public async Task Ordinary_plain_text_email_keeps_its_existing_link_behavior()
	{
		var sender = Substitute.For<IEmailSender>();
		await new SmtpCommunicationChannelGateway(sender).SendAsync(new()
		{
			Destination = "test@example.invalid", Subject = "Next steps", Body = "Ordinary message", LinkUrl = "https://school.example/inquiry"
		});
		await sender.Received(1).SendAsync("test@example.invalid", "Next steps", "Ordinary message\n\nhttps://school.example/inquiry", false);
	}

	[Fact]
	public async Task Transport_failure_is_recorded_without_exposing_provider_details()
	{
		var sender = Substitute.For<IEmailSender>();
		sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), false)
			.ThrowsAsync(new InvalidOperationException("sensitive provider configuration"));
		var result = await new SmtpCommunicationChannelGateway(sender).SendAsync(new CommunicationSendRequest());
		result.Status.ShouldBe(CommunicationDeliveryStatus.Failed);
		result.ErrorMessage.ShouldNotBeNull().ShouldNotContain("sensitive");
		result.ErrorCode.ShouldBe("SMTP_SEND_UNCONFIRMED");
	}

	[Fact]
	public async Task Explicit_smtp_rejection_is_a_known_failure_while_timeout_is_unconfirmed()
	{
		var sender = Substitute.For<IEmailSender>();
		sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), false)
			.ThrowsAsync(new SmtpException(SmtpStatusCode.MailboxUnavailable, "sensitive rejection detail"));
		var result = await new SmtpCommunicationChannelGateway(sender).SendAsync(new CommunicationSendRequest());
		result.Status.ShouldBe(CommunicationDeliveryStatus.Failed);
		result.ErrorCode.ShouldBe("SMTP_SEND_FAILED");
		result.ErrorMessage.ShouldNotBeNull().ShouldNotContain("sensitive");
	}

	[Fact]
	public void Campaign_status_distinguishes_sent_emails_from_queued_or_failed_attempts()
	{
		CommunicationCampaignDeliveryStatusResolver.Resolve([CommunicationDeliveryStatus.Sent]).ShouldBe(CommunicationCampaignStatus.Sent);
		CommunicationCampaignDeliveryStatusResolver.Resolve([CommunicationDeliveryStatus.Queued]).ShouldBe(CommunicationCampaignStatus.Queued);
		CommunicationCampaignDeliveryStatusResolver.Resolve([CommunicationDeliveryStatus.Failed]).ShouldBe(CommunicationCampaignStatus.Failed);
		CommunicationCampaignDeliveryStatusResolver.Resolve([CommunicationDeliveryStatus.Sent, CommunicationDeliveryStatus.Failed]).ShouldBe(CommunicationCampaignStatus.PartiallyFailed);
	}
}
