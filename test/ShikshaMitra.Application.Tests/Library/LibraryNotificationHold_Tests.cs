using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	[Fact]
	public async Task Notification_Hold_Recipient_Should_Use_The_Reviewed_Canonical_Person_And_Never_The_Preserved_Raw_Member_Id()
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var correctedPerson = await SeedOutsideRecipientAsync(context);
		var hold = await SeedNotificationHoldAsync(context, spare: true, memberId: correctedPerson, reviewed: true);
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 17)));
		var deliveries = rows.Where(row => row.RelatedEntityId == hold.RequestId && row.EventType == LibraryNotificationEventTypes.ReservedBookAvailable &&
			(row.Status == LibraryNotificationDeliveryStatuses.Queued || row.Status == LibraryNotificationDeliveryStatuses.Sent)).ToList();
		deliveries.ShouldNotContain(row => row.RecipientMemberId == context.StudentId);
		deliveries.ShouldContain(row => row.RecipientMemberId == correctedPerson);
	}

	[Fact]
	public async Task Notification_Hold_Availability_Should_Not_Promise_An_Unallocated_Spare_Copy_To_A_Waiting_Request()
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var hold = await SeedNotificationHoldAsync(context, ready: false, copyStatus: BookCopyStatus.Available);
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 17)));
		rows.ShouldNotContain(row => row.RelatedEntityId == hold.RequestId && row.EventType == LibraryNotificationEventTypes.ReservedBookAvailable);
	}

	[Fact]
	public async Task Notification_Hold_Availability_Should_Use_The_Actual_Ready_Reserved_Copy_Without_Any_Spare_Copy()
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var hold = await SeedNotificationHoldAsync(context);
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 17)));
		rows.ShouldContain(row => row.RelatedEntityId == hold.RequestId && row.EventType == LibraryNotificationEventTypes.ReservedBookAvailable &&
			(row.Status == LibraryNotificationDeliveryStatuses.Queued || row.Status == LibraryNotificationDeliveryStatuses.Sent));
	}

	[Fact]
	public async Task Notification_Hold_Expiry_Should_Use_Pickup_Deadline_Instead_Of_The_Requests_Later_Expiry()
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var hold = await SeedNotificationHoldAsync(context);
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 17)));
		rows.ShouldContain(row => row.RelatedEntityId == hold.RequestId && row.EventType == LibraryNotificationEventTypes.ReservationExpiring && row.Message.Contains("2026-11-18"));
	}

	[Theory]
	[InlineData(BookCopyStatus.Damaged, false)]
	[InlineData(BookCopyStatus.Reserved, true)]
	public async Task Notification_Hold_Availability_Should_Block_An_Uncollectable_Assigned_Copy_Despite_Other_Free_Stock(BookCopyStatus status, bool openLoan)
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var hold = await SeedNotificationHoldAsync(context, spare: true, copyStatus: status, openLoan: openLoan);
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 17)));
		rows.ShouldNotContain(row => row.RelatedEntityId == hold.RequestId && row.EventType == LibraryNotificationEventTypes.ReservedBookAvailable);
	}

	[Fact]
	public async Task Notification_Hold_State_Fingerprint_Should_Invalidate_A_Cancelled_Request_Even_When_Free_Stock_Remains()
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var hold = await SeedNotificationHoldAsync(context, spare: true);
		var intent = ReservationIntent(context, hold); var eligibility = Services().Get<LibraryNotificationReminderEligibility>();
		(await WithUnitOfWorkAsync(() => eligibility.CaptureAsync(intent))).ShouldBeTrue();
		await WithUnitOfWorkAsync(async () => {
			var requests = GetRequiredService<IRepository<BookReservation, Guid>>(); var request = await requests.GetAsync(hold.RequestId);
			request.Finish(LibraryReservationState.Cancelled, "Member cancelled", DateTimeOffset.UtcNow); await requests.UpdateAsync(request, true);
		});
		(await WithUnitOfWorkAsync(() => eligibility.IsCurrentAsync(intent))).ShouldBeFalse();
	}

	[Fact]
	public async Task Notification_Hold_State_Fingerprint_Should_Invalidate_A_Reallocated_Hold_And_Copy()
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var hold = await SeedNotificationHoldAsync(context, spare: true);
		var intent = ReservationIntent(context, hold); var eligibility = Services().Get<LibraryNotificationReminderEligibility>();
		(await WithUnitOfWorkAsync(() => eligibility.CaptureAsync(intent))).ShouldBeTrue();
		await WithUnitOfWorkAsync(async () => {
			var holds = GetRequiredService<IRepository<LibraryHold, Guid>>(); var old = await holds.GetAsync(hold.HoldId!.Value);
			old.Finish(LibraryHoldState.Released, "Reallocated after review", DateTimeOffset.UtcNow); await holds.UpdateAsync(old, true);
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>(); var original = await copies.GetAsync(hold.CopyId);
			original.Status = BookCopyStatus.Available; await copies.UpdateAsync(original, true);
			var next = await copies.GetAsync(hold.SpareId!.Value); next.Status = BookCopyStatus.Reserved; await copies.UpdateAsync(next, true);
			await holds.InsertAsync(new LibraryHold(Guid.NewGuid(), context.TenantId, hold.RequestId, hold.BookId, next.Id,
				DateTimeOffset.UtcNow, new DateOnly(2026, 11, 18), LibraryCivilDateProvenance.ExplicitSchoolDate, "Asia/Kolkata", "{}", "{}"), true);
		});
		(await WithUnitOfWorkAsync(() => eligibility.IsCurrentAsync(intent))).ShouldBeFalse();
	}

	[Fact]
	public async Task Notification_Hold_Allocation_Sink_Should_Queue_With_A_Named_System_Origin_Outside_The_Triggering_Actors_Member_Scope()
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var outside = await SeedOutsideRecipientAsync(context); var hold = await SeedNotificationHoldAsync(context, memberId: outside);
		using var actor = Principal(context.ActorId); await LimitRecipientActorToStageAsync(context);
		await WithUnitOfWorkAsync(async () => {
			var settings = GetRequiredService<ISettingManager>();
			await settings.SetForTenantAsync(context.TenantId, LibraryPolicySettingNames.CalendarTimeZone, "Asia/Kolkata", forceToSet: true);
			await settings.SetForTenantAsync(context.TenantId, LibraryNotificationSettingNames.AutomaticReminderTimeLocal, "00:00", forceToSet: true);
		});
		var services = Services(new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc));
		await WithUnitOfWorkAsync(async () => {
			var setup = await services.Get<LibraryNotificationPreferences>().GetSetupAsync();
			setup.NeedsConfiguration.ShouldBeFalse(string.Join(" ", setup.ConfigurationIssues));
			var request = await GetRequiredService<IRepository<BookReservation, Guid>>().GetAsync(hold.RequestId);
			var allocation = await GetRequiredService<IRepository<LibraryHold, Guid>>().GetAsync(hold.HoldId!.Value);
			var book = await GetRequiredService<IRepository<Book, Guid>>().GetAsync(hold.BookId);
			var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(hold.CopyId);
			await services.Get<LibraryReservationNotificationSink>().NotifyAllocatedAsync(request, allocation, book, copy);
		});
		var rows = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().GetListAsync(row => row.RelatedEntityId == hold.RequestId && row.Channel == LibraryNotificationChannels.Email));
		rows.Count.ShouldBe(1, "The committed allocation must create a real email queue record.");
		var row = rows.Single(); row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Queued);
		row.RecipientMemberId.ShouldBe(outside); row.ExtraProperties["DispatchSystemReminder"].ShouldBe(true);
		row.ExtraProperties["DispatchAction"].ShouldBe(nameof(LibrarySystemPurpose.HoldAllocator));
		row.ExtraProperties["DispatchActorUserId"].ShouldBeNull();
	}

	[Fact]
	public async Task Notification_Hold_Allocation_Sink_Should_Record_A_Linked_Skipped_Audit_When_Library_Delivery_Configuration_Is_Missing()
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var hold = await SeedNotificationHoldAsync(context);
		await WithUnitOfWorkAsync(async () => {
			var request = await GetRequiredService<IRepository<BookReservation, Guid>>().GetAsync(hold.RequestId);
			var allocation = await GetRequiredService<IRepository<LibraryHold, Guid>>().GetAsync(hold.HoldId!.Value);
			var book = await GetRequiredService<IRepository<Book, Guid>>().GetAsync(hold.BookId);
			var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(hold.CopyId);
			await Services(new DateTime(2026, 11, 17, 3, 0, 0, DateTimeKind.Utc)).Get<LibraryReservationNotificationSink>().NotifyAllocatedAsync(request, allocation, book, copy);
		});
		var rows = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().GetListAsync(row => row.RelatedEntityId == hold.RequestId));
		rows.ShouldNotContain(row => row.Status == LibraryNotificationDeliveryStatuses.Queued || row.Status == LibraryNotificationDeliveryStatuses.Sent);
		rows.ShouldContain(row => row.Status == LibraryNotificationDeliveryStatuses.Skipped && row.ErrorMessage != null && row.ErrorMessage.Contains("Library timezone"));
	}
}
