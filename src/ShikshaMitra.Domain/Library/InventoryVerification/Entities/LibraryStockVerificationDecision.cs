using System;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockVerificationDecision : Entity<Guid>, IMultiTenant
{
	public const int MaximumReasonLength = 500;
	public Guid? TenantId { get; private set; }
	public Guid SessionId { get; private set; }
	public Guid CommandId { get; private set; }
	public LibraryStockReconciliationSource Source { get; private set; }
	public Guid RowId { get; private set; }
	public int Version { get; private set; }
	public string AccessionNumber { get; private set; } = string.Empty;
	public LibraryStockFinding Finding { get; private set; }
	public string EvidenceRevision { get; private set; } = string.Empty;
	public LibraryStockDecisionOutcome Outcome { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public Guid DecidedBy { get; private set; }
	public DateTimeOffset DecidedAt { get; private set; }

	private LibraryStockVerificationDecision() { }

	public LibraryStockVerificationDecision(Guid id, LibraryStockVerificationSession session, Guid commandId,
		LibraryStockReconciliationSource source, Guid rowId, int version, string accessionNumber, LibraryStockFinding finding,
		string evidenceRevision, LibraryStockDecisionOutcome outcome, string reason, Guid actorId, DateTimeOffset recordedAt) : base(id)
	{
		if (session.Status != LibraryStockVerificationStatus.InReview)
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidState);
		var text = reason?.Trim();
		if (id == Guid.Empty || !session.TenantId.HasValue || commandId == Guid.Empty || rowId == Guid.Empty || version < 1 ||
			actorId == Guid.Empty || !session.ReviewStartedAt.HasValue || recordedAt < session.ReviewStartedAt.Value ||
			!Enum.IsDefined(source) || !Enum.IsDefined(finding) || !LibraryStockReconciliation.RequiresReview(finding) ||
			!Enum.IsDefined(outcome) || !LibraryStockVerificationCommand.IsFingerprint(evidenceRevision) ||
			accessionNumber == null || accessionNumber.Length > BookCopy.MaximumAccessionLength ||
			string.IsNullOrWhiteSpace(text) || text.Length > MaximumReasonLength)
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidInput);

		TenantId = session.TenantId;
		SessionId = session.Id;
		CommandId = commandId;
		Source = source;
		RowId = rowId;
		Version = version;
		AccessionNumber = accessionNumber;
		Finding = finding;
		EvidenceRevision = evidenceRevision;
		Outcome = outcome;
		Reason = text;
		DecidedBy = actorId;
		DecidedAt = recordedAt.ToUniversalTime();
	}
}
