using System;
using System.Collections.Generic;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.CampusTransfers;

public sealed record LibraryCampusDispatchInput(Guid SourceSectionId, Guid DestinationSectionId, Guid DestinationShelfId,
	DateOnly DispatchedOn, string Reason, Guid[] CopyIds);
public sealed record LibraryCampusDispatchCommand(LibraryCampusDispatchInput Dispatch, string Revision, string IdempotencyKey);
public sealed record LibraryCampusReceiveInput(DateOnly ReceivedOn, string? Notes, LibraryCampusReceiveCopy[] Copies);
public sealed record LibraryCampusReceiveCopy(Guid CopyId, BookCopyStatus Condition);
public sealed record LibraryCampusReceiveCommand(LibraryCampusReceiveInput Receipt, string Revision, string IdempotencyKey);
public sealed record LibraryCampusTransferScope(Guid CampusId, string Campus, Guid SectionId, string Section);
public sealed record LibraryCampusTransferSection(LibraryCampusTransferScope Location, List<LibraryCampusTransferShelf> Shelves);
public sealed record LibraryCampusTransferShelf(Guid Id, string Name);
public sealed record LibraryCampusTransferCopyOption(Guid CopyId, Guid BookId, string Title, string AccessionNumber,
	BookCopyStatus Status);
public sealed record LibraryCampusTransferError(Guid? CopyId, string Code, string Message);
public sealed record LibraryCampusTransferPreview(string? Revision, List<LibraryCampusTransferCopyOption> Copies,
	List<LibraryCampusTransferError> Errors);
public sealed record LibraryCampusTransferHeader(Guid Id, LibraryCampusTransferScope Source, LibraryCampusTransferScope Destination,
	LibraryCampusTransferShelf DestinationShelf, DateOnly DispatchedOn, string Reason, Guid ActorId, DateTime RecordedAt, int CopyCount);
public sealed record LibraryCampusTransferListItem(LibraryCampusTransferHeader Transfer, int PendingCount);
public sealed record LibraryCampusTransferDetail(LibraryCampusTransferHeader Transfer, int PendingCount,
	List<LibraryCampusTransferCopyDto> Copies, List<LibraryCampusTransferReceiptDto> Receipts);
public sealed record LibraryCampusTransferCopyDto(Guid CopyId, Guid BookId, string Title, string AccessionNumber,
	Guid SourceShelfId, Guid? ReceiptId, BookCopyStatus? Condition);
public sealed record LibraryCampusTransferReceiptDto(Guid Id, Guid TransferId, DateOnly ReceivedOn, string? Notes,
	Guid ActorId, DateTime RecordedAt, List<LibraryCampusTransferCopyDto> Copies);
