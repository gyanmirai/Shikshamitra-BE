using System;
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
	public async Task Transport_failure_is_recorded_without_exposing_provider_details()
	{
		var sender = Substitute.For<IEmailSender>();
		sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), false)
			.ThrowsAsync(new InvalidOperationException("sensitive provider configuration"));
		var result = await new SmtpCommunicationChannelGateway(sender).SendAsync(new CommunicationSendRequest());
		result.Status.ShouldBe(CommunicationDeliveryStatus.Failed);
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
