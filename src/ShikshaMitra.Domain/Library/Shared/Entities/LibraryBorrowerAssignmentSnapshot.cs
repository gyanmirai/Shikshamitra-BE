using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Shared;

public sealed class LibraryBorrowerAssignmentSnapshot : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid CaptureId { get; private set; }
	public Guid OrganizationAssignmentId { get; private set; }
	public Guid StageId { get; private set; }
	public Guid? MediumId { get; private set; }
	public Guid? BoardId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	private LibraryBorrowerAssignmentSnapshot() { }

	internal LibraryBorrowerAssignmentSnapshot(Guid id, Guid tenantId, Guid captureId, LibraryBorrowerAssignmentEvidence evidence) : base(id)
	{
		if (id == Guid.Empty || tenantId == Guid.Empty || captureId == Guid.Empty || evidence.OrganizationAssignmentId == Guid.Empty ||
			evidence.StageId == Guid.Empty || evidence.MediumId == Guid.Empty || evidence.BoardId == Guid.Empty || evidence.AcademicYearId == Guid.Empty)
			throw new BusinessException(LibraryErrorCodes.ContextInvalid);
		TenantId = tenantId; CaptureId = captureId; OrganizationAssignmentId = evidence.OrganizationAssignmentId;
		StageId = evidence.StageId; MediumId = evidence.MediumId; BoardId = evidence.BoardId; AcademicYearId = evidence.AcademicYearId;
	}
}
