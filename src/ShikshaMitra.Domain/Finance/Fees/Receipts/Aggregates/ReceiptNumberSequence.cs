using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Tracks the next available sequence value for receipt number generation.
/// One row per (TenantId, Year, Kind) combination.
/// <para>
/// Internally stores <c>Guid.Empty</c> for "host tenant" and <c>-1</c> for
/// "no kind" (legacy REC- sequence) to avoid PostgreSQL NULL-uniqueness pitfalls
/// in the unique index.  The public properties expose the nullable domain values.
/// </para>
/// <para>
/// The sequence is incremented atomically by the repository using a single
/// SQL <c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c> statement which is
/// both idempotent and concurrent-safe on PostgreSQL.
/// </para>
/// </summary>
public class ReceiptNumberSequence : AggregateRoot<Guid>
{
	// ── Internal storage columns (non-nullable in DB) ───────────────────────
	// Public for EF Core configuration access across assemblies.

	/// <summary>Stored tenant key. <c>Guid.Empty</c> = host (no tenant).</summary>
	public Guid StoredTenantId { get; private set; }

	/// <summary>Calendar year (e.g. 2026).</summary>
	public int Year { get; private set; }

	/// <summary>Stored kind key. <c>-1</c> = legacy REC- sequence (no kind).</summary>
	public int StoredKind { get; private set; }

	/// <summary>Next sequence value (incremented atomically by the repository).</summary>
	public long NextValue { get; internal set; }

	// ── Domain-friendly accessors ────────────────────────────────────────────

	/// <summary>Null means "host tenant" (no tenant).</summary>
	public Guid? TenantId =>
		StoredTenantId == Guid.Empty ? null : StoredTenantId;

	/// <summary>Null means the legacy <c>REC-</c> sequence.</summary>
	public FeeReceiptKind? Kind =>
		StoredKind == LegacyKindSentinel ? null : (FeeReceiptKind)StoredKind;

	// ── Sentinel constants ───────────────────────────────────────────────────
	public const int LegacyKindSentinel = -1;
	public static readonly Guid HostTenantSentinel = Guid.Empty;

	// ── Factory / ORM constructors ───────────────────────────────────────────

	private ReceiptNumberSequence() { /* ORM */ }

	/// <summary>Creates a new sequence row for the given tenant/year/kind.</summary>
	public static ReceiptNumberSequence New(Guid id, Guid? tenantId, int year, FeeReceiptKind? kind) =>
		new()
		{
			Id = id,
			StoredTenantId = tenantId ?? HostTenantSentinel,
			Year = year,
			StoredKind = kind.HasValue ? (int)kind.Value : LegacyKindSentinel,
			NextValue = 1,
		};

	/// <summary>
	/// Creates a new sequence row with an arbitrary raw kind value. Use this for
	/// non-receipt sequences (e.g. adjustment vouchers) that share this table but
	/// must not collide with <see cref="FeeReceiptKind"/> values.
	/// </summary>
	public static ReceiptNumberSequence NewWithStoredKind(Guid id, Guid? tenantId, int year, int storedKind) =>
		new()
		{
			Id = id,
			StoredTenantId = tenantId ?? HostTenantSentinel,
			Year = year,
			StoredKind = storedKind,
			NextValue = 1,
		};

	/// <summary>Increments the sequence value.</summary>
	public void Increment()
	{
		NextValue++;
	}
}
