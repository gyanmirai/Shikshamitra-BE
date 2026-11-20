using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryStockImportBatch : BasicAggregateRoot<Guid>, IMultiTenant
{
	public const int MaximumRows = 1000;
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid SectionId { get; private set; }
	public Guid ShelfId { get; private set; }
	public Guid ImportedBy { get; private set; }
	public DateTime ImportedAt { get; private set; }
	public string IdempotencyKey { get; private set; } = string.Empty;
	public string PayloadFingerprint { get; private set; } = string.Empty;
	public int RowCount { get; private set; }
	public int NewTitleCount { get; private set; }
	public LibraryStockSource Source { get; private set; }
	public string? SourceName { get; private set; }

	private LibraryStockImportBatch() { }

	public LibraryStockImportBatch(Guid id, Guid tenantId, Guid academicYearId, Guid sectionId, Guid shelfId, Guid actorId, DateTime importedAt,
		string idempotencyKey, string fingerprint, int rowCount, int newTitleCount, LibraryStockSource source, string? sourceName) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SectionId = sectionId;
		ShelfId = shelfId;
		ImportedBy = actorId;
		ImportedAt = importedAt;
		IdempotencyKey = idempotencyKey;
		PayloadFingerprint = fingerprint;
		RowCount = rowCount;
		NewTitleCount = newTitleCount;
		Source = source;
		SourceName = sourceName;
	}
}
