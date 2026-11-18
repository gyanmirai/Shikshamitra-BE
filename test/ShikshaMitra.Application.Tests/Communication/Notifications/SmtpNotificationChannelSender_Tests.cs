using System;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.Emailing;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class SmtpNotificationChannelSender_Tests
{
	private const string Destination = "test@example.invalid";
	private readonly IEmailSender _emails = Substitute.For<IEmailSender>();
	private readonly NotificationChannelSendRequest _request = new(Guid.NewGuid(), "notification-key", Guid.NewGuid(),
		CommunicationChannelType.Email, Destination, "Homework due", "<p>Please review the task.</p>",
		"https://school.example/academic/tasks?view=family&tab=homework", "en", 1);

	[Fact]
	public async Task Uses_the_existing_smtp_sender_and_records_provider_acceptance()
	{
		var result = await new SmtpNotificationChannelSender(_emails).SendAsync(_request, default);

		await _emails.Received(1).SendAsync(Destination, "Homework due",
			"<p>Please review the task.</p><p><a href=\"https://school.example/academic/tasks?view=family&amp;tab=homework\">Open in ShikshaMitra</a></p>", true);
		result.Acceptance.ShouldBe(NotificationProviderAcceptance.Accepted);
		result.ProviderMessageId.ShouldBe($"smtp:{_request.DispatchId:N}:{_request.AttemptNumber}");
		result.ErrorCode.ShouldBeNull();
	}

	[Fact]
	public async Task Link_already_in_the_rendered_template_is_not_added_twice()
	{
		const string body = "<p><a href=\"https://school.example/academic/tasks?view=family&amp;tab=homework\">View homework</a></p>";
		await new SmtpNotificationChannelSender(_emails).SendAsync(_request with { Body = body }, default);

		await _emails.Received(1).SendAsync(Destination, "Homework due", body, true);
	}

	[Theory]
	[InlineData(SmtpStatusCode.MailboxBusy, true)]
	[InlineData(SmtpStatusCode.MailboxUnavailable, false)]
	public async Task Explicit_smtp_replies_are_rejected_with_safe_retry_status(SmtpStatusCode status, bool transient)
	{
		_emails.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), true)
			.ThrowsAsync(new SmtpException(status, "private provider response"));

		var result = await new SmtpNotificationChannelSender(_emails).SendAsync(_request, default);

		result.Acceptance.ShouldBe(NotificationProviderAcceptance.Rejected);
		result.IsTransient.ShouldBe(transient);
		result.ProviderMessageId.ShouldBeNull();
		result.ErrorCode.ShouldBe(transient ? "SmtpTemporaryRejection" : "SmtpPermanentRejection");
	}

	[Fact]
	public async Task A_lost_connection_after_a_send_attempt_does_not_authorize_retry()
	{
		_emails.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), true).ThrowsAsync(new TimeoutException("private address"));

		var result = await new SmtpNotificationChannelSender(_emails).SendAsync(_request, default);

		result.Acceptance.ShouldBe(NotificationProviderAcceptance.UnknownAcceptance);
		result.IsTransient.ShouldBeFalse();
		result.ProviderMessageId.ShouldBeNull();
		result.ErrorCode.ShouldBe("SmtpAcceptanceUnknown");
	}

	[Fact]
	public async Task A_missing_route_or_cancellation_before_smtp_prevents_transport()
	{
		var sender = new SmtpNotificationChannelSender(_emails);
		var result = await sender.SendAsync(_request with { ProviderSettingId = Guid.Empty }, default);
		result.Acceptance.ShouldBe(NotificationProviderAcceptance.Rejected);
		result.ErrorCode.ShouldBe(NotificationErrorCodes.ProviderUnavailable);
		await Should.ThrowAsync<OperationCanceledException>(() => sender.SendAsync(_request, new CancellationToken(true)));
		await _emails.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
	}

	[Fact]
	public async Task A_non_https_action_link_is_rejected_before_smtp()
	{
		var result = await new SmtpNotificationChannelSender(_emails).SendAsync(_request with { LinkUrl = "javascript:alert(1)" }, default);

		result.Acceptance.ShouldBe(NotificationProviderAcceptance.Rejected);
		result.ErrorCode.ShouldBe(NotificationErrorCodes.TemplateInvalid);
		await _emails.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
	}
}
