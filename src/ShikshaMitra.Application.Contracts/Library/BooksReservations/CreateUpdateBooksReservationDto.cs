using ShikshaMitra.Library.Reservations;
using System;

namespace ShikshaMitra.Library.BooksReservations;

public class CreateUpdateBooksReservationDto
{
	public Guid BookId { get; set; }
    public Guid MemberId { get; set; }
	public DateTime RequestedDate { get; set; }
	public DateTime? FulfilledDate { get; set; }
    public ReservationStatus Status { get; set; }
}
