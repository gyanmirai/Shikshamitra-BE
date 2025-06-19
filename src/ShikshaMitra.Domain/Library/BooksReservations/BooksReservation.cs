using ShikshaMitra.Library.Reservations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.BooksReservations;

public class BooksReservation : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
    public Guid BookId { get; set; }
	public Guid MemberId { get; set; }
	public DateTime RequestedDate { get; set; }
	public DateTime? FulfilledDate { get; set; }
	public ReservationStatus Status { get; set; }

	private BooksReservation() { }

	public BooksReservation(
		Guid id,
		Guid? tenantId,
		Guid bookId,
		Guid memberId,
		DateTime requestedDate,
		DateTime? fulfilledDate,
		ReservationStatus status
	) : base(id)
	{
		TenantId = tenantId;
		BookId = bookId;
		MemberId = memberId;
		RequestedDate = requestedDate;
		FulfilledDate = fulfilledDate;
		Status = status;
	}
}
