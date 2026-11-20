using System;
using System.Text.Json;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Reservations;

public partial class LibraryReservationCoordinator
{
	private LibraryReservationBorrowerContext? CaptureStudentScope(LibraryReservationBorrowerFacts facts, Guid yearId,
		LibraryBorrowerScopeProvenance provenance)
	{
		if (facts.Role != 0) return null;
		var snapshot = JsonSerializer.Serialize(new {
			Version = 1, Source = "StudentAcademicPlacement", TenantId = CurrentTenant.Id, AcademicYearId = yearId,
			MemberRole = facts.Role, Provenance = provenance, CapturedAt = Now,
			facts.BoardId, facts.MediumId, facts.StageId, facts.StandardId, facts.DivisionId
		});
		return new(facts.BoardId, facts.MediumId, facts.StageId, facts.StandardId, facts.DivisionId, snapshot);
	}
}
