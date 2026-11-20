using System;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Custody;

public sealed class LibraryCustodyItem : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid HandoffId { get; private set; }
	public Guid CopyId { get; private set; }
	public Guid BookId { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public string AccessionNumber { get; private set; } = string.Empty;
	public Guid HomeSectionId { get; private set; }
	public Guid HomeShelfId { get; private set; }
	public Guid HandoffEventId { get; private set; }
	public Guid? ReturnReceiptId { get; private set; }
	public Guid? ReturnEventId { get; private set; }
	public BookCopyStatus? ReturnedCondition { get; private set; }

	private LibraryCustodyItem() { }

	public LibraryCustodyItem(Guid id, Guid tenant, Guid handoffId, BookCopy copy, string title, Guid handoffEventId) : base(id)
	{
		TenantId = tenant;
		HandoffId = handoffId;
		CopyId = copy.Id;
		BookId = copy.BookId;
		Title = title;
		AccessionNumber = copy.AccessionNumber;
		HomeSectionId = copy.SectionId;
		HomeShelfId = copy.ShelfId;
		HandoffEventId = handoffEventId;
	}

	public void Receive(Guid receiptId, Guid eventId, BookCopyStatus condition)
	{
		if (ReturnReceiptId.HasValue || receiptId == Guid.Empty || eventId == Guid.Empty || !IsReturnCondition(condition))
			throw new BusinessException(LibraryErrorCodes.CustodyInvalid);
		ReturnReceiptId = receiptId;
		ReturnEventId = eventId;
		ReturnedCondition = condition;
	}

	public static bool IsReturnCondition(BookCopyStatus condition) =>
		condition is BookCopyStatus.Available or BookCopyStatus.Damaged or BookCopyStatus.UnderRepair;
}
