using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Books;

public class CreateBookReservationRequest
{
    [Required]
    public DateTime RequestedDate { get; set; }

    [Required]
    public DateTime ReserveDueDate { get; set; }

    [Required]
    public Guid MemberId { get; set; }
}
