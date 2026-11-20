using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Custody;

public sealed class LibraryCustodyReceipt : BasicAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid HandoffId { get; private set; }
	public DateOnly ReceivedOn { get; private set; }
	public string? Notes { get; private set; }
	public Guid ActorId { get; private set; }
	public DateTime RecordedAt { get; private set; }
	public int CopyCount { get; private set; }
	public string IdempotencyKey { get; private set; } = string.Empty;
	public string PayloadFingerprint { get; private set; } = string.Empty;

	private LibraryCustodyReceipt() { }

	public LibraryCustodyReceipt(Guid id, Guid tenant, Guid handoffId, DateOnly receivedOn, string? notes,
		Guid actorId, DateTime recordedAt, int copyCount, string key, string fingerprint) : base(id)
	{
		TenantId = tenant;
		HandoffId = handoffId;
		ReceivedOn = receivedOn;
		Notes = notes;
		ActorId = actorId;
		RecordedAt = recordedAt;
		CopyCount = copyCount;
		IdempotencyKey = key;
		PayloadFingerprint = fingerprint;
	}
}
