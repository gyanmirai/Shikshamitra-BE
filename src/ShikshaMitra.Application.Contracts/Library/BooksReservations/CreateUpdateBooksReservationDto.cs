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
	[Required]
	public Guid BooksAssignmentId { get; set; }

	[Required]
	public DateTime RequestedDate { get; set; }

	public DateTime? FulfilledDate { get; set; }

	[Required]
	public ReservationStatus Status { get; set; }
}
