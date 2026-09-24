using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class StudentOnboardingSettingsValidation_Tests
{
	[Fact]
	public void Validator_Should_Accept_A_Versioned_Object_Configuration()
	{
		var result = new SaveStudentOnboardingSettingsDtoValidator().Validate(
			new SaveStudentOnboardingSettingsDto
			{
				SchemaVersion = StudentOnboardingSettingsConsts.CurrentSchemaVersion,
				ConfigurationJson = "{\"readinessAccess\":{\"requireDocuments\":true}}"
			});

		result.IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData(2, "{}")]
	[InlineData(1, "[]")]
	[InlineData(1, "not-json")]
	public void Validator_Should_Reject_Unsupported_Or_Invalid_Configurations(
		int schemaVersion, string configurationJson)
	{
		var result = new SaveStudentOnboardingSettingsDtoValidator().Validate(
			new SaveStudentOnboardingSettingsDto
			{
				SchemaVersion = schemaVersion,
				ConfigurationJson = configurationJson
			});

		result.IsValid.ShouldBeFalse();
	}
}
