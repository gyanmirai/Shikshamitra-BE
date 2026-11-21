using System;
using System.Text.Json.Serialization;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockVerificationResult : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid SessionId { get; private set; }
	public Guid CommandId { get; private set; }
	public LibraryStockReconciliationSource Source { get; private set; }
	public Guid RowId { get; private set; }
	public int Position { get; private set; }
	public string AccessionNumber { get; private set; } = string.Empty;
	public LibraryStockFinding Finding { get; private set; }
	public BookCopyStatus? BaselineStatus { get; private set; }
	public BookCopyStatus? CurrentStatus { get; private set; }
	public string? ExpectedLocation { get; private set; }
	public string? ObservedLocation { get; private set; }
	public int? ObservationSequence { get; private set; }
	public string EvidenceRevision { get; private set; } = string.Empty;
	public Guid? DecisionId { get; private set; }
	public bool ReceivedAfterOpening { get; private set; }

	private LibraryStockVerificationResult() { }

	public LibraryStockVerificationResult(Guid id, LibraryStockVerificationSession session, Guid commandId,
		LibraryStockReconciliationSource source, int position, LibraryStockPublishedEvidence evidence) : base(id)
	{
		if (session.Status != LibraryStockVerificationStatus.Finalized)
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidState);
		if (!session.TenantId.HasValue || id == Guid.Empty || commandId == Guid.Empty || evidence.RowId == Guid.Empty || position < 1 ||
			!Enum.IsDefined(source) || !Enum.IsDefined(evidence.Finding) || evidence.AccessionNumber.Length > BookCopy.MaximumAccessionLength ||
			!LibraryStockVerificationCommand.IsFingerprint(evidence.EvidenceRevision) || evidence.DecisionId == Guid.Empty ||
			evidence.DecisionId.HasValue != LibraryStockReconciliation.RequiresReview(evidence.Finding) ||
			evidence.ReceivedAfterOpening && source != LibraryStockReconciliationSource.OutsideBaseline)
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidInput);

		TenantId = session.TenantId;
		SessionId = session.Id;
		CommandId = commandId;
		Source = source;
		Position = position;
		RowId = evidence.RowId;
		AccessionNumber = evidence.AccessionNumber;
		Finding = evidence.Finding;
		BaselineStatus = evidence.BaselineStatus;
		CurrentStatus = evidence.CurrentStatus;
		ExpectedLocation = evidence.ExpectedLocation;
		ObservedLocation = evidence.ObservedLocation;
		ObservationSequence = evidence.ObservationSequence;
		EvidenceRevision = evidence.EvidenceRevision;
		DecisionId = evidence.DecisionId;
		ReceivedAfterOpening = evidence.ReceivedAfterOpening;
	}
}

public sealed record LibraryStockPublishedEvidence(Guid RowId, string AccessionNumber, LibraryStockFinding Finding,
	BookCopyStatus? BaselineStatus, BookCopyStatus? CurrentStatus, string? ExpectedLocation, string? ObservedLocation,
	int? ObservationSequence, string EvidenceRevision, Guid? DecisionId,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ReceivedAfterOpening = false);
