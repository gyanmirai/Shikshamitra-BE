using System;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.InventoryVerification;

public static class LibraryStockReconciliation
{
	public static LibraryStockFinding Classify(LibraryStockVerificationSession session, LibraryStockVerificationBaseline expected,
		BookCopy? current, LibraryStockVerificationObservation? observed, bool confirmedLoan, bool allocatedHold, bool conflictingLinks,
		bool confirmedCustody = false, bool confirmedTransfer = false)
	{
		if (current == null) return LibraryStockFinding.CurrentCopyUnavailable;
		var status = current.Status;
		if (current.BookId != expected.BookId || !Enum.IsDefined(status) || conflictingLinks ||
			(status == BookCopyStatus.Issued) != confirmedLoan || (status == BookCopyStatus.Reserved) != allocatedHold ||
			(status == BookCopyStatus.InCustody) != confirmedCustody || (status == BookCopyStatus.InTransit) != confirmedTransfer)
			return LibraryStockFinding.RegisterConflict;
		if (observed != null) return ClassifyObservation(expected, observed, status);

		return status switch
		{
			BookCopyStatus.Issued => LibraryStockFinding.OnLoan,
			BookCopyStatus.Reserved => LibraryStockFinding.OnHold,
			BookCopyStatus.InCustody => LibraryStockFinding.InCustody,
			BookCopyStatus.InTransit => LibraryStockFinding.InTransit,
			BookCopyStatus.UnderRepair => LibraryStockFinding.InRepair,
			BookCopyStatus.Lost => LibraryStockFinding.RecordedLost,
			BookCopyStatus.Discarded => LibraryStockFinding.Withdrawn,
			_ when (session.SectionId.HasValue && session.SectionId != current.SectionId) ||
				(session.ShelfId.HasValue && session.ShelfId != current.ShelfId) => LibraryStockFinding.MovedOutsideScope,
			_ => LibraryStockFinding.NotSeen
		};
	}

	public static bool RequiresReview(LibraryStockFinding finding) => finding is not
		(LibraryStockFinding.Observed or LibraryStockFinding.OnLoan or LibraryStockFinding.OnHold or LibraryStockFinding.InRepair or LibraryStockFinding.InCustody or
		 LibraryStockFinding.RecordedLost or LibraryStockFinding.Withdrawn or LibraryStockFinding.MovedOutsideScope or LibraryStockFinding.InTransit);

	private static LibraryStockFinding ClassifyObservation(LibraryStockVerificationBaseline expected,
		LibraryStockVerificationObservation observed, BookCopyStatus status)
	{
		if (status is BookCopyStatus.Issued or BookCopyStatus.UnderRepair or BookCopyStatus.Lost or BookCopyStatus.Discarded or BookCopyStatus.InCustody or BookCopyStatus.InTransit)
			return LibraryStockFinding.ObservedWithUnavailableStatus;
		if ((observed.ObservedSectionId.HasValue && observed.ObservedSectionId != expected.SectionId) ||
			(observed.ObservedShelfId.HasValue && observed.ObservedShelfId != expected.ShelfId))
			return LibraryStockFinding.ObservedElsewhere;
		return observed.ObservedShelfId.HasValue ? LibraryStockFinding.Observed : LibraryStockFinding.ObservedLocationUnconfirmed;
	}
}
