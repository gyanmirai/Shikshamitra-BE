using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Custody;

public sealed class LibraryCustodyHandoff : BasicAggregateRoot<Guid>, IMultiTenant
{
	public const int MaximumCopies = 100;
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public string Custodian { get; private set; } = string.Empty;
	public Guid? StandardId { get; private set; }
	public string? ClassName { get; private set; }
	public Guid? RoomId { get; private set; }
	public string? RoomName { get; private set; }
	public string Purpose { get; private set; } = string.Empty;
	public DateOnly HandedOffOn { get; private set; }
	public DateOnly? ExpectedReturnOn { get; private set; }
	public Guid ActorId { get; private set; }
	public DateTime RecordedAt { get; private set; }
	public int CopyCount { get; private set; }
	public string IdempotencyKey { get; private set; } = string.Empty;
	public string PayloadFingerprint { get; private set; } = string.Empty;

	private LibraryCustodyHandoff() { }

	public LibraryCustodyHandoff(Guid id, Guid tenant, Guid academicYearId, Guid employeeId, string custodian,
		Guid? standardId, string? className, Guid? roomId, string? roomName, string purpose, DateOnly handedOffOn,
		DateOnly? expectedReturnOn, Guid actorId, DateTime recordedAt, int copyCount, string key, string fingerprint) : base(id)
	{
		TenantId = tenant;
		AcademicYearId = academicYearId;
		EmployeeId = employeeId;
		Custodian = custodian;
		StandardId = standardId;
		ClassName = className;
		RoomId = roomId;
		RoomName = roomName;
		Purpose = purpose;
		HandedOffOn = handedOffOn;
		ExpectedReturnOn = expectedReturnOn;
		ActorId = actorId;
		RecordedAt = recordedAt;
		CopyCount = copyCount;
		IdempotencyKey = key;
		PayloadFingerprint = fingerprint;
	}
}
