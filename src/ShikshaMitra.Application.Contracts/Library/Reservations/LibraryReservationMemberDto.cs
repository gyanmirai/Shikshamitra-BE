using System;

namespace ShikshaMitra.Library.Reservations;

public class LibraryReservationMemberDto
{
	public Guid Id { get; set; }
	public string? MemberName { get; set; }
	public string? MemberIdentifier { get; set; }
	public string? ClassDivision { get; set; }
	public int Role { get; set; }
	public Guid? AcademicYearId { get; set; }
	public bool IsEligibleForReservation { get; set; }
	public string? IneligibilityReason { get; set; }
}
