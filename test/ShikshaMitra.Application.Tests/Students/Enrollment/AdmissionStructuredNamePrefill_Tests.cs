using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionStructuredNamePrefill_Tests
{
	[Theory]
	[InlineData("Applicant.Name")]
	[InlineData("STUDENT_NAME")]
	public void Captured_full_name_is_not_offered_as_a_structured_first_name(string sourceKey)
	{
		AdmissionStructuredNamePrefill.FirstName("Aarav Shah", new Dictionary<string, string> { [sourceKey] = "\"Aarav Shah\"" })
			.ShouldBeNull();
	}

	[Fact]
	public void Known_structured_first_name_is_preserved_even_when_full_name_was_captured()
	{
		AdmissionStructuredNamePrefill.FirstName("Aarav", new Dictionary<string, string>
		{
			["Applicant.Name"] = "\"Aarav Shah\"", ["Applicant.FirstName"] = "\"Aarav\""
		}).ShouldBe("Aarav");
	}

	[Fact]
	public void An_explicitly_corrected_first_name_is_preserved()
	{
		AdmissionStructuredNamePrefill.FirstName("Aarav", new Dictionary<string, string> { ["Applicant.Name"] = "\"Aarav Original Inquiry Name\"" })
			.ShouldBe("Aarav");
	}

	[Fact]
	public void Legacy_identity_without_a_captured_full_name_is_preserved()
	{
		AdmissionStructuredNamePrefill.FirstName("Aarav Shah", new Dictionary<string, string>()).ShouldBe("Aarav Shah");
	}
}
