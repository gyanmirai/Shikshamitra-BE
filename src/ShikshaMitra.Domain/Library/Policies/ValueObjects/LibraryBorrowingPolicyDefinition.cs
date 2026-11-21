using System;
using Volo.Abp;

namespace ShikshaMitra.Library.Policies;

public sealed record LibraryBorrowingPolicyDefinition(
	string Name, int Role, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid? BookCategoryId,
	Guid? SectionId, Guid? EducationMediumStageId, Guid? StandardId, int Priority, LibraryBorrowingTerms Terms)
{
	public LibraryBorrowingPolicyDefinition Validated()
	{
		if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length > 160 || Role is < 0 or > 1 ||
			EffectiveFrom == default || EffectiveTo < EffectiveFrom || Priority is < 0 or > 1000 ||
			BookCategoryId == Guid.Empty || SectionId == Guid.Empty || EducationMediumStageId == Guid.Empty || StandardId == Guid.Empty ||
			Role == 1 && (EducationMediumStageId.HasValue || StandardId.HasValue) || Terms == null)
			throw new BusinessException(LibraryErrorCodes.PolicyInvalid, "The borrowing policy context, effective dates, and selectors must be valid.");
		Terms.Validated();
		return this with { Name = Name.Trim() };
	}
}
