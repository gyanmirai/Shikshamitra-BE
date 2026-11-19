using System;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class StudentRecordInheritedIdentity_Tests
{
	[Fact]
	public void Inherited_question_identity_survives_effective_code_changes()
	{
		var family = Guid.NewGuid();
		var first = Source(family, "INTEREST", "custom:stable-interest");
		var renamed = Source(family, "INQ_INTEREST_1", "custom:stable-interest");

		StudentRecordCustomDetailsService.Identity(first, first.Definition!.Sections[0].Fields[0])
			.ShouldBe(StudentRecordCustomDetailsService.Identity(renamed, renamed.Definition!.Sections[0].Fields[0]));
	}

	[Fact]
	public void Unrelated_inherited_questions_do_not_share_current_values_because_their_codes_match()
	{
		var family = Guid.NewGuid();
		var interest = Source(family, "OTHER", "custom:interest");
		var support = Source(family, "OTHER", "custom:support");

		StudentRecordCustomDetailsService.Identity(interest, interest.Definition!.Sections[0].Fields[0])
			.ShouldNotBe(StudentRecordCustomDetailsService.Identity(support, support.Definition!.Sections[0].Fields[0]));
	}

	[Fact]
	public void Legacy_custom_record_identity_remains_unchanged()
	{
		var family = Guid.Parse("97c78924-ae78-4d6d-a7ed-2d258b829fb1");
		var source = Source(family, "NOTE", "custom:unused");
		var field = source.Definition!.Sections[0].Fields[0];
		field.InheritedFrom = null;

		StudentRecordCustomDetailsService.Identity(source, field)
			.ShouldBe("97c78924-ae78-4d6d-a7ed-2d258b829fb1:NOTE:Text:False");
	}

	private static StudentRecordRegistrationSourceDto Source(Guid family, string code, string identity)
	{
		var section = new AdmissionFormSectionDto { Id = Guid.NewGuid(), Code = "DETAILS", Name = "Details" };
		section.Fields.Add(new AdmissionFormFieldDto
		{
			Code = code, Label = "School question", SectionId = section.Id,
			FieldType = AdmissionFormFieldType.Text, BindingType = AdmissionFormBindingType.Custom,
			FieldIdentity = identity, InheritedFrom = new() { FieldIdentity = identity, SourceFieldCode = code }
		});
		return new() { FormFamilyId = family, Definition = new() { Sections = [section] } };
	}
}
