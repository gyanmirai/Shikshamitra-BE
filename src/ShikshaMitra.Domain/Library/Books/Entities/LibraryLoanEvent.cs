using System;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Books;

public sealed class LibraryLoanEvent : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid LoanId { get; private set; }
	public long Sequence { get; private set; }
	public LibraryLoanEventKind EventKind { get; private set; }
	public Guid ActorId { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public string BeforeJson { get; private set; } = string.Empty;
	public string AfterJson { get; private set; } = string.Empty;
	public DateTimeOffset RecordedAt { get; private set; }
	public Guid? BeforeBorrowerScopeCaptureId { get; private set; }
	public Guid? AfterBorrowerScopeCaptureId { get; private set; }
	private LibraryLoanEvent() { }

	public LibraryLoanEvent(Guid id, Guid? tenantId, Guid loanId, long sequence, LibraryLoanEventKind kind,
		Guid actorId, string reason, string beforeJson, string afterJson, DateTimeOffset recordedAt,
		Guid? beforeBorrowerScopeCaptureId = null, Guid? afterBorrowerScopeCaptureId = null) : base(id)
	{
		if (id == Guid.Empty || loanId == Guid.Empty || actorId == Guid.Empty || sequence < 1 || !Enum.IsDefined(kind) ||
			string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500 || string.IsNullOrWhiteSpace(beforeJson) ||
			string.IsNullOrWhiteSpace(afterJson) || recordedAt == default)
			throw new BusinessException(LibraryErrorCodes.ContextInvalid);
		TenantId = tenantId; LoanId = loanId; Sequence = sequence; EventKind = kind; ActorId = actorId;
		Reason = reason.Trim(); BeforeJson = beforeJson; AfterJson = afterJson; RecordedAt = recordedAt.ToUniversalTime();
		BeforeBorrowerScopeCaptureId = beforeBorrowerScopeCaptureId; AfterBorrowerScopeCaptureId = afterBorrowerScopeCaptureId;
	}
}
