using System;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Books;

public sealed class LibraryLoanCommand : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ActorId { get; private set; }
	public LibraryLoanOperation Operation { get; private set; }
	public string IdempotencyKey { get; private set; } = string.Empty;
	public string PayloadFingerprint { get; private set; } = string.Empty;
	public Guid LoanId { get; private set; }
	public string ResultJson { get; private set; } = string.Empty;
	public DateTimeOffset RecordedAt { get; private set; }
	private LibraryLoanCommand() { }

	public LibraryLoanCommand(Guid id, Guid? tenantId, Guid actorId, LibraryLoanOperation operation,
		string key, string fingerprint, Guid loanId, string resultJson, DateTimeOffset recordedAt) : base(id)
	{
		if (id == Guid.Empty || actorId == Guid.Empty || loanId == Guid.Empty || !Enum.IsDefined(operation) ||
			string.IsNullOrWhiteSpace(key) || key.Length > 100 || key != key.Trim() ||
			fingerprint.Length != 64 || string.IsNullOrWhiteSpace(resultJson) || recordedAt == default)
			throw new BusinessException(LibraryErrorCodes.ContextInvalid);
		TenantId = tenantId; ActorId = actorId; Operation = operation; IdempotencyKey = key;
		PayloadFingerprint = fingerprint; LoanId = loanId; ResultJson = resultJson; RecordedAt = recordedAt.ToUniversalTime();
	}
}
