using System;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentPortalOwnerSelection_Tests
{
	[Fact]
	public void Recorded_parents_take_priority_even_when_their_emails_are_missing()
	{
		var family = Family();
		family.UpdateFatherDetails(Person("Father"));
		family.UpdateMotherDetails(Person("Mother"));
		StudentPortalOwnerSelection.Relationships(family).ShouldBe(["father", "mother"]);
		StudentPortalOwnerSelection.AutomaticRelationship(family, "guardian").ShouldBe("father");
	}

	[Fact]
	public void Automatic_selection_respects_a_saved_parent_and_uses_an_available_parent_email()
	{
		var family = Family();
		family.UpdateFatherDetails(Person("Father"));
		family.UpdateMotherDetails(Person("Mother", "mother@example.test"));
		StudentPortalOwnerSelection.AutomaticRelationship(family, "mother").ShouldBe("mother");
		StudentPortalOwnerSelection.AutomaticRelationship(family, "guardian").ShouldBe("mother");
		StudentPortalOwnerSelection.AutomaticRelationship(family, "father").ShouldBe("father");
	}

	[Fact]
	public void Other_guardian_is_available_only_without_either_parent()
	{
		var family = Family();
		family.UpdateFatherDetails(Person("  "));
		StudentPortalOwnerSelection.Relationships(family).ShouldBe(["guardian"]);
		StudentPortalOwnerSelection.AutomaticRelationship(family, "father").ShouldBe("guardian");
		family.UpdateMotherDetails(Person("Mother"));
		StudentPortalOwnerSelection.Relationships(family).ShouldBe(["mother"]);
	}

	[Fact]
	public void Missing_family_does_not_invent_an_account_owner()
	{
		StudentPortalOwnerSelection.Relationships(null).ShouldBeEmpty();
		StudentPortalOwnerSelection.AutomaticRelationship(null, "guardian").ShouldBeNull();
	}

	private static StudentFamilyDetail Family()
	{
		var family = new StudentFamilyDetail(Guid.NewGuid(), Guid.NewGuid());
		family.UpdateGuardianDetails(Person("Other Guardian", "other@example.test"));
		return family;
	}

	private static ParentGuardianDetails Person(string name, string? email = null) =>
		new(name, null, null, email, null, null, null, null, null, null, null, null, null);
}
