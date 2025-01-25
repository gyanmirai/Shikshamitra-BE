using ShikshaMitra.Common;
using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Students.StudentEnrollments.Registration;

public class StudentAdditionalDetails(
	Category? category,
	YesNoStatus? rightToEducation,
	YesNoStatus? bPLStudent,
	string? bPLCardNo,
	YesNoStatus? personWithDisability,
	string? secondLanguage,
	string? emergencyContactNo,
	YesNoStatus? singleParentChild,
	YesNoStatus? singleChild,
	string? sponsorName
	) : ValueObject
{
	public Category? Category { get; private set; } = category;
	public YesNoStatus? RightToEducation { get; private set; } = rightToEducation;
	public YesNoStatus? BPLStudent { get; private set; } = bPLStudent;
	public string? BPLCardNo { get; private set; } = bPLCardNo;
	public YesNoStatus? PersonWithDisability { get; private set; } = personWithDisability;
	public string? SecondLanguage { get; private set; } = secondLanguage;
	public string? EmergencyContactNo { get; private set; } = emergencyContactNo;
	public YesNoStatus? SingleParentChild { get; private set; } = singleParentChild;
	public YesNoStatus? SingleChild { get; private set; } = singleChild;
	public string? SponsorName { get; private set; } = sponsorName;

	protected override IEnumerable<object> GetAtomicValues()
	{
		yield return Category;
		yield return RightToEducation;
		yield return BPLStudent;
		yield return BPLCardNo;
		yield return PersonWithDisability;
		yield return SecondLanguage;
		yield return EmergencyContactNo;
		yield return SingleParentChild;
		yield return SingleChild;
		yield return SponsorName;
			
	}
	
}