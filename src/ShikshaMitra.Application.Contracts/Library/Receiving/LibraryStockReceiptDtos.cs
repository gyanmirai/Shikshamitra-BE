using System;
using System.Collections.Generic;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryStockReceiptDto
{
	public Guid Id { get; set; }
	public DateOnly ReceivedOn { get; set; }
	public LibraryStockSource Source { get; set; }
	public string? SourceName { get; set; }
	public string? Reference { get; set; }
	public Guid RecordedBy { get; set; }
	public DateTimeOffset RecordedAt { get; set; }
	public int CopyCount { get; set; }
	public List<LibraryStockReceiptLineDto> Lines { get; set; } = [];
}

public sealed class LibraryStockReceiptLineDto
{
	public Guid Id { get; set; }
	public int LineNumber { get; set; }
	public Guid BookId { get; set; }
	public Guid SectionId { get; set; }
	public Guid ShelfId { get; set; }
	public string? CurrentBookTitle { get; set; }
	public string? CurrentSectionName { get; set; }
	public string? CurrentShelfName { get; set; }
	public string? UnitCost { get; set; }
	public string? Currency { get; set; }
	public List<LibraryReceivedCopyDto> Copies { get; set; } = [];
}

public sealed class LibraryReceivedCopyDto
{
	public Guid CopyId { get; set; }
	public string AccessionNumber { get; set; } = string.Empty;
	public int CopyNumber { get; set; }
}
