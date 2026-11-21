using System;

namespace ShikshaMitra.Library.Shared;

public sealed record LibraryBorrowerAssignmentEvidence(Guid OrganizationAssignmentId, Guid StageId,
	Guid? MediumId, Guid? BoardId, Guid? AcademicYearId)
{
	public bool IsComplete => MediumId.HasValue && MediumId != Guid.Empty && BoardId.HasValue && BoardId != Guid.Empty &&
		AcademicYearId.HasValue && AcademicYearId != Guid.Empty;
}
