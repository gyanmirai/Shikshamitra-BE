using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Receiving;

public sealed class LibraryStockReceiptCommand : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ActorId { get; private set; }
	public Guid ReceiptId { get; private set; }
	public string IdempotencyKey { get; private set; } = string.Empty;
	public string PayloadFingerprint { get; private set; } = string.Empty;
	public DateTimeOffset RecordedAt { get; private set; }
	private LibraryStockReceiptCommand() { }

	public LibraryStockReceiptCommand(Guid id, Guid tenantId, Guid actorId, Guid receiptId, string key,
		string fingerprint, DateTimeOffset recordedAt) : base(id)
	{
		if (id == Guid.Empty || tenantId == Guid.Empty || actorId == Guid.Empty || receiptId == Guid.Empty || recordedAt == default ||
			string.IsNullOrWhiteSpace(key) || key.Length > 100 || key != key.Trim() || key.Any(char.IsControl) ||
			fingerprint == null || fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
			throw new BusinessException(LibraryErrorCodes.ReceivingInvalid);
		TenantId = tenantId; ActorId = actorId; ReceiptId = receiptId;
		IdempotencyKey = key; PayloadFingerprint = fingerprint; RecordedAt = recordedAt.ToUniversalTime();
	}
}
