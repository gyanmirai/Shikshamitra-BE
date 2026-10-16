using System;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentRollOrdering_Tests
{
	private static readonly StudentRollCandidate[] People = [Person(1, "Jay", "Shah", GenderEnum.Male), Person(2, "Anaya", "Desai", GenderEnum.Female), Person(3, "Aarav", "Patel", GenderEnum.Male), Person(4, "Riya", "Mehta", GenderEnum.Female)];
	private static StudentRollCandidate Person(int id, string first, string? last, GenderEnum? gender, string? gr = null) => new(new Guid(id, 0, 0, new byte[8]), first, null, last, gender, gr, "ADM-" + id, DateTime.UtcNow.Date);
	private static StudentRollOrdering Ordering(string grouping = "mixed", string direction = "ascending", string nameFormat = "first-name-first", string field = "studentName") =>
		new(JsonSerializer.SerializeToElement(new { grouping, nameFormat, orderingRules = new[] { new { field, direction } } }));
	[Theory]
	[InlineData("mixed", "Aarav,Anaya,Jay,Riya")]
	[InlineData("boys-first", "Aarav,Jay,Anaya,Riya")]
	[InlineData("girls-first", "Anaya,Riya,Aarav,Jay")]
	public void Approved_gender_patterns_use_one_shared_order(string grouping, string expected) =>
		string.Join(",", Ordering(grouping).Order(People).Select(x => x.FirstName)).ShouldBe(expected);
	[Fact] public void Descending_changes_order_inside_groups_not_gender_priority() =>
		string.Join(",", Ordering("boys-first", "descending").Order(People).Select(x => x.FirstName)).ShouldBe("Jay,Aarav,Riya,Anaya");
	[Fact] public void Surname_order_is_school_configurable() =>
		string.Join(",", Ordering(nameFormat: "surname-first").Order(People).Select(x => x.FirstName)).ShouldBe("Anaya,Riya,Aarav,Jay");
	[Fact] public void Missing_gender_blocks_grouped_but_not_mixed_numbering()
	{
		var person = Person(1, "Jay", "Shah", null);
		Ordering("boys-first").Issue(person).ShouldNotBeNull(); Ordering().Issue(person).ShouldBeNull();
		Should.Throw<BusinessException>(() => Ordering("girls-first").Order([person])).Code.ShouldBe(StudentRollNumberErrorCodes.BatchBlocked);
	}
	[Fact] public void Unknown_gender_is_never_guessed_into_a_gender_group() => Ordering("boys-first").Issue(Person(1, "Jay", "Shah", (GenderEnum)99)).ShouldNotBeNull();
	[Theory][InlineData("grNumber")][InlineData("admissionNumber")][InlineData("admissionDate")]
	public void Missing_chosen_source_is_actionable(string field)
	{
		var person = Person(1, "Jay", "Shah", GenderEnum.Male) with { GrNumber = null, AdmissionNumber = null, AdmissionDate = null };
		Ordering(field: field).Issue(person).ShouldNotBeNull();
	}
	[Fact] public void GR_numeric_runs_are_compared_without_lexical_or_overflow_errors() =>
		Ordering(field: "grNumber").Order([Person(1, "Jay", "Shah", null, "GR-10"), Person(2, "Anaya", "Desai", null, "GR-2")]).First().GrNumber.ShouldBe("GR-2");
	[Fact] public void Equal_names_have_stable_identity_tie_breakers_independent_of_input_order()
	{
		var first = Person(1, "Jay", "Shah", null); var second = Person(2, "Jay", "Shah", null);
		Ordering().Order([second, first]).Select(x => x.StudentId).ShouldBe(Ordering().Order([first, second]).Select(x => x.StudentId));
	}
	[Fact] public void Missing_surname_is_not_invented() => Ordering(nameFormat: "surname-first").Issue(Person(1, "Jay", null, null)).ShouldNotBeNull();
	[Theory][InlineData("grouping", "alternate")][InlineData("nameFormat", "random")][InlineData("arrangeBy", "phone")][InlineData("arrangeType", "random")]
	public void Unsupported_rule_values_fail_closed(string property, string value) =>
		Should.Throw<BusinessException>(() => new StudentRollOrdering(JsonSerializer.SerializeToElement(new System.Collections.Generic.Dictionary<string, string> { [property] = value }))).Code.ShouldBe(StudentRollNumberErrorCodes.InvalidRule);
}
