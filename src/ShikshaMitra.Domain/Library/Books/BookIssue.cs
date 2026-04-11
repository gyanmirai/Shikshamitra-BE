using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Books;

public class BookIssue : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid BookCopyId { get; set; }

    public string AccessionNumber { get; set; }

    public Guid MemberId { get; set; }

    /// <summary>
    /// 0 = Student, 1 = Employee/Teacher
    /// </summary>
    public int MemberRole { get; set; }

    public DateTime IssueDate { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    public BookIssueStatus Status { get; set; }

    public Guid? ChargeCategoryId { get; set; }

    public decimal? Amount { get; set; }

    public DateTime? FineDueDate { get; set; }

    public string? Remarks { get; set; }

    protected BookIssue() { }

    public BookIssue(
        Guid id,
        Guid bookCopyId,
        string accessionNumber,
        Guid memberId,
        int memberRole,
        DateTime issueDate,
        DateTime dueDate,
        Guid? tenantId = null)
        : base(id)
    {
        BookCopyId = bookCopyId;
        AccessionNumber = accessionNumber;
        MemberId = memberId;
        MemberRole = memberRole;
        IssueDate = issueDate;
        DueDate = dueDate;
        Status = BookIssueStatus.Issued;
        TenantId = tenantId;
    }
}
