using System;
using System.Collections.Generic;

namespace ShikshaMitra.Library.Receiving;

public sealed record LibraryReceivedCopyEvidence(Guid CopyId, string AccessionNumber, int CopyNumber);

public sealed record LibraryStockLineEvidence(Guid Id, Guid BookId, Guid SectionId, Guid ShelfId,
	int ExpectedCopyCount, decimal? UnitCost, string? Currency,
	IReadOnlyList<LibraryReceivedCopyEvidence> Copies);
