using System;

namespace ShikshaMitra.Library.Books;

public class BookReservationDto
{
	public Guid Id { get; set; }
	public Guid BookId { get; set; }
	public Guid MemberId { get; set; }
	public DateTime? RequestedDate { get; set; }
	public DateTime? ReserveDueDate { get; set; }
	public DateOnly? SchoolRequestedDate { get; set; }
	public DateOnly? SchoolRequestExpiryDate { get; set; }
	public int State { get; set; }
	public string Revision { get; set; } = string.Empty;
	public bool RequiresReview { get; set; }
	public ShikshaMitra.Library.Reservations.LibraryHoldDto? Hold { get; set; }
}
