using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

// Shared by every admission track for the same class in an admission cycle.
public class AdmissionClassRoomAllocation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid RoomId { get; private set; }

	private AdmissionClassRoomAllocation() { }

	public AdmissionClassRoomAllocation(Guid id, Guid? tenantId, Guid academicYearId, Guid standardId, Guid roomId)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = Check.NotDefaultOrNull<Guid>(academicYearId, nameof(academicYearId));
		StandardId = Check.NotDefaultOrNull<Guid>(standardId, nameof(standardId));
		RoomId = Check.NotDefaultOrNull<Guid>(roomId, nameof(roomId));
	}
}
