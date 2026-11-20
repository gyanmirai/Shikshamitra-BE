using System;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryLoanBorrowerProvenance_Tests
{
	[Fact]
	public void Canonical_Student_Admission_Should_Identify_Its_Explicitly_Captured_Borrower_Context()
	{
		var yearId = Guid.NewGuid();
		var boardId = Guid.NewGuid();
		var mediumId = Guid.NewGuid();
		var stageId = Guid.NewGuid();
		var standardId = Guid.NewGuid();
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "STUDENT-PROVENANCE", Guid.NewGuid(), 0,
			new(2026, 11, 17), new(2026, 11, 24), new(yearId, boardId, mediumId, stageId, standardId, null,
				"{}", "{}", "{}", Guid.NewGuid(), new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero)));

		loan.BorrowerScopeProvenance.ShouldBe(LibraryBorrowerScopeProvenance.CapturedAtOperation);
		loan.BorrowerBoardId.ShouldBe(boardId);
		loan.BorrowerMediumId.ShouldBe(mediumId);
		loan.BorrowerStageId.ShouldBe(stageId);
		loan.BorrowerStandardId.ShouldBe(standardId);
		loan.BorrowerScopeCaptureId.ShouldBeNull();
		loan.BorrowerScopeCaptureVersion.ShouldBeNull();
	}

	[Fact]
	public void A_Legacy_Raw_Loan_Should_Keep_Unknown_Borrower_Provenance_Without_A_Guessed_Capture()
	{
		var loan = LegacyLoan();

		loan.BorrowerScopeProvenance.ShouldBe(LibraryBorrowerScopeProvenance.Unknown);
		loan.BorrowerScopeCaptureId.ShouldBeNull();
		loan.BorrowerScopeCaptureVersion.ShouldBeNull();
	}

	[Fact]
	public void Confirming_Legacy_School_Dates_Should_Not_Confirm_Unknown_Borrower_Placement()
	{
		var loan = LegacyLoan();

		loan.ReviewSchoolDates(new(2026, 11, 17), new(2026, 11, 24), new(2026, 11, 24), null);

		loan.IssueDateProvenance.ShouldBe(LibraryCivilDateProvenance.ReviewedLegacy);
		loan.DueDateProvenance.ShouldBe(LibraryCivilDateProvenance.ReviewedLegacy);
		loan.BorrowerScopeProvenance.ShouldBe(LibraryBorrowerScopeProvenance.Unknown);
		loan.BorrowerScopeCaptureId.ShouldBeNull();
	}

	private static BookIssue LegacyLoan() => new(Guid.NewGuid(), Guid.NewGuid(), "LEGACY-PROVENANCE", Guid.NewGuid(), 0,
		new DateTime(2026, 11, 17, 19, 30, 0), new DateTime(2026, 11, 24, 2, 15, 0), Guid.NewGuid());
}
