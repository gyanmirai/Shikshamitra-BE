using System;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.CampusTransfers;

public sealed class LibraryCampusTransferItem : BasicAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid TransferId { get; private set; }
	public Guid CopyId { get; private set; }
	public Guid BookId { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public string AccessionNumber { get; private set; } = string.Empty;
	public Guid SourceShelfId { get; private set; }
	public Guid DispatchEventId { get; private set; }
	public Guid? ReceiptId { get; private set; }
	public Guid? ReceiptEventId { get; private set; }
	public BookCopyStatus? Condition { get; private set; }

	private LibraryCampusTransferItem() { }

	public LibraryCampusTransferItem(Guid id, Guid tenant, Guid transfer, BookCopy copy, string title, Guid dispatchEvent) : base(id)
	{
		TenantId = tenant;
		TransferId = transfer;
		CopyId = copy.Id;
		BookId = copy.BookId;
		Title = title;
		AccessionNumber = copy.AccessionNumber;
		SourceShelfId = copy.ShelfId;
		DispatchEventId = dispatchEvent;
	}

	public void Receive(Guid receipt, Guid copyEvent, BookCopyStatus condition)
	{
		if (ReceiptId.HasValue || receipt == Guid.Empty || copyEvent == Guid.Empty || !CanReceiveCondition(condition))
			throw new BusinessException(LibraryErrorCodes.CampusTransferInvalid);
		ReceiptId = receipt;
		ReceiptEventId = copyEvent;
		Condition = condition;
	}

	public static bool CanReceiveCondition(BookCopyStatus condition) =>
		condition is BookCopyStatus.Available or BookCopyStatus.Damaged or BookCopyStatus.UnderRepair;
}
