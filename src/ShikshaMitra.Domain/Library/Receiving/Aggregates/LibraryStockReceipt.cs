using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryStockReceipt : BasicAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public DateOnly ReceivedOn { get; private set; }
	public LibraryStockSource Source { get; private set; }
	public string? SourceName { get; private set; }
	public string? Reference { get; private set; }
	public Guid RecordedBy { get; private set; }
	public DateTimeOffset RecordedAt { get; private set; }
	public int ExpectedLineCount { get; private set; }
	public int ExpectedCopyCount { get; private set; }
	private readonly List<LibraryStockReceiptLine> _lines = [];
	public IReadOnlyList<LibraryStockReceiptLine> Lines => _lines.AsReadOnly();
	private LibraryStockReceipt() { }

	public LibraryStockReceipt(Guid id, Guid tenantId, DateOnly receivedOn, LibraryStockSource source,
		string? sourceName, string? reference, Guid recordedBy, DateTimeOffset recordedAt,
		IReadOnlyList<LibraryStockLineEvidence> lines) : base(id)
	{
		LibraryStockReceiptRules.Validate(id, tenantId, receivedOn, source, sourceName, reference, recordedBy, recordedAt, lines);
		TenantId = tenantId; ReceivedOn = receivedOn; Source = source;
		SourceName = string.IsNullOrWhiteSpace(sourceName) ? null : sourceName.Trim();
		Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
		RecordedBy = recordedBy; RecordedAt = recordedAt.ToUniversalTime();
		ExpectedLineCount = lines.Count;
		foreach (var line in lines)
		{
			_lines.Add(new LibraryStockReceiptLine(tenantId, id, _lines.Count + 1, line));
			ExpectedCopyCount += line.ExpectedCopyCount;
		}
	}
}
