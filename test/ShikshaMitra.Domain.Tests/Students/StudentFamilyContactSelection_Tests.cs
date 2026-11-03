using System;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentFamilyContactSelection_Tests
{
	[Fact]
	public void Guardian_responsibility_acknowledgment_does_not_change_app_access_identity()
	{
		StudentFamilyContactSelection.RequiresAccessReview(
			"{\"guardianDetails\":{\"fullName\":\"Aunt\"},\"mainContactRelationship\":\"father\"}",
			"{\"guardianDetails\":{\"fullName\":\"Aunt\"},\"mainContactRelationship\":\"guardian\",\"guardianResponsibilityConfirmed\":true}").ShouldBeFalse();
	}
	[Fact]
	public void Sole_parent_is_default_even_when_an_other_guardian_is_saved()
	{
		var student = StudentWithFamily();
		student.UpdateFamilyInfo(Person("Rajesh", null), null, Person("Anita", "anita@example.com"), []);
		StudentFamilyContactSelection.Resolve(student).ShouldBe("father");
	}

	[Fact]
	public void Two_parents_require_an_explicit_choice_instead_of_choosing_by_email()
	{
		var student = StudentWithFamily();
		student.UpdateFamilyInfo(Person("Rajesh", null), Person("Meena", "meena@example.com"), null, []);
		StudentFamilyContactSelection.Resolve(student).ShouldBeNull();
		StudentFamilyContactSelection.Select(student, "mother");
		StudentFamilyContactSelection.Resolve(student).ShouldBe("mother");
	}

	[Fact]
	public void Guardian_can_be_explicitly_selected_without_changing_saved_parents_or_siblings()
	{
		var student = StudentWithFamily();
		var sibling = Guid.NewGuid();
		student.UpdateFamilyInfo(Person("Rajesh", "father@example.com"), Person("Meena", "mother@example.com"), Person("Anita", "aunt@example.com"), [sibling]);
		StudentFamilyContactSelection.Select(student, "guardian");
		StudentFamilyContactSelection.Resolve(student).ShouldBe("guardian");
		student.FamilyDetail!.FatherDetails!.Email.ShouldBe("father@example.com");
		student.FamilyDetail.MotherDetails!.Email.ShouldBe("mother@example.com");
		student.FamilyDetail.SiblingStudentIds.ShouldBe([sibling]);
		student.UserId.ShouldBeNull();
	}

	[Fact]
	public void An_absent_or_unknown_contact_cannot_be_selected()
	{
		var student = StudentWithFamily();
		student.UpdateFamilyInfo(Person("Rajesh", null), null, null, []);
		Should.Throw<BusinessException>(() => StudentFamilyContactSelection.Select(student, "guardian"));
		Should.Throw<BusinessException>(() => StudentFamilyContactSelection.Select(student, "student"));
	}

	private static Student StudentWithFamily() => Student.CreateDirectRegistration(Guid.NewGuid(), Guid.NewGuid(), "Rahul", null, "Sharma",
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, new DateTime(2018, 5, 15), GenderEnum.Male, "9876543210");
	private static ParentGuardianDetails Person(string name, string? email) => new(name, null, null, email, null, null, null, null, null, null, null, null, null);

	[Fact]
	public void Contact_preference_only_change_preserves_portal_access_but_identity_edits_require_review()
	{
		const string before = "{\"fatherDetails\":{\"email\":\"father@example.com\"},\"guardianDetails\":{\"email\":\"aunt@example.com\"},\"mainContactRelationship\":\"father\",\"guardianRelationship\":\"Aunt\",\"siblingStudentIds\":[\"same-sibling\"]}";
		var after = before.Replace("\"mainContactRelationship\":\"father\"", "\"mainContactRelationship\":\"guardian\"");
		StudentFamilyContactSelection.RequiresAccessReview(before, after).ShouldBeFalse();
		StudentFamilyContactSelection.RequiresAccessReview(before, after.Replace("father@example.com", "changed@example.com")).ShouldBeTrue();
		StudentFamilyContactSelection.RequiresAccessReview(before, "{}").ShouldBeTrue();
	}

	[Fact]
	public void Adding_an_optional_guardian_preserves_an_unchanged_linked_parent()
	{
		const string before = "{\"fatherDetails\":{\"email\":\"father@example.com\"},\"guardianDetails\":null}";
		const string after = "{\"fatherDetails\":{\"email\":\"father@example.com\"},\"guardianDetails\":{\"email\":\"aunt@example.com\"}}";
		StudentFamilyContactSelection.RequiresAccessReview(before, after, ["father"]).ShouldBeFalse();
		StudentFamilyContactSelection.RequiresAccessReview(before, after.Replace("father@example.com", "changed@example.com"), ["father"]).ShouldBeTrue();
		StudentFamilyContactSelection.RequiresAccessReview(before, after, ["unknown"]).ShouldBeTrue();
	}

	[Theory]
	[InlineData("father,mother,guardian", null, "father,mother")]
	[InlineData("father,mother,guardian", "guardian", "guardian")]
	[InlineData("father,mother,guardian", "mother", "mother")]
	[InlineData("guardian", null, "guardian")]
	[InlineData("father", "guardian", "")]
	public void School_messages_prioritise_parents_and_require_explicit_guardian_choice_when_parents_exist(string available, string? selected, string expected)
	{
		string.Join(",", StudentFamilyContactSelection.MessageRelationships(available.Split(','), selected)).ShouldBe(expected);
	}
}
