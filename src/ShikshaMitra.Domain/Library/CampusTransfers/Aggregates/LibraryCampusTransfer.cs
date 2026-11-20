using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.CampusTransfers;

public sealed record LibraryCampusTransferLocation(Guid CampusId, string Campus, Guid SectionId, string Section);

public sealed class LibraryCampusTransfer : BasicAggregateRoot<Guid>, IMultiTenant
{
	public const int MaximumCopies = 100;
	public Guid? TenantId { get; private set; }
	public Guid SourceCampusId { get; private set; }
	public string SourceCampus { get; private set; } = string.Empty;
	public Guid SourceSectionId { get; private set; }
	public string SourceSection { get; private set; } = string.Empty;
	public Guid DestinationCampusId { get; private set; }
	public string DestinationCampus { get; private set; } = string.Empty;
	public Guid DestinationSectionId { get; private set; }
	public string DestinationSection { get; private set; } = string.Empty;
	public Guid DestinationShelfId { get; private set; }
	public string DestinationShelf { get; private set; } = string.Empty;
	public DateOnly DispatchedOn { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public Guid ActorId { get; private set; }
	public DateTime RecordedAt { get; private set; }
	public int CopyCount { get; private set; }
	public string IdempotencyKey { get; private set; } = string.Empty;
	public string PayloadFingerprint { get; private set; } = string.Empty;

	private LibraryCampusTransfer() { }

	public LibraryCampusTransfer(Guid id, Guid tenant, LibraryCampusTransferLocation source, LibraryCampusTransferLocation destination,
		Guid destinationShelfId, string destinationShelf, DateOnly dispatchedOn, string reason, Guid actorId, DateTime recordedAt,
		int copyCount, string key, string fingerprint) : base(id)
	{
		TenantId = tenant;
		SourceCampusId = source.CampusId;
		SourceCampus = source.Campus;
		SourceSectionId = source.SectionId;
		SourceSection = source.Section;
		DestinationCampusId = destination.CampusId;
		DestinationCampus = destination.Campus;
		DestinationSectionId = destination.SectionId;
		DestinationSection = destination.Section;
		DestinationShelfId = destinationShelfId;
		DestinationShelf = destinationShelf;
		DispatchedOn = dispatchedOn;
		Reason = reason;
		ActorId = actorId;
		RecordedAt = recordedAt;
		CopyCount = copyCount;
		IdempotencyKey = key;
		PayloadFingerprint = fingerprint;
	}
}
