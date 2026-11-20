using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Library.Books;

public static class LibraryCopyConditionRules
{
	private static readonly BookCopyStatus[] PhysicalConditions =
		[BookCopyStatus.Available, BookCopyStatus.Lost, BookCopyStatus.Damaged, BookCopyStatus.UnderRepair];

	public static List<BookCopyStatus> AllowedConditions(BookCopyStatus current, bool hasOpenLoan)
	{
		if (!Enum.IsDefined(current) || current is BookCopyStatus.Discarded or BookCopyStatus.Reserved or BookCopyStatus.InCustody or BookCopyStatus.InTransit) return [];
		if (!hasOpenLoan) return PhysicalConditions.Where(status => status != current).ToList();
		if (current is BookCopyStatus.Lost or BookCopyStatus.UnderRepair) return [];
		return new[] { BookCopyStatus.Lost, BookCopyStatus.Damaged }.Where(status => status != current).ToList();
	}

	public static void EnsureConditionChange(BookCopyStatus current, BookCopyStatus target, bool hasOpenLoan)
	{
		if (!Enum.IsDefined(target) || target is BookCopyStatus.Issued or BookCopyStatus.Reserved or BookCopyStatus.Discarded or BookCopyStatus.InCustody or BookCopyStatus.InTransit)
			throw new BusinessException(LibraryErrorCodes.InvalidCopyStatusTransition, "Use the corresponding circulation or inventory action for this status.");
		if (current == BookCopyStatus.Discarded) throw WrittenOff();
		if (current == BookCopyStatus.Reserved) throw Held();
		if (current == BookCopyStatus.InCustody) throw HeldInCustody();
		if (current == BookCopyStatus.InTransit) throw InTransfer();
		if (!AllowedConditions(current, hasOpenLoan).Contains(target))
			throw hasOpenLoan ? OpenLoan() : new BusinessException(LibraryErrorCodes.InvalidCopyStatusTransition, "Choose a different valid physical condition.");
	}

	public static bool CanMove(BookCopyStatus current, bool hasOpenLoan) => !hasOpenLoan &&
		current is BookCopyStatus.Available or BookCopyStatus.Damaged or BookCopyStatus.UnderRepair;

	public static void EnsureMovable(BookCopyStatus current, bool hasOpenLoan)
	{
		if (hasOpenLoan) throw OpenLoan();
		if (current == BookCopyStatus.Reserved) throw Held();
		if (current == BookCopyStatus.Discarded) throw WrittenOff();
		if (current == BookCopyStatus.InCustody) throw HeldInCustody();
		if (current == BookCopyStatus.InTransit) throw InTransfer();
		if (!CanMove(current, false))
			throw new BusinessException(LibraryErrorCodes.InvalidCopyStatusTransition, "Recover this copy before recording a physical move.");
	}

	public static bool CanWriteOff(BookCopyStatus current, bool hasOpenLoan) => !hasOpenLoan && Enum.IsDefined(current) &&
		current is not (BookCopyStatus.Reserved or BookCopyStatus.Discarded or BookCopyStatus.InCustody or BookCopyStatus.InTransit);

	public static void EnsureCanWriteOff(BookCopyStatus current, bool hasOpenLoan)
	{
		if (hasOpenLoan) throw OpenLoan();
		if (current == BookCopyStatus.Reserved) throw Held();
		if (current == BookCopyStatus.Discarded) throw WrittenOff();
		if (current == BookCopyStatus.InCustody) throw HeldInCustody();
		if (current == BookCopyStatus.InTransit) throw InTransfer();
		if (!CanWriteOff(current, false)) throw new BusinessException(LibraryErrorCodes.InvalidCopyStatusTransition);
	}

	public static string RequireReason(string? reason)
	{
		var value = reason?.Trim();
		if (string.IsNullOrWhiteSpace(value) || value.Length > 500)
			throw new BusinessException(LibraryErrorCodes.CopyReasonRequired, "Provide a reason of at most 500 characters for this copy action.");
		return value;
	}

	private static BusinessException OpenLoan() => new(LibraryErrorCodes.CopyHasOpenLoan,
		"This copy has an open loan. Use the return or recovery workflow before making it available, moving, repairing, or writing it off.");
	private static BusinessException Held() => new(LibraryErrorCodes.CopyHasHold, "Release the copy's hold before changing its physical condition or location.");
	private static BusinessException HeldInCustody() => new(LibraryErrorCodes.CopyInCustody, "Receive the copy through classroom custody before changing its condition, location or withdrawal state.");
	private static BusinessException InTransfer() => new(LibraryErrorCodes.CopyInTransit, "Record the campus transfer receipt before changing this copy's inventory state.");
	private static BusinessException WrittenOff() => new(LibraryErrorCodes.CopyWrittenOff, "This copy is written off. Its inventory and history are retained.");
}
