using System;

namespace ShikshaMitra.Library.Policies;

public sealed record LibraryBorrowingPolicyContext(Guid AcademicYearId, int Role, DateOnly AsOfDate,
	Guid? BookCategoryId = null, Guid? SectionId = null, Guid? EducationMediumStageId = null, Guid? StandardId = null);

public sealed record LibraryBorrowingPolicyDecision(LibraryBorrowingPolicyVersion? Policy, int Role, int MaxBooksAtTime, int ReservationLimit)
{
	public bool IsLegacyManualDate => Policy == null;
}
