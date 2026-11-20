using System;
using System.Collections.Generic;
using System.Globalization;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reservations;

namespace ShikshaMitra.Library.Notifications;

public sealed record LibraryReservationNotificationEvidence(BookReservation Request, Guid MemberId, Book Book,
	LibraryHold? Hold, BookCopy? Copy, DateOnly? Deadline, string Signature)
{
	public Dictionary<string, string?> Placeholders() => new(StringComparer.OrdinalIgnoreCase) {
		["bookTitle"] = Book.Title,
		["requestedDate"] = Format(Request.SchoolRequestedDate),
		["requestExpiryDate"] = Format(Request.SchoolRequestExpiryDate),
		["pickupDeadline"] = Format(Hold?.SchoolPickupDeadline),
		["reservationState"] = Request.State.ToString(),
		["reservationDeadlineLabel"] = Hold == null ? "Request expiry" : "Pickup deadline",
		["reservationDeadlineSummary"] = Deadline.HasValue ? $"{(Hold == null ? "Request expiry" : "Pickup deadline")}: {Format(Deadline)}." : "No deadline is recorded yet.",
		["allocationSummary"] = Copy == null ? "No copy is allocated yet." : $"Assigned copy: {Copy.AccessionNumber}.",
		["reserveDueDate"] = Format(Deadline),
		["accessionNumber"] = Copy?.AccessionNumber,
		["copyNumber"] = Copy?.CopyNumber.ToString(CultureInfo.InvariantCulture)
	};
	private static string Format(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
}
