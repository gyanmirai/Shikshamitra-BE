using System;
using System.Linq.Expressions;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Notifications;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryNotificationDeliveryJob_Tests
{
	[Fact]
	public async Task Accepted_Email_Is_Sent_Once_When_A_Job_Is_Replayed()
	{
		var context = new Context();
		await context.Job.ExecuteAsync(context.Args);
		await context.Job.ExecuteAsync(context.Args);
		context.Row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Sent);
		context.Row.AttemptCount.ShouldBe(1);
		await context.Sender.Received(1).SendAsync("member@example.test", "Due", "Return the book.", false);
	}

	[Fact]
	public async Task Transport_Uncertainty_Is_Safe_And_Never_Leaks_Exception_Text()
	{
		var context = new Context();
		context.Sender.SendAsync("member@example.test", "Due", "Return the book.", false)
			.Returns(Task.FromException(new InvalidOperationException("private recipient or SMTP secret")));
		await context.Job.ExecuteAsync(context.Args);
		context.Row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Unconfirmed);
		context.Row.ErrorMessage.ShouldNotBeNull().ShouldNotContain("secret");
		context.Row.NextAttemptAt.ShouldBeNull();
	}

	[Fact]
	public async Task Explicit_Temporary_SMTP_Rejection_Is_Queued_For_Retry()
	{
		var context = new Context();
		context.Sender.SendAsync("member@example.test", "Due", "Return the book.", false)
			.Returns(Task.FromException(new SmtpException(SmtpStatusCode.MailboxBusy, "private transport detail")));
		await context.Job.ExecuteAsync(context.Args);
		context.Row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Queued);
		context.Row.NextAttemptAt.ShouldNotBeNull();
		context.Row.SentAt.ShouldBeNull();
		context.Row.ErrorMessage.ShouldNotBeNull().ShouldNotContain("private");
	}

	[Fact]
	public async Task Queued_Notification_Respects_A_Disabled_School_Setting()
	{
		var context = new Context();
		context.Setting.IsActive = false;
		await context.Job.ExecuteAsync(context.Args);
		context.Row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Skipped);
		await context.Sender.DidNotReceive().SendAsync(to: Arg.Any<string>(), subject: Arg.Any<string>(),
			body: Arg.Any<string>(), isBodyHtml: false);
	}

	private sealed class Context
	{
		public LibraryNotificationHistory Row { get; }
		public LibraryNotificationSetting Setting { get; }
		public IEmailSender Sender { get; } = Substitute.For<IEmailSender>();
		public LibraryNotificationDeliveryJob Job { get; }
		public LibraryNotificationDeliveryJobArgs Args { get; }

		public Context()
		{
			var now = new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
			var tenantId = Guid.NewGuid();
			Row = new LibraryNotificationHistory(Guid.NewGuid(), tenantId, LibraryNotificationEventTypes.DueDateReminder,
				LibraryNotificationEventGroups.Transactions, "Due", LibraryNotificationChannels.Email, LibraryNotificationRecipientRoles.Member,
				null, "Member", "member@example.test", "Due", "Return the book.", LibraryNotificationDeliveryStatuses.Queued,
				null, null, null, null, null, null);
			Row.QueueForDelivery(now);
			Setting = new LibraryNotificationSetting(Guid.NewGuid(), tenantId, Row.EventType, Row.EventGroup, Row.EventName,
				Row.RecipientRole, Row.Channel, true, true, false, null, "Due", "Return the book.");
			var histories = Substitute.For<IRepository<LibraryNotificationHistory, Guid>>();
			histories.FindAsync(Row.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Row);
			var settings = Substitute.For<IRepository<LibraryNotificationSetting, Guid>>();
			settings.FindAsync(Arg.Any<Expression<Func<LibraryNotificationSetting, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Setting);
			var units = Substitute.For<IUnitOfWorkManager>();
			units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), Arg.Any<bool>()).Returns(_ => Substitute.For<IUnitOfWork>());
			var tenant = Substitute.For<ICurrentTenant>();
			tenant.Id.Returns(tenantId);
			var clock = Substitute.For<IClock>();
			clock.Now.Returns(now);
			var recipientAuthority = Substitute.For<ILibraryNotificationRecipientResolver>();
			recipientAuthority.IsCurrentDestinationAsync(Row).Returns(true);
			var sourceAuthority = Substitute.For<ILibraryNotificationAuthorityService>();
			sourceAuthority.CanDispatchAsync(Row).Returns(true);
			var eligibility = Substitute.For<ILibraryNotificationReminderEligibility>();
			eligibility.IsCurrentAsync(Row).Returns(true);
			Args = new LibraryNotificationDeliveryJobArgs { HistoryId = Row.Id, TenantId = tenantId };
			Job = new LibraryNotificationDeliveryJob(histories, settings, recipientAuthority, sourceAuthority,
				eligibility, Substitute.For<ILibraryInventoryLock>(),
				Sender, Substitute.For<IBackgroundJobManager>(), units, tenant, clock);
		}
	}
}
