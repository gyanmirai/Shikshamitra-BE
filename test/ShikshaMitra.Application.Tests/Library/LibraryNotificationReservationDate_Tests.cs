using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	[Fact]
	public async Task Notification_Reservation_Reminders_Should_Not_Interpret_Unconfirmed_Legacy_Timestamps_As_School_Dates()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var reservationId = await SeedNotificationReservationAsync(context, confirmed: false);
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 17)));
		rows.ShouldNotContain(row => row.RelatedEntityId == reservationId &&
			(row.Status == LibraryNotificationDeliveryStatuses.Queued || row.Status == LibraryNotificationDeliveryStatuses.Sent));
	}

	[Fact]
	public async Task Notification_Reservation_Expiry_Should_Use_Confirmed_School_Dates_Instead_Of_Conflicting_Raw_Timestamps()
	{
		var context = await SeedAsync();
		using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var reservationId = await SeedNotificationReservationAsync(context, confirmed: true);
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 17)));
		rows.ShouldContain(row => row.RelatedEntityId == reservationId && row.EventType == LibraryNotificationEventTypes.ReservationExpiring &&
			(row.Status == LibraryNotificationDeliveryStatuses.Queued || row.Status == LibraryNotificationDeliveryStatuses.Sent) && row.Message.Contains("2026-11-18"));
	}

	private Task<Guid> SeedNotificationReservationAsync(Context context, bool confirmed) => WithUnitOfWorkAsync(async () => {
		var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.CopyId);
		var student = await GetRequiredService<IRepository<Student, Guid>>().GetAsync(context.StudentId);
		var id = Guid.NewGuid();
		var reservation = confirmed
			? BookReservation.Create(id, context.TenantId, copy.BookId, context.StudentId, context.MemberId, 0,
				student.AcademicYearId!.Value, 1, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 18), "{}",
				new DateTime(2026, 10, 1), new DateTime(2026, 10, 2))
			: new BookReservation(id, copy.BookId, context.StudentId, new DateTime(2026, 11, 16), new DateTime(2026, 11, 18), context.TenantId);
		await GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(reservation, true);
		return id;
	});
}
