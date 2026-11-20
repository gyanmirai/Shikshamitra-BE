using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Notifications;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Fine_Due_Reminders_Should_Use_The_Verified_Collectible_Amount_And_School_Due_Date()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		fixture.Collection.Receivable.SettledPaymentAmount.ShouldBe(40.25m);
		fixture.Collection.Receivable.OutstandingAmount.ShouldBe(60.50m);
		var reminders = GetRequiredService<LibraryNotificationReminderService>();
		var due = owner.DueDate.ToDateTime(TimeOnly.MinValue);
		var early = await WithUnitOfWorkAsync(() => reminders.ProcessDueRemindersAsync(due.AddDays(-1)));
		early.ShouldNotContain(row => row.RelatedEntityId == owner.AssessmentId);

		var dueRows = await WithUnitOfWorkAsync(() => reminders.ProcessDueRemindersAsync(due));

		var email = dueRows.Where(row => row.RelatedEntityId == owner.AssessmentId &&
			row.EventType == LibraryNotificationEventTypes.FineDueReminder && row.Channel == LibraryNotificationChannels.Email).ShouldHaveSingleItem();
		email.RecipientMemberId.ShouldBe(owner.PersonId);
		email.Message.ShouldContain("60.50");
		email.Message.ShouldContain("INR");
		email.Message.ShouldNotContain("100.75");
		email.ReminderDate.ShouldBe(due);
		email.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Queued);
		var eligibility = GetRequiredService<ILibraryNotificationReminderEligibility>();
		(await WithUnitOfWorkAsync(() => eligibility.IsCurrentAsync(email))).ShouldBeTrue();
		var evidenceReader = GetRequiredService<ILibraryFineReminderEvidenceReader>();
		var evidence = (await WithUnitOfWorkAsync(() => evidenceReader.ReadAsync([owner.AssessmentId], owner.DueDate)))[owner.AssessmentId];
		(await WithUnitOfWorkAsync(() => eligibility.CaptureAsync(email, evidence with { Collectible = 100.75m }))).ShouldBeFalse();

		using (SignIn(owner))
		{
			var context = fixture.Settlement;
			var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
			var remainder = CashCollectionInput(context, form.Revision);
			remainder.Payments.Single().Amount = 60.50m;
			var paid = await WithUnitOfWorkAsync(() => TypedService(owner).CollectAsync(remainder));
			paid.Receivable.OutstandingAmount.ShouldBe(0m);
		}
		(await WithUnitOfWorkAsync(() => eligibility.IsCurrentAsync(email))).ShouldBeFalse();
		var sender = Substitute.For<IEmailSender>();
		var worker = ActivatorUtilities.CreateInstance<LibraryNotificationDeliveryJob>(ServiceProvider, sender);
		await worker.ExecuteAsync(new() { TenantId = owner.TenantId, HistoryId = email.Id });
		await sender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), false);
		var persistedEmail = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().GetAsync(email.Id));
		persistedEmail.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Skipped);
		var settled = await WithUnitOfWorkAsync(() => reminders.ProcessDueRemindersAsync(due.AddDays(1)));
		settled.ShouldNotContain(row => row.RelatedEntityId == owner.AssessmentId);
	}

	[Fact]
	public async Task Fine_Due_Reminders_Should_Not_Demand_An_Unposted_Assessment()
	{
		var owner = await SeedAsync(FinanceExternalPayerKind.Employee, departed: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		var reminders = GetRequiredService<LibraryNotificationReminderService>();

		var rows = await WithUnitOfWorkAsync(() => reminders.ProcessDueRemindersAsync(owner.DueDate.ToDateTime(TimeOnly.MinValue)));

		rows.ShouldNotContain(row => row.EventType == LibraryNotificationEventTypes.FineDueReminder && row.RelatedEntityId == owner.AssessmentId);
	}

	[Fact]
	public async Task Fine_Due_Reminders_Should_Keep_A_Verified_Obligation_In_An_Inactive_Members_Inbox()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee, prepareSource: owner => WithUnitOfWorkAsync(async () =>
		{
			var employees = GetRequiredService<IRepository<Employee, Guid>>();
			var employee = await employees.GetAsync(owner.PersonId);
			employee.EmployeeStatusType.ShouldBe(EmployeeStatusType.Inactive);
			employee.UpdateUserId(owner.ActorId);
			await employees.UpdateAsync(employee, true);
		}));
		var member = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(member.TenantId);
		var reminders = GetRequiredService<LibraryNotificationReminderService>();
		var rows = await WithUnitOfWorkAsync(() => reminders.ProcessDueRemindersAsync(member.DueDate.ToDateTime(TimeOnly.MinValue)));
		var delivered = rows.Where(row => row.RelatedEntityId == member.AssessmentId && row.Channel == LibraryNotificationChannels.InApp).ShouldHaveSingleItem();
		delivered.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Sent);
		using var actor = SignIn(member);
		var inbox = GetRequiredService<ILibraryInboxAppService>();
		var visible = await inbox.GetAsync(new());
		visible.Items.ShouldContain(row => row.Id == delivered.Id);
	}
}
