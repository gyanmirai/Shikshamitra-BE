using System;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentCoreIdentity_Tests
{
	[Theory]
	[InlineData("Cher", null, GenderEnum.Female, true)]
	[InlineData("Jay", "Shah", GenderEnum.Male, true)]
	[InlineData(" ", null, GenderEnum.Female, false)]
	[InlineData("Cher", "Shah", (GenderEnum)99, false)]
	public void Onboarding_identity_accepts_single_names_but_not_blank_names_or_undefined_gender(
		string name, string? surname, GenderEnum gender, bool expected)
	{
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), null, name, null, surname,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null,
			new DateTimeOffset(2016, 1, 1, 0, 0, 0, TimeSpan.Zero), gender, "9876543210");
		AutomaticOnboardingReadinessRules.HasCompleteIdentity(student).ShouldBe(expected);
	}
}
