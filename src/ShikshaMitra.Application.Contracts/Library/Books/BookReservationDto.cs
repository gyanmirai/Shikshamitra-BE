using System;

namespace ShikshaMitra.Library.Books;

public class BookReservationDto
{
	public Guid Id { get; set; }
	public Guid BookId { get; set; }
	public Guid MemberId { get; set; }
	public DateTime RequestedDate { get; set; }
	public DateTime ReserveDueDate { get; set; }
}
