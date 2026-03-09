using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Books;

public class BookReservation : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid BookId { get; set; }

    public Guid MemberId { get; set; }

    public DateTime RequestedDate { get; set; }

    public DateTime ReserveDueDate { get; set; }

    protected BookReservation() { }

    public BookReservation(
        Guid id,
        Guid bookId,
        Guid memberId,
        DateTime requestedDate,
        DateTime reserveDueDate,
        Guid? tenantId = null)
        : base(id)
    {
        BookId = bookId;
        MemberId = memberId;
        RequestedDate = requestedDate;
        ReserveDueDate = reserveDueDate;
        TenantId = tenantId;
    }
}
