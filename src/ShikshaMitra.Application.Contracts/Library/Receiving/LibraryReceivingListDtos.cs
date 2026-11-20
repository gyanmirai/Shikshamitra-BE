using System;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryReceivingListInput
{
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;
	public DateOnly? ReceivedFrom { get; set; }
	public DateOnly? ReceivedTo { get; set; }
	public LibraryStockSource? Source { get; set; }
	public string? Search { get; set; }
	public Guid? CopyId { get; set; }
}

public sealed class LibraryStockReceiptSummaryDto
{
	public Guid Id { get; set; }
	public DateOnly ReceivedOn { get; set; }
	public LibraryStockSource Source { get; set; }
	public string? SourceName { get; set; }
	public string? Reference { get; set; }
	public Guid RecordedBy { get; set; }
	public DateTimeOffset RecordedAt { get; set; }
	public int LineCount { get; set; }
	public int CopyCount { get; set; }
}
