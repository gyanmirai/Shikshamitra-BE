using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Library.Notifications;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Migrated_Postgres_Fine_Due_Reminder_Should_Use_Verified_Partial_Cash_With_Notification_Authority()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.SourceFingerprintEvidence);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var fixture = await SeedFinancePgAsync(factory);
		var prepared = await PrepareFinancePgAsync(factory, database, fixture);
		using var maker = FinancePgClient(factory, FinancePgMakerId, FinancePgMakerRole);
		var form = await FinancePgGetAsync<FinanceExternalCollectionFormDto>(maker,
			FinancePgRoute + "/collect-form" + FinancePgSourceQuery(fixture.Source));
		var collected = await PostAsync<FinanceExternalCollectionDto>(maker, FinancePgRoute + "/collections", new FinanceExternalCollectInput
		{
			Source = fixture.Source, Revision = form.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"),
			SchoolReceiptDate = new(2026, 11, 17), Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 40.25m }]
		});
		collected.Receivable.OutstandingAmount.ShouldBe(60.50m);

		maker.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);
		maker.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, new[]
		{
			ShikshaMitraSubmodulePermissions.Library.Notification.View,
			ShikshaMitraSubmodulePermissions.Library.Notification.Add
		});
		const string route = "/api/library/notifications/run-reminders";
		var request = new LibraryNotificationRunRemindersRequest { RunDate = new DateTime(2026, 11, 23) };
		var early = await PostAsync<LibraryNotificationRunRemindersResultDto>(maker, route, request);
		early.Items.ShouldNotContain(row => row.RelatedEntityId == fixture.Source.SourceId);
		request.RunDate = new DateTime(2026, 11, 24);
		var due = await PostAsync<LibraryNotificationRunRemindersResultDto>(maker, route, request);
		var email = due.Items.Where(row => row.RelatedEntityId == fixture.Source.SourceId &&
			row.EventType == LibraryNotificationEventTypes.FineDueReminder && row.Channel == LibraryNotificationChannels.Email).ShouldHaveSingleItem();
		email.RecipientMemberId.ShouldBe(fixture.EmployeeId);
		email.Message.ShouldContain("INR 60.50");
		email.Message.ShouldContain("2026-11-24");
		var repeated = await PostAsync<LibraryNotificationRunRemindersResultDto>(maker, route, request);
		repeated.Items.ShouldNotContain(row => row.RelatedEntityId == fixture.Source.SourceId);
		(await FinancePgRowAsync(database, "Finance_ExternalReceivableSourceLinks", prepared.LinkId)).ShouldBe(prepared.OriginalLinkJson);
	}
}
