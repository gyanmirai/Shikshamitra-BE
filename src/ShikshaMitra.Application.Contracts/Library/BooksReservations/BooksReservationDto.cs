using ShikshaMitra.Library.Reservations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.BooksReservations;

public class BooksReservationDto : EntityDto<Guid>
{
	public Guid Id { get; set; }
	public Guid BooksAssignmentsId { get; set; }
	public DateTime RequestedDate { get; set; }
	public DateTime? FulfilledDate { get; set; }
	public ReservationStatus Status { get; set; }
}
