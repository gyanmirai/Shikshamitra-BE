using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionReferenceSequence : AggregateRoot<Guid>
{
	public static readonly Guid HostTenantSentinel = Guid.Empty;

	public Guid StoredTenantId { get; private set; }
	public int Year { get; private set; }
	public AdmissionReferenceKind Kind { get; private set; }
	public long NextValue { get; private set; }

	private AdmissionReferenceSequence() { }

	public static AdmissionReferenceSequence Create(
		Guid id,
		Guid? tenantId,
		int year,
		AdmissionReferenceKind kind) =>
		new()
		{
			Id = id,
			StoredTenantId = tenantId ?? HostTenantSentinel,
			Year = year,
			Kind = kind,
			NextValue = 1
		};

	public long TakeNext()
	{
		var value = NextValue;
		NextValue++;
		return value;
	}
}
