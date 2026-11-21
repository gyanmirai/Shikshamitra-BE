using System;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class AssignmentAcademicPlacement_Tests
{
	[Fact]
	public void Placement_evidence_is_optional_for_legacy_and_immutable_after_capture()
	{
		var assignment = new StudentFeeStructure(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		assignment.AcademicStandardId.ShouldBeNull(); assignment.AcademicDivisionId.ShouldBeNull(); assignment.AcademicRollNumber.ShouldBeNull();
		var standard = Guid.NewGuid(); var division = Guid.NewGuid();
		assignment.CaptureAcademicPlacement(standard, division, 17);
		assignment.CaptureAcademicPlacement(standard, division, 17);
		assignment.AcademicStandardId.ShouldBe(standard); assignment.AcademicDivisionId.ShouldBe(division); assignment.AcademicRollNumber.ShouldBe(17);
		Should.Throw<InvalidOperationException>(() => assignment.CaptureAcademicPlacement(standard, Guid.NewGuid(), 99));
		assignment.AcademicDivisionId.ShouldBe(division); assignment.AcademicRollNumber.ShouldBe(17);
	}

	[Fact]
	public void Missing_division_is_durable_evidence_and_cannot_be_filled_from_later_placement()
	{
		var assignment = new StudentFeeStructure(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		var standard = Guid.NewGuid();
		assignment.CaptureAcademicPlacement(standard, null, null);
		Should.Throw<InvalidOperationException>(() => assignment.CaptureAcademicPlacement(standard, Guid.NewGuid(), 3));
		assignment.AcademicDivisionId.ShouldBeNull(); assignment.AcademicRollNumber.ShouldBeNull();
	}
}
