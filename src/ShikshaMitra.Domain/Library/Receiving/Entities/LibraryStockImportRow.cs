using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryStockImportRow : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid BatchId { get; private set; }
	public int RowNumber { get; private set; }
	public string SourceRowId { get; private set; } = string.Empty;
	public string NormalizedSourceRowId { get; private set; } = string.Empty;
	public Guid BookId { get; private set; }
	public string Title { get; private set; } = string.Empty;
	public bool IsNewTitle { get; private set; }
	public Guid CopyId { get; private set; }
	public Guid ReceiptId { get; private set; }
	public string AccessionNumber { get; private set; } = string.Empty;

	private LibraryStockImportRow() { }

	public LibraryStockImportRow(Guid id, Guid tenantId, Guid batchId, int rowNumber, string sourceRowId, Guid bookId,
		string title, bool isNewTitle, Guid copyId, Guid receiptId, string accession) : base(id)
	{
		TenantId = tenantId;
		BatchId = batchId;
		RowNumber = rowNumber;
		SourceRowId = sourceRowId;
		NormalizedSourceRowId = NormalizeSourceRowId(sourceRowId);
		BookId = bookId;
		Title = title;
		IsNewTitle = isNewTitle;
		CopyId = copyId;
		ReceiptId = receiptId;
		AccessionNumber = accession;
	}

	public static string NormalizeSourceRowId(string sourceRowId) => sourceRowId.Trim().ToUpperInvariant();
}
