using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockVerificationCommand : Entity<Guid>, IMultiTenant
{
	public const int MaximumKeyLength = 100;

	public Guid? TenantId { get; private set; }
	public Guid SessionId { get; private set; }
	public Guid? ObservationId { get; private set; }
	public Guid ActorId { get; private set; }
	public LibraryStockVerificationCommandKind Kind { get; private set; }
	public string IdempotencyKey { get; private set; } = string.Empty;
	public string PayloadFingerprint { get; private set; } = string.Empty;
	public DateTimeOffset RecordedAt { get; private set; }

	private LibraryStockVerificationCommand() { }

	public LibraryStockVerificationCommand(Guid id, Guid tenantId, Guid sessionId, Guid? observationId, Guid actorId,
		LibraryStockVerificationCommandKind kind, string key, string fingerprint, DateTimeOffset recordedAt) : base(id)
	{
		ValidateKey(key);
		if (id == Guid.Empty || tenantId == Guid.Empty || sessionId == Guid.Empty || actorId == Guid.Empty || observationId == Guid.Empty ||
			recordedAt == default || !Enum.IsDefined(kind) || observationId.HasValue != (kind == LibraryStockVerificationCommandKind.Observe) ||
			!IsFingerprint(fingerprint)) throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidInput);

		TenantId = tenantId;
		SessionId = sessionId;
		ObservationId = observationId;
		ActorId = actorId;
		Kind = kind;
		IdempotencyKey = key;
		PayloadFingerprint = fingerprint;
		RecordedAt = recordedAt.ToUniversalTime();
	}

	public static void ValidateKey(string key)
	{
		if (string.IsNullOrWhiteSpace(key) || key.Length > MaximumKeyLength || key != key.Trim() || key.Any(char.IsControl))
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidInput);
	}

	public static bool IsFingerprint(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
