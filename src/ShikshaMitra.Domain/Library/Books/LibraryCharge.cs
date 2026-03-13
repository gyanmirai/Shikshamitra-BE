using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Books;

public class LibraryCharge : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid MemberId { get; set; }

    /// <summary>
    /// 0 = Student, 1 = Employee/Teacher
    /// </summary>
    public int MemberRole { get; set; }

    public Guid ChargeCategoryId { get; set; }

    public Guid? BookIssueId { get; set; }

    public decimal Amount { get; set; }

    public DateTime ChargeDate { get; set; }

    public DateTime DueDate { get; set; }

    public ChargePaymentStatus PaymentStatus { get; set; }

    public DateTime? PaidDate { get; set; }

    public string? Remarks { get; set; }

    protected LibraryCharge() { }

    public LibraryCharge(
        Guid id,
        Guid memberId,
        int memberRole,
        Guid chargeCategoryId,
        Guid? bookIssueId,
        decimal amount,
        DateTime chargeDate,
        DateTime dueDate,
        Guid? tenantId = null)
        : base(id)
    {
        TenantId = tenantId;
        MemberId = memberId;
        MemberRole = memberRole;
        ChargeCategoryId = chargeCategoryId;
        BookIssueId = bookIssueId;
        Amount = amount;
        ChargeDate = chargeDate;
        DueDate = dueDate;
        PaymentStatus = ChargePaymentStatus.Pending;
    }
}
