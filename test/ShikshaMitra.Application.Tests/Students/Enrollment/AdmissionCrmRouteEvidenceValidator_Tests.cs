using System.Collections.Generic;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmRouteEvidenceValidator_Tests
{
	[Fact]
	public void Null_configuration_and_input_collections_are_normalized()
	{
		var result = AdmissionCrmRouteEvidenceValidator.Validate(
			"{\"requiredEvidenceCodes\":null}",
			(IReadOnlyList<string>)null!);

		result.IsValid.ShouldBeTrue();
		result.MissingCodes.ShouldBeEmpty();
		result.InvalidReferences.ShouldBeEmpty();
	}

	[Fact]
	public void Required_codes_need_well_formed_coded_references()
	{
		var result = AdmissionCrmRouteEvidenceValidator.Validate(
			"{\"requiredEvidenceCodes\":[\"ALLOTMENT\",\"AGE_PROOF\"]}",
			["ALLOTMENT:AUTH-100", "AGE_PROOF"]);

		result.IsValid.ShouldBeFalse();
		result.MissingCodes.ShouldContain("AGE_PROOF");
		result.InvalidReferences.ShouldContain("AGE_PROOF");
	}
}
