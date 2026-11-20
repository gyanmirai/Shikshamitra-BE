using System;
using System.Linq;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.Notifications;

internal static class LibraryNotificationSource
{
	private static readonly (string Event, string Type)[] Sources =
	[
		(LibraryNotificationEventTypes.BookIssueConfirmation, nameof(BookIssue)),
		(LibraryNotificationEventTypes.DueDateReminder, nameof(BookIssue)),
		(LibraryNotificationEventTypes.OverdueReminder, nameof(BookIssue)),
		(LibraryNotificationEventTypes.ReturnConfirmation, nameof(BookIssue)),
		(LibraryNotificationEventTypes.ReturnChargeReminder, nameof(BookIssue)),
		(LibraryNotificationEventTypes.ReservationConfirmed, nameof(BookReservation)),
		(LibraryNotificationEventTypes.ReservedBookAvailable, nameof(BookReservation)),
		(LibraryNotificationEventTypes.ReservationExpiring, nameof(BookReservation)),
		(LibraryNotificationEventTypes.FineDueReminder, nameof(LibraryCharge)),
		(LibraryNotificationEventTypes.LowStockAlert, nameof(Book))
	];

	public static bool IsConsistent(string eventType, string? type, Guid? id) =>
		id.HasValue ? Sources.Any(source => source.Event == eventType && source.Type == type) : string.IsNullOrEmpty(type);

	public static IQueryable<LibraryNotificationHistory> Apply(IQueryable<LibraryNotificationHistory> query)
	{
		var loanEvents = EventsFor(nameof(BookIssue));
		var requestEvents = EventsFor(nameof(BookReservation));
		var chargeEvents = EventsFor(nameof(LibraryCharge));
		var bookEvents = EventsFor(nameof(Book));
		return query.Where(row => !row.RelatedEntityId.HasValue && (row.RelatedEntityType == null || row.RelatedEntityType == "") ||
			row.RelatedEntityId.HasValue &&
			(row.RelatedEntityType == nameof(BookIssue) && Enumerable.Contains(loanEvents, row.EventType) ||
			 row.RelatedEntityType == nameof(BookReservation) && Enumerable.Contains(requestEvents, row.EventType) ||
			 row.RelatedEntityType == nameof(LibraryCharge) && Enumerable.Contains(chargeEvents, row.EventType) ||
			 row.RelatedEntityType == nameof(Book) && Enumerable.Contains(bookEvents, row.EventType)));
	}

	private static string[] EventsFor(string type) => Sources.Where(source => source.Type == type).Select(source => source.Event).ToArray();
}
