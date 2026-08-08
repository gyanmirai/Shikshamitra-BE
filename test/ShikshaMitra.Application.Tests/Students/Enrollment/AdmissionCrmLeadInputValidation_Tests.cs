using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmLeadInputValidation_Tests
{
	[Fact]
	public void Malformed_contact_is_rejected_before_duplicate_preview_or_creation()
	{
		var results = Validate(CreateInput("abc1"));

		results.ShouldContain(result => result.MemberNames.Contains(nameof(CreateAdmissionCrmLeadDto.PrimaryContactNo)));
	}

	[Fact]
	public void International_contact_and_boundary_length_name_are_accepted()
	{
		var input = CreateInput("+1 (604) 555-0123");
		input.FirstName = new string('A', AdmissionCrmConsts.MaxNameLength);

		Validate(input).ShouldBeEmpty();
	}

	[Fact]
	public void Name_above_the_contract_boundary_is_rejected()
	{
		var input = CreateInput("9999900807");
		input.FirstName = new string('A', AdmissionCrmConsts.MaxNameLength + 1);

		Validate(input).ShouldContain(result =>
			result.MemberNames.Contains(nameof(CreateAdmissionCrmLeadDto.FirstName)));
	}

	private static List<ValidationResult> Validate(CreateAdmissionCrmLeadDto input)
	{
		var results = new List<ValidationResult>();
		Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);
		return results;
	}

	private static CreateAdmissionCrmLeadDto CreateInput(string contact) => new()
	{
		FirstName = "Aarav",
		PrimaryContactNo = contact,
		AdmissionIntent = "General admission"
	};
}
