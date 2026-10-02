using System.Linq;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Records;

public sealed class StudentRecordTransitionIssueCode_Tests
{
	[Theory]
	[InlineData(StudentOnboardingErrorCodes.DivisionFull, "Students.Onboarding.DivisionFull")]
	[InlineData(StudentOnboardingErrorCodes.RollNumberDuplicate, "Students.Onboarding.RollNumberDuplicate")]
	[InlineData("DivisionFull", "Students:Records:DivisionFull")]
	[InlineData("Students:Records:StaleSnapshot", "Students:Records:StaleSnapshot")]
	public void Record_readiness_preview_preserves_owner_error_contract_and_qualifies_local_codes(string sourceCode, string expectedCode)
	{
		var row = new StudentRecordTransitionPreviewRowDto();
		StudentRecordTransitionReadinessService.Add(row, sourceCode, "The owning check requires staff action.");
		var issue = row.Issues.Single(); issue.Code.ShouldBe(expectedCode); issue.Message.ShouldBe("The owning check requires staff action.");
	}
}
