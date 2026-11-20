using System;
using System.Linq;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockVerificationObservation : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid SessionId { get; private set; }
	public int Sequence { get; private set; }
	public string AccessionNumber { get; private set; } = string.Empty;
	public Guid? CopyId { get; private set; }
	public Guid? ObservedSectionId { get; private set; }
	public Guid? ObservedShelfId { get; private set; }
	public DateOnly SchoolDate { get; private set; }
	public Guid ObservedBy { get; private set; }
	public DateTimeOffset ObservedAt { get; private set; }

	private LibraryStockVerificationObservation() { }

	public LibraryStockVerificationObservation(Guid id, LibraryStockVerificationSession session, string accessionNumber,
		LibraryCopySnapshot? resolvedCopy, Guid? observedSectionId, Guid? observedShelfId, DateOnly schoolDate,
		Guid observedBy, DateTimeOffset observedAt) : base(id)
	{
		var accession = accessionNumber?.Trim();
		if (id == Guid.Empty || !session.TenantId.HasValue || string.IsNullOrWhiteSpace(accession) ||
			accession.Length > BookCopy.MaximumAccessionLength || accession.Any(char.IsControl) || schoolDate < session.SchoolDate ||
			observedSectionId == Guid.Empty || observedShelfId == Guid.Empty || (observedShelfId.HasValue && !observedSectionId.HasValue) ||
			(resolvedCopy != null && (resolvedCopy.TenantId != session.TenantId || resolvedCopy.CopyId == Guid.Empty)))
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidInput);
		session.RecordObservation(observedBy, observedAt);
		TenantId = session.TenantId;
		SessionId = session.Id;
		Sequence = session.ObservationCount;
		AccessionNumber = accession;
		CopyId = resolvedCopy?.CopyId;
		ObservedSectionId = observedSectionId;
		ObservedShelfId = observedShelfId;
		SchoolDate = schoolDate;
		ObservedBy = observedBy;
		ObservedAt = observedAt.ToUniversalTime();
	}
}
