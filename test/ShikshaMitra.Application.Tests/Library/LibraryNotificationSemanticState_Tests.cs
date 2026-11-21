using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryNotificationSemanticState_Tests
{
	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public async Task Equal_Business_State_Should_Stay_Current_After_Storage_Representation_Changes(bool changeScale, bool changeDateKind)
	{
		var fixture = CreateFixture(changeScale ? 50.00m : 50m,
			changeDateKind ? DateTimeKind.Utc : DateTimeKind.Unspecified);

		await fixture.Eligibility.CaptureAsync(fixture.Intent);

		(await fixture.Eligibility.IsCurrentAsync(fixture.Intent)).ShouldBeTrue();
	}

	[Fact]
	public async Task A_Real_Assessment_Amount_Change_Should_Invalidate_The_Captured_State()
	{
		var fixture = CreateFixture(51m, DateTimeKind.Unspecified);
		await fixture.Eligibility.CaptureAsync(fixture.Intent);

		(await fixture.Eligibility.IsCurrentAsync(fixture.Intent)).ShouldBeFalse();
	}

	private static Fixture CreateFixture(decimal reloadedAmount, DateTimeKind reloadedKind)
	{
		var tenantId = Guid.NewGuid();
		var memberId = Guid.NewGuid();
		var copyId = Guid.NewGuid();
		var issueId = Guid.NewGuid();
		var original = Issue(50m, DateTimeKind.Unspecified);
		var reloaded = Issue(reloadedAmount, reloadedKind);
		var issues = Substitute.For<IRepository<BookIssue, Guid>>();
		issues.FindAsync(issueId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<BookIssue?>(original), Task.FromResult<BookIssue?>(reloaded));
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(tenantId);
		var clock = Substitute.For<IClock>();
		clock.Now.Returns(new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc));
		var settings = Substitute.For<ISettingManager>();
		var calendar = new LibraryCalendarService(settings, tenant, clock,
			Substitute.For<IRepository<LibraryTiming, Guid>>(), Substitute.For<IRepository<LibraryCalendarClosure, Guid>>());
		var preferences = new LibraryNotificationPreferences(settings, tenant, calendar, clock);
		var reservationEvidence = new LibraryReservationNotificationEvidenceReader(
			Substitute.For<IRepository<BookReservation, Guid>>(), Substitute.For<IRepository<LibraryHold, Guid>>(),
			Substitute.For<IRepository<Book, Guid>>(), Substitute.For<IRepository<BookCopy, Guid>>(), issues,
			Substitute.For<IAsyncQueryableExecuter>(), tenant);
		var eligibility = new LibraryNotificationReminderEligibility(issues,
			Substitute.For<IRepository<LibraryCharge, Guid>>(),
			Substitute.For<IRepository<LibraryMember, Guid>>(), Substitute.For<IRepository<Book, Guid>>(),
			Substitute.For<IRepository<BookCopy, Guid>>(), preferences, reservationEvidence, Substitute.For<ILibraryFineReminderEvidenceReader>(), tenant);
		var intent = new LibraryNotificationHistory(Guid.NewGuid(), tenantId, LibraryNotificationEventTypes.BookIssueConfirmation,
			"Circulation", "Issue confirmation", LibraryNotificationChannels.Email, LibraryNotificationRecipientRoles.Member,
			memberId, "Library reader", "reader@example.test", "Book issued", "Your book issue is confirmed.",
			LibraryNotificationDeliveryStatuses.Queued, null, nameof(BookIssue), issueId, null, null, null);
		return new(eligibility, intent);

		BookIssue Issue(decimal amount, DateTimeKind kind) => new(issueId, copyId, "SEMANTIC-001", memberId, 0,
			new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 20, 0, 0, 0, kind), tenantId) { Amount = amount };
	}

	private sealed record Fixture(LibraryNotificationReminderEligibility Eligibility, LibraryNotificationHistory Intent);
}
