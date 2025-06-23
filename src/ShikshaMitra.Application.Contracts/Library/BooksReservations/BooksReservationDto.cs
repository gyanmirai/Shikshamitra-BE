using ShikshaMitra.Library.Reservations;
using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.BooksReservations;

public class BooksReservationDto : EntityDto<Guid>
{
	public Guid Id { get; set; }
	public Guid BookId { get; set; }
    public Guid MemberId { get; set; }
    public DateTime RequestedDate { get; set; }
	public DateTime? FulfilledDate { get; set; }
	public ReservationStatus Status { get; set; }
}
