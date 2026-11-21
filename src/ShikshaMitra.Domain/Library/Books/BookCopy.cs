using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Books;

public class BookCopy : Entity<Guid>, IMultiTenant
{
    public const int MaximumAccessionLength = 50;

    public Guid? TenantId { get; private set; }

    public Guid BookId { get; set; }

    public int CopyNumber { get; set; }

    public string AccessionNumber { get; set; }

    public string NormalizedAccessionNumber { get; private set; } = null!;

    public Guid SectionId { get; set; }

    public Guid ShelfId { get; set; }

    public BookCopyStatus Status { get; set; }

    public string? Remarks { get; set; }

    protected BookCopy()
    {
        // EF materializes these required columns after this ORM constructor.
        AccessionNumber = null!;
    }

    public BookCopy(Guid id, Guid bookId, int copyNumber, string accessionNumber, Guid sectionId, Guid shelfId, Guid? tenantId = null)
        : base(id)
    {
        TenantId = tenantId;
        BookId = bookId;
        CopyNumber = copyNumber;
        AccessionNumber = accessionNumber;
        SectionId = sectionId;
        ShelfId = shelfId;
        Status = BookCopyStatus.Available;
    }
}
