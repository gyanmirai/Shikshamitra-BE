using System;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockVerificationBaseline : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid SessionId { get; private set; }
	public Guid CopyId { get; private set; }
	public Guid BookId { get; private set; }
	public string AccessionNumber { get; private set; } = string.Empty;
	public Guid SectionId { get; private set; }
	public Guid ShelfId { get; private set; }
	public BookCopyStatus CopyStatus { get; private set; }

	private LibraryStockVerificationBaseline() { }

	public LibraryStockVerificationBaseline(Guid id, LibraryStockVerificationSession session, LibraryCopySnapshot copy) : base(id)
	{
		if (session.Status != LibraryStockVerificationStatus.Counting || session.ObservationCount != 0)
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidState);
		if (id == Guid.Empty || !session.TenantId.HasValue || copy.TenantId != session.TenantId ||
			copy.CopyId == Guid.Empty || copy.BookId == Guid.Empty || copy.AccessionNumber == null ||
			copy.AccessionNumber.Length > BookCopy.MaximumAccessionLength ||
			(session.SectionId.HasValue && session.SectionId != copy.SectionId) ||
			(session.ShelfId.HasValue && session.ShelfId != copy.ShelfId))
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidInput);
		TenantId = session.TenantId;
		SessionId = session.Id;
		CopyId = copy.CopyId;
		BookId = copy.BookId;
		AccessionNumber = copy.AccessionNumber;
		SectionId = copy.SectionId;
		ShelfId = copy.ShelfId;
		CopyStatus = copy.Status;
	}
}
