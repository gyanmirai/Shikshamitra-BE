using System;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructure
{
	/// <summary>Verified placement in this assignment's year; null means legacy evidence was not captured.</summary>
	public Guid? AcademicStandardId { get; private set; }
	public Guid? AcademicDivisionId { get; private set; }
	public int? AcademicRollNumber { get; private set; }

	public void CaptureAcademicPlacement(Guid standardId, Guid? divisionId, int? rollNumber)
	{
		if (standardId == Guid.Empty || divisionId == Guid.Empty)
			throw new ArgumentException("Academic placement identifiers must be valid.");
		if (AcademicStandardId.HasValue && (AcademicStandardId != standardId || AcademicDivisionId != divisionId || AcademicRollNumber != rollNumber))
			throw new InvalidOperationException("Assignment academic placement evidence is immutable.");
		AcademicStandardId = standardId;
		AcademicDivisionId = divisionId;
		AcademicRollNumber = rollNumber;
	}
}
