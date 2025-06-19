using ShikshaMitra.Library.Reservations;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.BooksReservations;

public class CreateUpdateBooksReservationDto
{
	public Guid BookId { get; set; }
    public Guid MemberId { get; set; }
	public DateTime RequestedDate { get; set; }
	public DateTime? FulfilledDate { get; set; }
    public ReservationStatus Status { get; set; }
}
