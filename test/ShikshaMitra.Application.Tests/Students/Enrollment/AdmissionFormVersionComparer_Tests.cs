using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormVersionComparer_Tests
{
	[Fact]
	public void Comparison_returns_directional_field_details_with_the_existing_change_messages()
	{
		var first = Version("Admission", ("NATIONALITY", "Nationality"), ("PARENT_EMAIL", "Parent email"));
		var second = Version("Admission application", ("BIRTHPLACE", "Place of birth (as recorded)"), ("PARENT_EMAIL", "Parent email address"));

		var result = AdmissionFormVersionComparer.CompareVersions(first, second);

		result.FromId.ShouldBe(first.Id);
		result.ToId.ShouldBe(second.Id);
		result.Changes.ShouldBe(AdmissionFormVersionComparer.Compare(first, second));
		result.FieldChanges.ShouldBe([
			new(1, "STUDENT", "Student details (local)", "Place of birth (as recorded)", "added"),
			new(2, "STUDENT", "Student details (local)", "Nationality", "removed"),
			new(3, "STUDENT", "Student details (local)", "Parent email → Parent email address", "changed")
		]);

		var reverse = AdmissionFormVersionComparer.CompareVersions(second, first);
		reverse.FieldChanges.ShouldContain(change => change.FieldName == "Place of birth (as recorded)" && change.Kind == "removed");
	}

	private static AdmissionFormDto Version(string name, params (string Code, string Label)[] fields) => new()
	{
		Id = Guid.NewGuid(),
		Name = name,
		Sections =
		[
			new()
			{
				Code = "STUDENT",
				Name = "Student details (local)",
				Fields = [.. Array.ConvertAll(fields, field => new AdmissionFormFieldDto { Code = field.Code, Label = field.Label })]
			}
		]
	};
}
