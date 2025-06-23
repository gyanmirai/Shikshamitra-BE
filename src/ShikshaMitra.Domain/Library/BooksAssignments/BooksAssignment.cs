using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.BooksAssignments;

public class BooksAssignment : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid BookId { get; set; }
    public Guid MemberId { get; set; }
    public string BookTitle { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }

    private BooksAssignment() { }

    public BooksAssignment(
        Guid id,
        Guid? tenantId,
        Guid bookId,
        Guid memberId,
		string bookTitle,
        DateTime issueDate,
        DateTime dueDate,
        DateTime? returnDate
    ) : base(id) 
    {
        TenantId = tenantId;
        BookId = bookId;
        MemberId = memberId;
		BookTitle = bookTitle;
        IssueDate = issueDate;
        DueDate = dueDate;
        ReturnDate = returnDate;
    }
}
