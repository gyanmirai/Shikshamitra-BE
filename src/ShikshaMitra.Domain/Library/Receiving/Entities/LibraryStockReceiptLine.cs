using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryStockReceiptLine : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReceiptId { get; private set; }
	public int LineNumber { get; private set; }
	public Guid BookId { get; private set; }
	public Guid SectionId { get; private set; }
	public Guid ShelfId { get; private set; }
	public int ExpectedCopyCount { get; private set; }
	public decimal? UnitCost { get; private set; }
	public string? Currency { get; private set; }
	private readonly List<LibraryReceivedCopy> _copies = [];
	public IReadOnlyList<LibraryReceivedCopy> Copies => _copies.AsReadOnly();
	private LibraryStockReceiptLine() { }

	internal LibraryStockReceiptLine(Guid tenantId, Guid receiptId, int lineNumber, LibraryStockLineEvidence evidence) : base(evidence.Id)
	{
		TenantId = tenantId; ReceiptId = receiptId; LineNumber = lineNumber; BookId = evidence.BookId;
		SectionId = evidence.SectionId; ShelfId = evidence.ShelfId; ExpectedCopyCount = evidence.ExpectedCopyCount;
		UnitCost = evidence.UnitCost; Currency = evidence.Currency;
		foreach (var copy in evidence.Copies) _copies.Add(new LibraryReceivedCopy(tenantId, Id, copy));
	}
}
